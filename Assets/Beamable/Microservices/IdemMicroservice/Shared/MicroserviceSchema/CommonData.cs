namespace Beamable.Microservices.Idem.Shared.MicroserviceSchema
{
    public class IdemPlayer
    {
        public readonly int teamId;
        public readonly string playerId;

        public IdemPlayer(int teamId, string playerId)
        {
            this.teamId = teamId;
            this.playerId = playerId;
        }
    }
    
    public class IdemMatchResult
    {
        public string gameId;
        public string matchId;
        public string server;
        public float gameLength;
        public IdemTeamResult[] teams;
    }

    public class IdemTeamResult
    {
        public int rank;
        public IdemPlayerResult[] players;
    }

    public class IdemPlayerResult
    {
        public string playerId;
        public float score;
    }
}