using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Beamable.Microservices.Idem.Shared;
using Beamable.Microservices.Idem.Shared.MicroserviceSchema;
using UnityEngine;

namespace Beamable.Microservices.Idem.IdemLogic
{
    public class IdemLogic
    {
	    // TODO_IDEM
	    // - player timeouts
	    //  - player timeout to the configs
	    //  - on timeout remove from queue if waiting
	    //  - on timeout fail match if already matched with requeue for everybody else
	    
        private readonly bool debug;
        private readonly Dictionary<string, GameModeContainer> gameModes = new ();
        private readonly Func<object, bool> sendToIdem;
        private readonly Dictionary<Type, List<TaskCompletionSource<BaseIdemMessage>>> incomingAwaiters = new ();

        public IdemLogic(bool debug, Func<object, bool> sendToIdem)
        {
            this.debug = debug;
            this.sendToIdem = sendToIdem;
        }
        
        public void UpdateSupportedGameModes(IEnumerable<string> gameModesUpdate)
        {
	        var newGameModes = new HashSet<string>();
	        foreach (var mode in gameModesUpdate)
		        newGameModes.Add(mode);
	        
	        foreach (var mode in gameModes.Keys)
	        {
		        if (!newGameModes.Contains(mode))
			        gameModes.Remove(mode);
	        }
	        
	        foreach (var mode in newGameModes)
	        {
		        if (!gameModes.ContainsKey(mode))
			        gameModes[mode] = new GameModeContainer();
	        }
		}

        public async Task<BaseResponse> GetPlayers(string gameId)
        {
			var awaiter = new TaskCompletionSource<BaseIdemMessage>();
	        AddAwaiter<GetPlayersResponseMessage>(awaiter);
	        
	        var result = sendToIdem(new GetPlayersMessage(gameId));
	        if (!result)
		        return BaseResponse.IdemConnectionFailure;
	        
	        var response = await awaiter.Task;
	        return new StringResponse(response.ToJson());
        }
        
        public async Task<BaseResponse> GetMatches(string gameId)
        {
			var awaiter = new TaskCompletionSource<BaseIdemMessage>();
	        AddAwaiter<GetMatchesResponseMessage>(awaiter);
	        
	        var result = sendToIdem(new GetMatchesMessage(gameId));
	        if (!result)
		        return BaseResponse.IdemConnectionFailure;
	        
	        var response = await awaiter.Task;
	        return new StringResponse(response.ToJson());
        }

        public BaseResponse StartMatchmaking(string playerId, string gameMode, string[] servers)
        {
	        if (playerId == null)
		        return BaseResponse.UnauthorizedFailure;
		        
	        var gameContainer = GetGameMode(gameMode);
	        if (gameContainer == null)
		        return BaseResponse.UnsupportedGameModeFailure;

	        var result = sendToIdem(new AddPlayerMessage(gameMode, playerId, servers));
	        
	        return result ? BaseResponse.Success : BaseResponse.IdemConnectionFailure;
        }

        public BaseResponse StopMatchmaking(string playerId)
        {
	        if (playerId == null)
		        return BaseResponse.UnauthorizedFailure;

	        var success = true;
	        foreach (var (gameId, gameContainer) in gameModes)
	        {
		        if (gameContainer.waitingPlayers.Contains(playerId))
			        success = success && sendToIdem(new RemovePlayerMessage(gameId, playerId));

		        if (gameContainer.pendingMatches.TryGetValue(playerId, out var match))
			        success = success && sendToIdem(new FailMatchMessage(gameId, match.matchId, playerId,
				        match.players.Select(p => p.playerId)));
	        }

	        return success ? BaseResponse.Success : BaseResponse.IdemConnectionFailure;
        }

        public BaseResponse GetMatchmakingStatus(string playerId)
        {
	        foreach (var (gameId, gameContainer) in gameModes)
	        {
		        if (gameContainer.waitingPlayers.Contains(playerId))
			        return MMStateResponse.InQueue(gameId);

		        if (gameContainer.pendingMatches.TryGetValue(playerId, out var match))
			        return MMStateResponse.MatchFound(gameId, match.matchId, match.players);
	        }
	        
	        return MMStateResponse.None();
        }

        public BaseResponse ConfirmMatch(string playerId, string matchId)
        {
	        var match = FindMatch(playerId, matchId);
	        if (match == null)
		        return BaseResponse.UnknownMatchFailure;

	        match.ConfirmBy(playerId);
	        if (match.ConfirmedByAll && sendToIdem(new ConfirmMatchMessage(match.gameId, match.matchId)))
				return ConfirmMatchResponse.MatchConfirmed;

	        return ConfirmMatchResponse.MatchNotConfirmed;
        }

        public BaseResponse CompleteMatch(string playerId, string matchId, IdemMatchResult result)
        {
	        var match = FindMatch(playerId, matchId);
	        if (match == null)
		        return BaseResponse.UnknownMatchFailure;

	        return sendToIdem(new CompleteMatchMessage(result))
		        ? BaseResponse.Success
		        : BaseResponse.IdemConnectionFailure;
        }

        public void HandleIdemMessage(string message)
        {
            if (debug)
                Debug.Log($"Got message from Idem: {message}");
            
            var baseMessage = JsonUtil.Parse<BaseIdemMessage>(message);
            var fullMessage = BaseIdemMessage.ParseByAction(baseMessage, message);

            if (fullMessage.error != null)
            {
                Debug.LogError($"Got error from idem after action {fullMessage.action}: {fullMessage.error.code} - {fullMessage.error.message}");
                return;
            }

            switch (fullMessage)
            {
	            case AddPlayerResponseMessage addPlayerResponse:
		            OnAddPlayerResponse(addPlayerResponse);
		            break;
	            case RemovePlayerResponseMessage removePlayerResponse:
		            OnRemovePlayerResponse(removePlayerResponse);
		            break;
	            case GetPlayersResponseMessage getPlayersResponse:
		            OnGetPlayersResponse(getPlayersResponse);
		            break;
	            case GetMatchesResponseMessage getMatchesResponse:
		            OnGetMatchesResponse(getMatchesResponse);
		            break;
	            case ConfirmMatchResponseMessage confirmMatchResponse:
		            OnConfirmMatchResponse(confirmMatchResponse);
		            break;
	            case FailMatchResponseMessage failMatchResponse:
		            OnFailMatchResponse(failMatchResponse);
		            break;
	            case CompleteMatchResponseMessage completeMatchResponse:
		            OnCompleteMatchResponse(completeMatchResponse);
		            break;
	            case MatchSuggestionMessage matchSuggestion:
		            OnMatchSuggestion(matchSuggestion);
		            break;
	            default:
		            if (fullMessage.GetType() != typeof(BaseIdemMessage))
			            Debug.LogError($"Unsupported message type: {fullMessage.GetType()}");
		            break;
            }
        }

        private GameModeContainer GetGameMode(string gameMode) => gameModes.GetValueOrDefault(gameMode);

        private CachedMatch FindMatch(string playerId, string matchId)
        {
	        var allMatches =
		        gameModes.Values.SelectMany(g => g.pendingMatches.Values)
			        .Concat(
				        gameModes.Values.SelectMany(g => g.activeMatches.Values)
			        );
	        
	        return allMatches.FirstOrDefault(match =>
		        match.matchId == matchId && match.players.Any(p => p.playerId == playerId)
	        );
        }

        private void HandleMatchSuggestion(string payloadGameId, Match match)
        {
	        // TODO_IDEM
	        throw new NotImplementedException();
        }

        private void OnAddPlayerResponse(AddPlayerResponseMessage addPlayerResponse)
        {
	        var gameContainer = GetGameMode(addPlayerResponse.payload.gameId);
	        if (gameContainer == null || addPlayerResponse.payload.players == null)
		        return;

	        foreach (var player in addPlayerResponse.payload.players)
	        {
		        gameContainer.waitingPlayers.Add(player.playerId);
	        }

	        SignalAwaiters(addPlayerResponse);
        }

        private void OnRemovePlayerResponse(RemovePlayerResponseMessage removePlayerResponse)
        {
	        var gameContainer = GetGameMode(removePlayerResponse.payload.gameId);
	        if (gameContainer == null || removePlayerResponse.payload.playerId == null)
		        return;

	        gameContainer.waitingPlayers.Remove(removePlayerResponse.payload.playerId);
	        SignalAwaiters(removePlayerResponse);
        }

        private void OnGetPlayersResponse(GetPlayersResponseMessage getPlayersResponse)
        {
	        if (debug)
				Debug.Log($"Got players:\n" +
						  string.Join("\n",
							  getPlayersResponse.payload.players
								  .Select(p => $"[{p.status}] {p.playerId}")
						  )
				);
	        SignalAwaiters(getPlayersResponse);
        }

        private void OnGetMatchesResponse(GetMatchesResponseMessage getMatchesResponse)
        {
	        SignalAwaiters(getMatchesResponse);
	        
	        if (getMatchesResponse?.payload?.matches == null)
		        return;
	        
	        foreach (var match in getMatchesResponse.payload.matches)
	        {
		        HandleMatchSuggestion(getMatchesResponse.payload.gameId, match);
	        }
        }

        private void OnConfirmMatchResponse(ConfirmMatchResponseMessage confirmMatchResponse)
        {
	        SignalAwaiters(confirmMatchResponse);
        }

        private void OnFailMatchResponse(FailMatchResponseMessage failMatchResponse)
        {
	        SignalAwaiters(failMatchResponse);
        }

        private void OnCompleteMatchResponse(CompleteMatchResponseMessage completeMatchResponse)
        {
	        SignalAwaiters(completeMatchResponse);
        }

        private void OnMatchSuggestion(MatchSuggestionMessage matchSuggestion)
        {
	        SignalAwaiters(matchSuggestion);

	        if (matchSuggestion?.payload?.match == null)
		        return;
	        
	        HandleMatchSuggestion(matchSuggestion.payload.gameId, matchSuggestion.payload.match);
        }
        
        private void AddAwaiter<T>(TaskCompletionSource<BaseIdemMessage> awaiter) where T : BaseIdemMessage
		{
	        var type = typeof(T);
	        if (!incomingAwaiters.TryGetValue(type, out var list))
	        {
		        list = new List<TaskCompletionSource<BaseIdemMessage>>();
		        incomingAwaiters[type] = list;
	        }
	        list.Add(awaiter);
		}

        private void SignalAwaiters(BaseIdemMessage message)
        {
	        var type = message.GetType();
	        if (!incomingAwaiters.TryGetValue(type, out var list) || list == null)
		        return;

	        foreach (var awaiter in list)
	        {
		        awaiter.SetResult(message);
	        }
	        list.Clear();
        }
    }
}