using System;
using System.Diagnostics.CodeAnalysis;
using CommandSystem;
using Exiled.API.Features;

namespace TestFPS.Commands;

[CommandHandler(typeof(ClientCommandHandler))]
public class FixCamera : ICommand
{
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, [UnscopedRef] out string response)
    {
        var senderPlayer = Player.Get((CommandSender)sender);

        if (senderPlayer is null)
        {
            response = "You must be a player to use this command.";
            return false;
        }

        response = "Camera fixed.";
        return true;
    }

    public string Command { get; } = "fixcamera";
    public string[] Aliases { get; } = ["fc"];
    public string Description { get; } = "Fixes the camera.";
}