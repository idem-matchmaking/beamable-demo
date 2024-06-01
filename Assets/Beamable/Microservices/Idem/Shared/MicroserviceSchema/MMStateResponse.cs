using System.Collections.Generic;
using System.Linq;

namespace Beamable.Microservices.Idem.Shared.MicroserviceSchema
{
    public class MMStateResponse : BaseResponse
    {
        public bool inQueue;
        public bool matchFound;
        public string gameMode;
        public string matchId;
        public IdemPlayer[] players;


        public MMStateResponse(bool inQueue, bool matchFound, string gameMode = "", string matchId = "", IdemPlayer[] players = null) : base(true)
        {
            this.inQueue = inQueue;
            this.matchFound = matchFound;
            this.gameMode = gameMode;
            this.matchId = matchId;
            this.players = players;
        }

        public static MMStateResponse None() => new(false, false);
        public static MMStateResponse InQueue(string gameMode) => new(true, false, gameMode);

        public static MMStateResponse MatchFound(string gameMode, string matchId, IEnumerable<IdemPlayer> players)
            => new(false, true, gameMode, matchId, players.ToArray());
    }
}