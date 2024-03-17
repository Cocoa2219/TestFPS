﻿using System;
using Exiled.API.Features;
using Player = Exiled.Events.Handlers.Player;
using Server = Exiled.Events.Handlers.Server;

namespace TestFPS
{
    public class TestFPS : Plugin<Config>
    {
        public static TestFPS Instance { get; private set; }

        public override string Name => "TestFPS";
        public override string Author => "Cocoa";
        public override string Prefix => "TestFPS";
        public override Version Version => new(1, 0, 0);

        public EventHandler _eventHandler;

        public override void OnEnabled()
        {
            Instance = this;

            _eventHandler = new EventHandler();
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Server.RoundStarted += _eventHandler.OnRoundStarted;
            Server.RestartingRound += _eventHandler.OnServerRestarting;
            Player.ChangingItem += _eventHandler.OnChangingItem;
        }

        public override void OnDisabled()
        {
            UnregisterEvents();

            _eventHandler = null;
            Instance = null;
        }

        private void UnregisterEvents()
        {
            Server.RoundStarted -= _eventHandler.OnRoundStarted;
            Server.RestartingRound -= _eventHandler.OnServerRestarting;
            Player.ChangingItem -= _eventHandler.OnChangingItem;
        }
    }
}