using System.Collections;
using Beamable;
using Beamable.Microservices.Idem.Shared;
using Beamable.Microservices.Idem.Shared.MicroserviceSchema;
using Beamable.Server.Clients;
using UnityEngine;

namespace DefaultNamespace
{
    public class DirectIdemMicroserviceTest : MonoBehaviour
    {
        private string playersText = "";
        private string matchesText = "";
        private string fullPlayerStatsText = "";
        private string gameModeText = "1v1";
        private string serversText = "mainServer\nsecondServer";
        private string playerId = "";
        private string matchId = "";
        private string mmStatus = "";
        private string matchResultJson = "";
        private bool isBeamableReady = false;

        private IdemMicroserviceClient idemClient => BeamContext.Default.Microservices().IdemMicroservice();

        private void OnEnable()
        {
            InitBeamable();
            StartCoroutine(UpdateStatus());
            matchResultJson = CreateDummyMatchResult().ToJson();
        }

        private IEnumerator UpdateStatus()
        {
            while (true)
            {
                UpdateMMStatus();
                yield return new WaitForSeconds(2f);
            }
        }

        private void OnGUI()
        {
            var columnWidth = 200;
            if (!isBeamableReady)
            {
                GUI.Label(new Rect(100, 50, columnWidth, 30), "Beamable is not ready");
                return;
            }

            // left column
            var leftColumnX = 100;
            GUI.Label(new Rect(leftColumnX, 50, columnWidth, 30), "Game mode");
            gameModeText = GUI.TextField(new Rect(leftColumnX, 100, columnWidth, 30), gameModeText);
            
            GUI.Label(new Rect(leftColumnX, 150, columnWidth, 30), "Servers list");
            serversText = GUI.TextArea(new Rect(leftColumnX, 200, columnWidth, 80), serversText);
            
            if (GUI.Button(new Rect(leftColumnX, 300, columnWidth, 30), "Enter queue"))
            {
                idemClient.StartMatchmaking(gameModeText, serversText.Split("\n"));
            }
            
            if (GUI.Button(new Rect(leftColumnX, 350, columnWidth, 30), "Leave queue"))
            {
                idemClient.StopMatchmaking();
            }
            
            GUI.Label(new Rect(leftColumnX, 400, columnWidth, 30), "MM Status");
            GUI.TextArea(new Rect(leftColumnX, 450, columnWidth, 100), mmStatus);

            GUI.Label(new Rect(leftColumnX, 550, columnWidth, 30), "Player id");
            GUI.TextField(new Rect(leftColumnX, 600, columnWidth, 30), playerId);
            
            // central column
            var centralColumnX = 450;
            GUI.Label(new Rect(centralColumnX, 150, columnWidth, 30), "Match id");
            matchId = GUI.TextArea(new Rect(centralColumnX, 200, columnWidth, 80), matchId);
            
            if (GUI.Button(new Rect(centralColumnX, 300, columnWidth, 30), "Confirm match"))
            {
                idemClient.ConfirmMatch(matchId);
            }
            
            GUI.Label(new Rect(centralColumnX, 350, columnWidth, 30), "Match result");
            matchResultJson = GUI.TextArea(new Rect(centralColumnX, 400, columnWidth, 130), matchResultJson);
            if (GUI.Button(new Rect(centralColumnX, 550, columnWidth, 30), "Complete match"))
            {
                idemClient.CompleteMatch(matchResultJson);
            }
            
            // right column
            var rightColumnX = 800;
            GUI.Label(new Rect(rightColumnX, 50, columnWidth, 30), "Players");
            GUI.TextArea(new Rect(rightColumnX, 100, columnWidth, 130), playersText);
            if (GUI.Button(new Rect(rightColumnX, 250, columnWidth, 30), "Update players"))
            {
                UpdatePlayers();
            }
            
            GUI.Label(new Rect(rightColumnX, 300, columnWidth, 30), "Matches");
            GUI.TextArea(new Rect(rightColumnX, 350, columnWidth, 130), matchesText);
            if (GUI.Button(new Rect(rightColumnX, 500, columnWidth, 30), "Update matches"))
            {
                UpdateMatches();
            }
            
            GUI.Label(new Rect(rightColumnX, 550, columnWidth, 30), "Full player stats");
            GUI.TextArea(new Rect(rightColumnX, 600, columnWidth, 130), fullPlayerStatsText);
            if (GUI.Button(new Rect(rightColumnX, 750, columnWidth, 30), "Get player stats"))
            {
                GetFullPlayerStats();
            }
        }

        private async void InitBeamable()
        {
            var ctx = BeamContext.Default;
            await ctx.OnReady;
            playerId = ctx.AuthorizedUser.IsAssigned ? ctx.AuthorizedUser.Value.id.ToString() : "not assigned";
            isBeamableReady = true;
            
            Debug.Log($"Beamable is ready, player id is {playerId}");
        }

        private async void UpdateMMStatus()
        {
            await BeamContext.Default.OnReady;
            mmStatus = await idemClient.GetMatchmakingStatus();
            Debug.Log($"Got fresh MM status: {mmStatus}");
            
            if (JsonUtil.TryParse<MMStateResponse>(mmStatus, out var parsed) &&
                !string.IsNullOrWhiteSpace(parsed.matchId))
            {
                UpdateMatchData(parsed);
            }
        }

        private async void UpdatePlayers()
        {
            var players = await idemClient.DebugGetPlayers(gameModeText);
            playersText = ParseResponse(players);
            Debug.Log($"Got response {players}");
        }
        
        private async void UpdateMatches()
        {
            var matches = await idemClient.DebugGetMatches(gameModeText);
            matchesText = ParseResponse(matches);
        }
        
        private async void GetFullPlayerStats()
        {
            var matches = await idemClient.DebugGetRecentPlayer(playerId);
            fullPlayerStatsText = ParseResponse(matches);
        }

        private string ParseResponse(string response)
        {
            var parsed = JsonUtil.Parse<StringResponse>(response);
            return parsed?.value ?? response;
        }

        private void UpdateMatchData(MMStateResponse parsed)
        {
            matchId = parsed.matchId;
            gameModeText = parsed.gameMode;
            matchResultJson = CreateDummyMatchResult(matchId, parsed.players[0].playerId, parsed.players[1].playerId)
                .ToJson();
        }

        private object CreateDummyMatchResult(string matchId = "ENTER_MATCH_ID", string playerId1 = "ENTER_PLAYER_ID", string playerId2 = "ENTER_PLAYER_ID")
            => new IdemMatchResult
            {
                server = "mainServer",
                gameId = gameModeText,
                matchId = matchId,
                gameLength = 100,
                teams = new[]
                {
                    new IdemTeamResult
                    {
                        rank = 1,
                        players = new[]
                        {
                            new IdemPlayerResult
                            {
                                playerId = playerId1,
                                score = 100
                            },
                        }
                    },
                    new IdemTeamResult
                    {
                        rank = 2,
                        players = new[]
                        {
                            new IdemPlayerResult
                            {
                                playerId = playerId2,
                                score = 50
                            },
                        }
                    }
                }
            };
    }
}