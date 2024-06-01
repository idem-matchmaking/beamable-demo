using System.Collections.Generic;
using System.Linq;
using Beamable.Microservices.Idem.Shared;
using Beamable.Microservices.Idem.Shared.MicroserviceSchema;

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
		public readonly Dictionary<string, CachedMatch> pendingMatches = new();

		/**
		 * Players with confirmed matches without completion
		 */
		public readonly Dictionary<string, CachedMatch> activeMatches = new();
		// TODO_IDEM clear active matches after big timeout (like a day) to avoid memory leak
	}

	internal class CachedMatch
	{
		public readonly string gameId;
		public readonly string matchId;
		public readonly IdemPlayer[] players;
		public readonly bool[] confirmed;
		public bool isActive;

		public CachedMatch(string gameId, Match match)
		{
			this.gameId = gameId;
			matchId = match.uuid;
			
			var players = new List<IdemPlayer>();
			for (int i = 0; i < match.teams.Length; i++)
			{
				foreach (var p in match.teams[i].players)
				{
					players.Add(new IdemPlayer(i, p.playerId));
				}
			}
			
			this.players = players.ToArray();
			this.confirmed = new bool[this.players.Length];
		}

		public bool ConfirmedByAll => confirmed.All(t => t);

		public void ConfirmBy(string playerId)
		{
			for (int i = 0; i < players.Length; i++)
			{
				if (players[i].playerId == playerId)
				{
					confirmed[i] = true;
				}
			}
		}
	}
}