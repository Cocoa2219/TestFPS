using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using CommandSystem;
using Exiled.API.Features;

namespace TestFPS.Commands;

[CommandHandler(typeof(ClientCommandHandler))]
public class ShowHint : ICommand
{
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, [UnscopedRef] out string response)
    {
        var senderPlayer = Player.Get((CommandSender)sender);

        var text = string.Join(" ", arguments);

        if (senderPlayer is null)
        {
            response = "You must be a player to use this command.";
            return false;
        }

        if (string.IsNullOrEmpty(text))
        {
            response = "Usage: showhint <text>";
            return false;
        }

        senderPlayer.ShowHint(text, 5f);
        response = "Hint shown.";
        return true;
    }

    public string Command { get; } = "showhint";
    public string[] Aliases { get; } = ["sh"];
    public string Description { get; } = "hinttest.";
}