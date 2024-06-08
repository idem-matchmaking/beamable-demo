using System.Collections.Generic;
using System.Linq;

namespace Beamable.Microservices.Idem.Shared.MicroserviceSchema
{
    public class MMStateResponse : BaseResponse
    {
        public bool inQueue;
        public bool matchFound;
        public bool timeout;
        public string gameMode;
        public string matchId;
        public IdemPlayer[] players;

        public MMStateResponse()
        {
        }

        public MMStateResponse(bool inQueue, bool matchFound, bool timeout, string gameMode = "", string matchId = "", IdemPlayer[] players = null) : base(true)
        {
            this.inQueue = inQueue;
            this.matchFound = matchFound;
            this.timeout = timeout;
            this.gameMode = gameMode;
            this.matchId = matchId;
            this.players = players;
        }

        public static MMStateResponse None() => new(false, false, false);
        public static MMStateResponse InQueue(string gameMode) => new(true, false, false, gameMode);

        public static MMStateResponse MatchFound(string gameMode, string matchId, IEnumerable<IdemPlayer> players)
            => new(false, true, false, gameMode, matchId, players.ToArray());
        
        public static MMStateResponse Timeout() => new(false, false, true);
    }
}