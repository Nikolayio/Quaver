﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Quaver.Beatmaps;
using Quaver.Discord;
using Quaver.Main;
using Quaver.QuaFile;

namespace Quaver.GameState.States
{
    internal class MapImportLoadingState : IGameState
    {
        public State CurrentState { get; set; } = State.LoadingScreen;

        public void Initialize()
        {
            // TODO: Add some sort of general loading screen here. The state is only going to be used during map importing.
            // Set Rich Presence
            GameBase.DiscordController.presence.details = $"Importing Charts";
            DiscordRPC.UpdatePresence(ref GameBase.DiscordController.presence);
        }

        public void LoadContent() { }

        public void UnloadContent() { }

        public void Update(GameTime gameTime) { }

        public void Draw()
        {
            GameBase.GraphicsDevice.Clear(Color.Red);
        }
    }
}