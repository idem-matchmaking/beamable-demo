using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Beamable.Microservices.Idem.Shared;
using Beamable.Microservices.Idem.Shared.MicroserviceSchema;
using UnityEngine;
using Object = System.Object;

namespace Beamable.Microservices.Idem.IdemLogic
{
    public class IdemLogic
    {
	    private const int MaxRecentPlayers = 1000;
	    
        private readonly bool debug;
        private readonly TimeSpan playerTimeoutMs;
        private readonly Dictionary<string, GameModeContainer> gameModes = new ();
        private readonly Func<object, bool> sendToIdem;
        private readonly Dictionary<Type, List<TaskCompletionSource<BaseIdemMessage>>> incomingAwaiters = new ();
        private readonly Queue<PlayerFullStats> recentPlayers = new ();
        private readonly Timer secondsTimer = new(1000);

        public IdemLogic(bool debug, int playerTimeoutMs, Func<object, bool> sendToIdem)
        {
            this.debug = debug;
            this.playerTimeoutMs = TimeSpan.FromMilliseconds(playerTimeoutMs);
            this.sendToIdem = sendToIdem;

            secondsTimer.Elapsed += OnEverySecond;
            secondsTimer.AutoReset = true;
            secondsTimer.Enabled = true;
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

        public BaseResponse GetRecentPlayer(string playerId)
        {
	        foreach (var p in recentPlayers)
		        if (p.playerId == playerId)
					return new StringResponse(p.ToJson());
	        
	        return BaseResponse.UnknownPlayerFailure;
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
		        if (gameContainer.waitingPlayers.ContainsKey(playerId))
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
		        if (gameContainer.waitingPlayers.TryGetValue(playerId, out var waitingPlayer))
		        {
			        if (waitingPlayer.isInactive)
			        {
				        // there is a removal request already sent to Idem due to inactivity
				        return MMStateResponse.Timeout();
			        }
			        
			        waitingPlayer.lastSeen = DateTime.Now;
			        return MMStateResponse.InQueue(gameId);
		        }

		        if (gameContainer.pendingMatches.TryGetValue(playerId, out var pendingMatch))
			        return MMStateResponse.MatchFound(gameId, pendingMatch.matchId, pendingMatch.players);
		        if (gameContainer.activeMatches.TryGetValue(playerId, out var activeMatch))
			        return MMStateResponse.MatchFound(gameId, activeMatch.matchId, activeMatch.players);
	        }
	        
	        return MMStateResponse.None();
        }

        public BaseResponse ConfirmMatch(string playerId, string matchId)
        {
	        var match = FindAnyMatch(playerId, matchId);
	        if (match == null)
		        return BaseResponse.UnknownMatchFailure;

	        match.ConfirmBy(playerId);
	        if (match.ConfirmedByAll && sendToIdem(new ConfirmMatchMessage(match.gameId, match.matchId)))
				return ConfirmMatchResponse.MatchConfirmed;

	        return ConfirmMatchResponse.MatchNotConfirmed;
        }

        public BaseResponse CompleteMatch(string playerId, string matchId, IdemMatchResult result)
        {
	        var match = FindAnyMatch(playerId, matchId);
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

        private CachedMatch FindAnyMatch(string playerId, string matchId)
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

        private CachedMatch FindPendingMatch(string matchId, out GameModeContainer outGameMode)
        {
	        foreach (var gameMode in gameModes.Values)
	        foreach (var match in gameMode.pendingMatches.Values)
	        {
		        if (match.matchId == matchId)
		        {
			        outGameMode = gameMode;
			        return match;
		        }
	        }

	        outGameMode = null;
	        return null;
        }

        private CachedMatch FindActiveMatch(string matchId, out GameModeContainer outGameMode)
        {
	        foreach (var gameMode in gameModes.Values)
	        foreach (var match in gameMode.activeMatches.Values)
	        {
		        if (match.matchId == matchId)
		        {
			        outGameMode = gameMode;
			        return match;
		        }
	        }

	        outGameMode = null;
	        return null;
        }

        private void HandleMatchSuggestion(string gameId, Match match)
        {
	        var gameContainer = GetGameMode(gameId);
	        if (gameContainer == null)
	        {
		        Debug.LogError($"Got match {match.uuid} suggested for unsupported game mode {gameId}");
		        return;
	        }

	        var cachedMatch = new CachedMatch(gameId, match);
	        for (var i = 0; i < cachedMatch.players.Length; i++)
	        {
		        var p = cachedMatch.players[i];
		        gameContainer.pendingMatches[p.playerId] = cachedMatch;
		        if (gameContainer.waitingPlayers.TryGetValue(p.playerId, out var waitingPlayer))
		        {
			        cachedMatch.lastSeen[i] = waitingPlayer.lastSeen;
			        gameContainer.waitingPlayers.Remove(p.playerId);
		        }
	        }
        }

        private void OnAddPlayerResponse(AddPlayerResponseMessage addPlayerResponse)
        {
	        var gameContainer = GetGameMode(addPlayerResponse.payload.gameId);
	        if (gameContainer == null || addPlayerResponse.payload.players == null)
		        return;

	        foreach (var player in addPlayerResponse.payload.players)
	        {
		        gameContainer.waitingPlayers[player.playerId] = new WaitingPlayer();
	        }

	        SignalAwaiters(addPlayerResponse);
        }

        private void OnRemovePlayerResponse(RemovePlayerResponseMessage removePlayerResponse)
        {
	        var gameContainer = GetGameMode(removePlayerResponse.payload.gameId);
	        if (gameContainer == null || removePlayerResponse.payload.playerId == null)
		        return;

	        gameContainer.waitingPlayers.Remove(new (removePlayerResponse.payload.playerId));
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

	        var match = FindPendingMatch(confirmMatchResponse.payload.matchId, out var gameMode);
	        if (match == null)
	        {
		        Debug.LogWarning($"Got confirmation for unknown match {confirmMatchResponse.payload.matchId}");
		        return;
	        }
	        if (match.isActive)
	        {
		        Debug.LogWarning($"Got confirmation for active match {confirmMatchResponse.payload.matchId}");
	        }

	        match.isActive = true;
	        
	        foreach (var p in match.players)
	        {
		        gameMode.pendingMatches.Remove(p.playerId);
		        gameMode.activeMatches[p.playerId] = match;
	        }
        }

        private void OnFailMatchResponse(FailMatchResponseMessage failMatchResponse)
        {
	        SignalAwaiters(failMatchResponse);
	        
	        // TODO_IDEM
        }

        private void OnCompleteMatchResponse(CompleteMatchResponseMessage completeMatchResponse)
        {
	        SignalAwaiters(completeMatchResponse);

	        var match = FindActiveMatch(completeMatchResponse.payload.matchId, out var gameMode);
	        if (match == null)
	        {
		        Debug.LogWarning($"Got completion for unknown match {completeMatchResponse.payload.matchId}");
		        return;
	        }
	        if (match.isCompleted)
	        {
		        Debug.LogWarning($"Got completion for completed match {completeMatchResponse.payload.matchId}");
	        }

	        match.isCompleted = true;

	        foreach (var p in match.players)
	        {
		        gameMode.activeMatches.Remove(p.playerId);
	        }
	        
	        foreach (var fullPlayerStats in completeMatchResponse.payload.players)
				recentPlayers.Enqueue(fullPlayerStats);

	        while (recentPlayers.Count > MaxRecentPlayers)
		        recentPlayers.Dequeue();
        }

        private void OnMatchSuggestion(MatchSuggestionMessage matchSuggestion)
        {
	        SignalAwaiters(matchSuggestion);

	        if (matchSuggestion?.payload?.match == null)
		        return;
	        
	        HandleMatchSuggestion(matchSuggestion.payload.gameId, matchSuggestion.payload.match);
        }

        private void OnEverySecond(Object source, ElapsedEventArgs elapsedArgs)
        {
	        try
	        {
				TimeoutPlayers();
				TimoutPendingMatches();
	        }
	        catch (Exception e)
	        {
		        Debug.LogError($"Timeout handling exception: {e}");
	        }
        }

        private void TimeoutPlayers()
        {
	        var toRemove = new List<(string gameId, string playerId)>();
			var now = DateTime.Now;
	        foreach (var (gameId, gameModeContainer) in gameModes)
			foreach (var (playerId, waitingPlayer) in gameModeContainer.waitingPlayers)
			{
				if (now - waitingPlayer.lastSeen > playerTimeoutMs)
				{
					waitingPlayer.isInactive = true;
					toRemove.Add((gameId, playerId));
				}
			}

	        foreach (var (gameId, playerId) in toRemove)
	        {
		        sendToIdem(new RemovePlayerMessage(gameId, playerId));
	        }
        }
        
        private void TimoutPendingMatches()
        {
	        var toRemove = new Dictionary<CachedMatch, List<string>>();
			var now = DateTime.Now;
	        foreach (var gameModeContainer in gameModes.Values)
	        {
		        toRemove.Clear();
		        foreach (var match in gameModeContainer.pendingMatches.Values)
		        {
			        for (var i = 0; i < match.players.Length; i++)
			        {
				        var p = match.players[i];
				        var lastSeen = match.lastSeen[i];
				        if (now - lastSeen <= playerTimeoutMs)
					        continue;
				        
				        if (!toRemove.TryGetValue(match, out var list))
				        {
					        list = new List<string>();
					        toRemove[match] = list;
				        }

				        list.Add(p.playerId);
			        }
		        }
	        }

	        foreach (var (match, timeoutedList) in toRemove)
	        {
		        var failMatchMessage = new FailMatchMessage(
			        match.gameId,
			        match.matchId,
			        timeoutedList.ToArray(),
			        match.players
				        .Where(p => !timeoutedList.Contains(p.playerId))
				        .Select(p => p.playerId)
				        .ToArray()
		        );
		        sendToIdem(failMatchMessage);
	        }
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