using System.Collections.Generic;
using System.Linq;

namespace Beamable.Microservices.MicroserviceSchemas
{
    public class MMStateResponse : BaseResponse
    {
        public bool inQueue;
        public bool matchFound;
        public string gameMode;
        public string matchId;
        public Player[] players;

        public class Player
        {
            public int teamId;
            public string playerId;

            public Player(int teamId, string playerId)
            {
                this.teamId = teamId;
                this.playerId = playerId;
            }
        }

        public MMStateResponse(bool inQueue, bool matchFound, string gameMode = "", string matchId = "", Player[] players = null) : base(true)
        {
            this.inQueue = inQueue;
            this.matchFound = matchFound;
            this.gameMode = gameMode;
            this.matchId = matchId;
            this.players = players;
        }

        public static MMStateResponse None() => new(false, false);
        public static MMStateResponse InQueue(string gameMode) => new(true, false, gameMode);

        public static MMStateResponse MatchFound(string gameMode, string matchId, IEnumerable<Player> players)
            => new(false, true, gameMode, matchId, players.ToArray());
    }
}