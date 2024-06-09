using System.Collections.Generic;
using Beamable;
using Idem;
using UnityEngine;

public class IdemServiceTest : MonoBehaviour
{
    private const string gameMode = "1v1";
    private const string server = "mainServer";
    
    private bool isBeamableReady;
    private IdemService idemService => BeamContext.Default.IdemService();

    private void OnEnable()
    {
        InitBeamable();
    }

    private void OnGUI()
    {
        var columnWidth = 200;
        var leftColumnX = 100;
        if (!isBeamableReady || BeamContext.Default.IsStopped)
        {
            GUI.Label(new Rect(leftColumnX, 50, columnWidth, 30), "Beamable is not ready");
            return;
        }
        
        if (GUI.Button(new Rect(leftColumnX, 50, columnWidth, 30), "Enter queue"))
        {
            idemService.StartMatchmaking(gameMode, server);
        }
        
        if (GUI.Button(new Rect(leftColumnX, 100, columnWidth, 30), "Leave queue"))
        {
            idemService.StopMatchmaking();
        }
        
        if (GUI.Button(new Rect(leftColumnX, 150, columnWidth, 30), "Complete match"))
        {
            idemService.CompleteMatch(100, new Dictionary<int, int>() { { 0, 1 }, { 1, 0 } },
                new Dictionary<string, float>());
        }
        
        if (GUI.Button(new Rect(leftColumnX, 200, columnWidth, 30), "Delete save"))
        {
            PlayerPrefs.DeleteAll();
        }

        var middleColumnX = leftColumnX + columnWidth * 1.5f;
        GUI.Label(new Rect(middleColumnX, 50, columnWidth, 30), "PlayerId: " + (BeamContext.Default.AuthorizedUser.IsAssigned ? BeamContext.Default.AuthorizedUser.Value.id.ToString() : "not assigned"));
        GUI.Label(new Rect(middleColumnX, 80, columnWidth, 30), "Is matchmaking: " + idemService.IsMatchmaking);
        GUI.Label(new Rect(middleColumnX, 110, columnWidth, 30), "Match found: " + idemService.CurrentMatchInfo.HasValue);
        GUI.Label(new Rect(middleColumnX, 140, columnWidth, 30), "Match ready: " + idemService.CurrentMatchInfo?.ready);
        GUI.Label(new Rect(middleColumnX, 170, columnWidth * 2f, 30), "Match id: " + idemService.CurrentMatchInfo?.matchId);
        GUI.Label(new Rect(middleColumnX, 200, columnWidth, 30), "Server: " + idemService.CurrentMatchInfo?.server);
        GUI.Label(new Rect(middleColumnX, 230, columnWidth, 30), "Game mode: " + idemService.CurrentMatchInfo?.gameMode);
        var y = 260;
        if (idemService.CurrentMatchInfo?.players == null)
            return;
        foreach (var player in idemService.CurrentMatchInfo?.players)
        {
            GUI.Label(new Rect(middleColumnX, y, columnWidth, 30),$"Player: [{player.teamId}] {player.playerId}");
            y += 30;
        }
    }

    private async void InitBeamable()
    {
        var ctx = BeamContext.Default;
        await ctx.OnReady;
        isBeamableReady = true;
        
        var playerId = ctx.AuthorizedUser.IsAssigned ? ctx.AuthorizedUser.Value.id.ToString() : "not assigned";
        Debug.Log($"Beamable is ready, player id is {playerId}");
    }
}