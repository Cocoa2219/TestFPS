using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommandSystem;
using Exiled.API.Features;

namespace TestFPS.Commands;

[CommandHandler(typeof(ClientCommandHandler))]
public class PinPoint : ICommand
{
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, [UnscopedRef] out string response)
    {
        var senderPlayer = Player.Get((CommandSender)sender);

        if (arguments.Count < 1)
        {
            response = "Usage: pinpoint <player>";
            return false;
        }

        var player = Player.Get(arguments.At(0));

        if (player == null)
        {
            response = "Player not found.";
            return false;
        }

        if (TestFPS.Instance._eventHandler.pinPointedPlayers.ContainsKey(senderPlayer))
        {
            TestFPS.Instance._eventHandler.pinPointedPlayers[senderPlayer].Add(player);
        }
        else
        {
            TestFPS.Instance._eventHandler.pinPointedPlayers.Add(senderPlayer, [player]);
        }

        response = $"Pinpointed {player.Nickname}.";
        return true;
    }

    public string Command { get; } = "pinpoint";
    public string[] Aliases { get; } = ["pp"];
    public string Description { get; } = "Pinpoints a player.";
}