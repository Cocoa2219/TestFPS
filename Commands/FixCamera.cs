using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using UnityEngine;

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

        var nearestPlayer = Player.List.OrderBy(player => Vector3.Distance(player.Position, senderPlayer.Position)).FirstOrDefault();

        if (nearestPlayer != null) senderPlayer.CameraTransform.LookAt(nearestPlayer.Position);

        response = "Camera fixed.";
        return true;
    }

    public string Command { get; } = "fixcamera";
    public string[] Aliases { get; } = ["fc"];
    public string Description { get; } = "Fixes the camera.";
}