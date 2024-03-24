using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using Respawning;

namespace TestFPS;

public class MultiHint
{
    public static readonly Dictionary<string, List<string>> playerHint = new();

    public static IEnumerator<float> AddPlayerHint(string playerId, int time, string text)
    {
        var player = Player.Get(playerId);
        var writtenText = string.Empty;

        if (!playerHint.ContainsKey(playerId))
            playerHint.Add(playerId, []);

        writtenText += text;
        writtenText += "\n";

        if (playerHint[playerId].Count > 0)
            for (var i = playerHint[playerId].Count - 1; i >= 0; i--)
            {
                writtenText += playerHint[playerId][i];
                writtenText += "\n";
            }

        writtenText += "<size=300>\n\n";

        playerHint[playerId].Add(text);
        player.ShowHint(writtenText, 120);
        yield return Timing.WaitForSeconds(time);
        playerHint[playerId].Remove(text);
        RefreshHint(player);
    }

    private static void RefreshHint(Player player)
    {
        var text = string.Empty;

        if (playerHint[player.UserId].Count > 0)
            for (var i = playerHint[player.UserId].Count - 1; i >= 0; i--)
            {
                text += playerHint[player.UserId][i];
                text += "\n";
            }

        text += "<size=350>\n\n";

        player.ShowHint(text, 120);
    }
}