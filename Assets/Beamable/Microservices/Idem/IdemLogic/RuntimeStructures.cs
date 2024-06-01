using System.Collections.Generic;

namespace Beamable.Microservices.Idem.IdemLogic
{
	    internal class GameModeContainer
	    {
		    /**
		     * Players with AddPlayer sent but no match suggestions received
		     */
			public readonly HashSet<string> waitingPlayers = new();
		    
		    /**
		     * Players with match suggestions received but not confirmed
		     */
			public readonly Dictionary<string, Match> pendingMatches = new ();
		    
		    /**
		     * Players with confirmed matches without completion
		     */
			public readonly Dictionary<string, Match> activeMatches = new ();
		    // TODO clear active matches after big timeout (like a day) to avoid memory leak
	    }

	    internal class CachedMatch
	    {
		    public readonly string matchId;
		    
	    }
}