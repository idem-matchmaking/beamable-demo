using System.Collections.Generic;
using System.Linq;

namespace Beamable.Microservices.Idem.IdemLogic
{
    public class BaseIdemMessage
    {
        public string action;
        public string messageId;
        public IdemError error;

        public static BaseIdemMessage ParseByAction(BaseIdemMessage header, string fullJson)
            => header?.action switch
            {
                "addPlayerResponse" => CompactJson.Serializer.Parse<AddPlayerResponseMessage>(fullJson),
                "removePlayerResponse" => CompactJson.Serializer.Parse<RemovePlayerResponseMessage>(fullJson),
                "getPlayersResponse" => CompactJson.Serializer.Parse<GetPlayersResponseMessage>(fullJson),
                "getMatchesResponse" => CompactJson.Serializer.Parse<GetMatchesResponseMessage>(fullJson),
                "updateMatchConfirmedResponse" => CompactJson.Serializer.Parse<ConfirmMatchResponseMessage>(fullJson),
                "updateMatchFailedResponse" => CompactJson.Serializer.Parse<FailMatchResponseMessage>(fullJson),
                "updateMatchCompletedResponse" => CompactJson.Serializer.Parse<CompleteMatchResponseMessage>(fullJson),
                "matchSuggestion" => CompactJson.Serializer.Parse<MatchSuggestionMessage>(fullJson),
                "keepAlive" => header,
                _ => null
            };
    }

    public class IdemError
    {
        public int code;
        public string message;
    }

    public class AddPlayerMessage : BaseIdemMessage
    {
        public AddPlayerPayload payload;

        public AddPlayerMessage()
        {
            action = "addPlayer";
        }
        
        public AddPlayerMessage(string gameId, string playerId, string[] servers) : this()
        {
            payload = new AddPlayerPayload
            {
                gameId = gameId,
                players = new []
                {
                    new AddPlayerPlayer
                    {
                        playerId = playerId,
                        servers = servers
                    }
                }
            };
        }
    }

    public class AddPlayerResponseMessage : BaseIdemMessage
    {
        public AddPlayerResponsePayload payload;
    }

    public class RemovePlayerMessage : BaseIdemMessage
    {
        public RemovePlayerPayload payload;
        
        public RemovePlayerMessage()
        {
            action = "removePlayer";
        }

        public RemovePlayerMessage(string gameId, string playerId) : this()
        {
            payload = new RemovePlayerPayload
            {
                gameId = gameId,
                playerId = playerId
            };
        }
    }

    public class RemovePlayerResponseMessage : BaseIdemMessage
    {
        public RemovePlayerResponsePayload payload;
    }

    public class GetPlayersMessage : BaseIdemMessage
    {
        public GameIdPayload payload;
        
        public GetPlayersMessage()
        {
            action = "getPlayers";
        }

        public GetPlayersMessage(string gameId) : this()
        {
            payload = new GameIdPayload
            {
                gameId = gameId
            };
        }
    }
    
    public class GetPlayersResponseMessage : BaseIdemMessage
    {
        public GetPlayersResponsePayload payload;
    }

    public class GetMatchesMessage : BaseIdemMessage
    {
        public GameIdPayload payload;
        
        public GetMatchesMessage()
        {
            action = "getMatches";
        }
    }
    
    public class GetMatchesResponseMessage : BaseIdemMessage
    {
        public GetMatchesResponsePayload payload;
    }

    public class ConfirmMatchMessage : BaseIdemMessage
    {
        public MatchIdPayload payload;
        
        public ConfirmMatchMessage()
        {
            action = "updateMatchConfirmed";
        }
    }
    
    public class ConfirmMatchResponseMessage : BaseIdemMessage
    {
        public MatchIdPayload payload;
    }

    public class FailMatchMessage : BaseIdemMessage
    {
        public FailMatchPayload payload;
        
        public FailMatchMessage()
        {
            action = "updateMatchFailed";
        }

        public FailMatchMessage(string gameId, string matchId, string failedPlayerId, IEnumerable<string> allPlayers) : this()
        {
            payload = new FailMatchPayload
            {
                gameId = gameId,
                matchId = matchId,
                remove = new [] { failedPlayerId },
                requeue = allPlayers.Where(p => p != failedPlayerId).ToArray()
            };
        }
    }
    
    public class FailMatchResponseMessage : BaseIdemMessage
    {
        public MatchIdPayload payload;
    }

    public class CompleteMatchMessage : BaseIdemMessage
    {
        public CompleteMatchPayload payload;
        
        public CompleteMatchMessage()
        {
            action = "updateMatchCompleted";
        }
    }

    public class CompleteMatchResponseMessage : BaseIdemMessage
    {
        public CompleteMatchReponsePayload payload;
    }
    
    public class MatchSuggestionMessage : BaseIdemMessage
    {
        public MatchSuggestionPayload payload;
    }

    public class AddPlayerPayload
    {
        public string gameId;
        public string partyName;
        public AddPlayerPlayer[] players;
    }
    
    public class AddPlayerResponsePayload
    {
        public string gameId;
        public Player[] players;
    }

    public class RemovePlayerPayload
    {
        public string gameId;
        public string playerId;
    }

    public class RemovePlayerResponsePayload
    {
        public string gameId;
        public string playerId;
        public string reference;
    }
    
    public class GameIdPayload
    {
        public string gameId;
    }
    
    public class GetPlayersResponsePayload
    {
        public string gameId;
        public PlayerStatus[] players;
    }
    
    public class GetMatchesResponsePayload
    {
        public string gameId;
        public Match[] matches;
    }
    
    public class MatchIdPayload
    {
        public string gameId;
        public string matchId;
    }

    public class CompleteMatchPayload : MatchIdPayload
    {
        public string server;
        public float gameLength;
        public TeamResult[] teams;
    }
    
    public class FailMatchPayload : MatchIdPayload
    {
        public string[] remove;
        public string[] requeue;
    }

    public class CompleteMatchReponsePayload : MatchIdPayload
    {
        public PlayerFullStats[] players;
    }

    public class MatchSuggestionPayload
    {
        public string gameId;
        public Match match;
    }

    public class AddPlayerPlayer : Player
    {
        public string[] servers;
    }

    public class TeamResult
    {
        public int rank;
        public PlayerResult[] players;
    }

    public class PlayerResult
    {
        public string playerId;
        public float score;
    }

    public class Match
    {
        public string uuid;
        public Team[] teams;
    }

    public class Team
    {
        public Player[] players;
    }
    
    public class Player
    {
        public string playerId;
        public string reference;
    }

    public class PlayerStatus : Player
    {
        public string status;
    }

    public class PlayerFullStats : Player
    {
        public int totalWins;
        public int totalLosses;
        public int totalMatchesPlayed;
        public string season;
        public int seasonWins;
        public int seasonLosses;
        public int seasonMatchesPlayed;
        public float rating;
        public float ratingUncertainty;
        public float rankingPoints;
        public float ratingDeltaLastGame;
        public float rankingDeltaLastGame;
        public int wins;
        public int losses;
        public int matchesPlayed;
        public float winRatio;
    }
}