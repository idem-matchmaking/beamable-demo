using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable.Microservices.Idem.IdemLogic;
using Beamable.Microservices.Idem.Shared;
using Beamable.Microservices.Idem.Shared.MicroserviceSchema;
using Beamable.Microservices.Idem.Tools;
using Beamable.Server;
using UnityEngine;
using WebSocketSharp;

namespace Beamable.Microservices
{
	[Microservice("IdemMicroservice")]
	public class IdemMicroservice : Microservice
	{
        
        // microsservice config keys
        private const string ConfigNamespace = "Idem";
        private const string IdemUsernameConfigKey = "Username";
        private const string IdemPasswordConfigKey = "Password";
        private const string IdemClientIdConfigKey = "ClientId";
        private const string DebugConfigKey = "Debug";
        private const string SupportedGameModesConfigKey = "SupportedGameModes";
        private const string PlayerTimoutMsConfigKey = "PlayerTimeoutMs";
        
        // microservice state
        private static bool debug = false;
        private static WebSocket ws = null;
        private static Task<bool> connectionTask = null;
        private static IdemLogic logic = null;
        private static readonly List<string> gameModes = new();
        
        // convenience
        private string playerId => Context.UserId == 0 ? null : Context.UserId.ToString();

        [AdminOnlyCallable]
        public async Task<string> AdminGetPlayers(string gameId)
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.GetPlayers(gameId);
            return result.ToJson();
        }

        [AdminOnlyCallable]
        public async Task<string> AdminGetMatches(string gameId)
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.GetMatches(gameId);
            return result.ToJson();
        }

        [AdminOnlyCallable]
        public async Task<string> AdminGetRecentPlayer(string anyPlayerId)
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.GetRecentPlayer(anyPlayerId);
            return result.ToJson();
        }
        
        [ClientCallable]
        public async Task<string> StartMatchmaking(string gameMode, string[] servers)
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.StartMatchmaking(playerId, gameMode, servers);
            return result.ToJson();
        }
        
        [ClientCallable]
        public async Task<string> StopMatchmaking()
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.StopMatchmaking(playerId);
            return result.ToJson();
        }
        
        [ClientCallable]
        public async Task<string> GetMatchmakingStatus()
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.GetMatchmakingStatus(playerId);
            return result.ToJson();
        }
        
        [ClientCallable]
        public async Task<string> ConfirmMatch(string matchId)
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var result = logic.ConfirmMatch(playerId, matchId);
            return result.ToJson();
        }
        
        [ClientCallable]
        public async Task<string> CompleteMatch(string matchId, string payload)
        {
            var connectionError = await CheckConnection();
            if (connectionError != null)
                return connectionError;

            var matchResult = JsonUtil.Parse<IdemMatchResult>(payload);

            var result = logic.CompleteMatch(playerId, matchId, matchResult);
            return result.ToJson();
        }

        private async Task<string> CheckConnection()
        {
            if (connectionTask == null)
            {
                await Initialize();
            }

            if (connectionTask == null || !await connectionTask)
            {
                // init failed
                return BaseResponse.InternalErrorFailure.ToJson();
            }

            return null;
        }

        private async Task Initialize()
        {
            var connectionCompletion = new TaskCompletionSource<bool>();
            connectionTask = connectionCompletion.Task;

            var config = await Services.RealmConfig.GetRealmConfigSettings();
            debug = !string.IsNullOrWhiteSpace(config.GetSetting(ConfigNamespace, DebugConfigKey));
            var idemUsername = config.GetSetting(ConfigNamespace, IdemUsernameConfigKey);
            var idemPassword = config.GetSetting(ConfigNamespace, IdemPasswordConfigKey);
            var idemClientId = config.GetSetting(ConfigNamespace, IdemClientIdConfigKey);
            var gameModesStr = config.GetSetting(ConfigNamespace, SupportedGameModesConfigKey);
            if (string.IsNullOrWhiteSpace(idemUsername) || string.IsNullOrWhiteSpace(idemPassword) ||
                string.IsNullOrWhiteSpace(idemClientId) || string.IsNullOrWhiteSpace(gameModesStr))
            {
                Fail($"Idem config is not complete: {IdemUsernameConfigKey}, {IdemPasswordConfigKey}, {IdemClientIdConfigKey}, {SupportedGameModesConfigKey} are required");
                return;
            }

            var token = await AWSAuth.AuthAndGetToken(idemUsername, idemPassword, idemClientId, debug);
            if (string.IsNullOrWhiteSpace(token))
            {
                Fail($"Could not authorize with AWS Cognito: response token is empty");
                return;
            }

            gameModes.AddRange(gameModesStr
                .Split(",")
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
            );
            
            Debug.Log($"Supported game modes: {string.Join(", ", gameModes.Select(s => $"'{s}'"))}");
            if (gameModes.Count == 0)
            {
                Fail($"No supported game modes. Halt.");
                return;
            }
            
            var playerTimeoutMsStr = config.GetSetting(ConfigNamespace, PlayerTimoutMsConfigKey);
            if (!int.TryParse(playerTimeoutMsStr, out var playerTimeoutMs))
            {
                const int defaultPlayerTimeoutMs = 5000;
                Debug.LogWarning($"Could not parse player timeout ms: '{playerTimeoutMsStr}', using default {defaultPlayerTimeoutMs}");
                playerTimeoutMs = defaultPlayerTimeoutMs;
            }
            
            logic ??= new(debug, playerTimeoutMs, SendThroughWs);
            logic.UpdateSupportedGameModes(gameModes);
            
            // TODO_IDEM implement multiple game modes in the push mode
            var pushParams = $"receiveMatches=true&gameMode={gameModes[0]}&";
            var connectionUrl = $"wss://ws-int.idem.gg/?{pushParams}authorization={token}";
            Debug.Log($"Starting Idem connection to: {connectionUrl}");

            ws = new WebSocket(connectionUrl);
            ws.OnOpen += (sender, param) =>
            {
                connectionCompletion.SetResult(ws.ReadyState == WebSocketState.Open);
                Debug.Log("Idem WS is open");
            };
            ws.OnClose += OnWsClose;
            ws.OnMessage += OnWsMessage;
            ws.OnError += OnWsError;
            
            ws.Connect();

            Debug.Log($"Idem WS state: {ws.ReadyState}");
            
            return;

            void Fail(string message)
            {
                Debug.LogError(message);
                connectionCompletion.SetResult(false);
            }
        }

        private bool SendThroughWs(object payload)
        {
            if (payload == null)
            {
                Debug.LogError($"Trying to send null payload to Idem WS");
                return false;
            }

            if (ws is not { ReadyState: WebSocketState.Open })
            {
                Debug.LogError($"Trying to send payload with Idem WS in the wrong state '{ws?.ReadyState}'");
                return false;
            }
            
            var json = payload.ToJson();
            if (debug)
                Debug.Log($"Sending message to Idem: {json}");
            
            ws.Send(json);
            return true;
        }

        private void OnWsError(object sender, ErrorEventArgs errorArgs)
        {
            Debug.LogError($"Idem WS error: {errorArgs.Message}\n{errorArgs.Exception.StackTrace}");
            Debug.Log($"Idem WS state: {ws.ReadyState}");
            if (ws.ReadyState != WebSocketState.Open)
                ws.Close();
        }

        private void OnWsClose(object sender, CloseEventArgs closeArgs)
        {
            Debug.Log($"Idem WS is closed: {closeArgs.Code}, reason {closeArgs.Reason}, clean {closeArgs.WasClean}");
            ws = null;
            connectionTask = null;
        }

        private void OnWsMessage(object sender, MessageEventArgs messageArgs)
        {
            logic?.HandleIdemMessage(messageArgs.Data);
            
            if (debug)
                Debug.Log("Idem WS message: " + messageArgs.Data);
        }
	}
}
