﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Quaver.Audio;
using Quaver.Beatmaps;
using Quaver.Config;
using Quaver.Discord;
using Quaver.Gameplay;
using Quaver.GameState;
using Quaver.Graphics;
using Quaver.Input;
using Quaver.Logging;
using Quaver.Main;
using Quaver.QuaFile;
using Quaver.Utility;

namespace Quaver.GameState.States
{
    /// <summary>
    ///     This is the GameState when the player is actively playing.
    /// </summary>
    internal class PlayScreenState : IGameState
    {
        /// <summary>
        ///     The current state as defined in the enum.
        /// </summary>
        public State CurrentState { get; set; } = State.PlayScreen;

        /// <summary>
        ///     The input manager for this game state.
        /// </summary>
        private GameplayInputManager InputManager { get; } = new GameplayInputManager();

        /// <summary>
        ///     The MD5 Hash of the played beatmap.
        /// </summary>
        private string BeatmapMd5 { get; set; }

        /// <summary>
        ///     Keeps track of whether or not the song intro is current skippable.
        /// </summary>
        private bool IntroSkippable { get; set; }

        /// <summary>
        ///     Ctor, data passed in from loading state
        /// </summary>
        /// <param name="qua"></param>
        /// <param name="beatmapMd5"></param>
        public PlayScreenState(string beatmapMd5)
        {
            BeatmapMd5 = beatmapMd5;
        }

        /// <summary>
        ///     TODO: Add Summary
        /// </summary>
        public void Initialize()
        {
            Console.WriteLine($"[GAMEPLAY STATE] Initialized Gameplay State with Mods: { String.Join(", ", GameBase.CurrentGameModifiers.Select(x => x.ModIdentifier)) }");
            Console.WriteLine($"[GAMEPLAY STATE] Loaded Beatmap: {GameBase.SelectedBeatmap.Artist} - {GameBase.SelectedBeatmap.Title} [{GameBase.SelectedBeatmap.DifficultyName}]");
            Console.WriteLine($"[GAMEPLAY STATE] Loaded Beatmap MD5: {BeatmapMd5}");
            Console.WriteLine($"[GAMEPLAY STATE] Beatmap has Key Count: {GameBase.SelectedBeatmap.Qua.KeyCount}");

            GameBase.DiscordController.presence.details = $"Playing: {GameBase.SelectedBeatmap.Artist} - {GameBase.SelectedBeatmap.Title} ({GameBase.SelectedBeatmap.DifficultyName})";
            DiscordRPC.UpdatePresence(ref GameBase.DiscordController.presence);

            //Create loggers
            LogManager.AddLogger("KeyCount", Color.Pink);
            LogManager.AddLogger("DeltaTime", Color.LawnGreen);
            LogManager.AddLogger("SongTime", Color.White);
            LogManager.AddLogger("SongPos", Color.White);
            LogManager.AddLogger("HitObjects", Color.Wheat);
            LogManager.AddLogger("Skippable", CustomColors.NameTagAdmin);
        }

        /// <summary>
        ///     TODO: Add Summary
        /// </summary>
        public void LoadContent()
        {
            //Initialize Components
            Playfield.InitializePlayfield();
            Timing.InitializeTiming(GameBase.SelectedBeatmap.Qua);
            NoteRendering.InitializeNotes(GameBase.SelectedBeatmap.Qua);
        }

        /// <summary>
        ///     TODO: Add Summary
        /// </summary>
        public void UnloadContent()
        {
            //Do unload stuff
            //GameStateManager.Instance.UnloadContent();
        }

        /// <summary>
        ///     TODO: Add Summary
        /// </summary>
        public void Update(GameTime gameTime)
        {
            // Get the current game time in milliseconds.
            var dt = gameTime.ElapsedGameTime.TotalMilliseconds;

            // Set the current song time.
            Timing.SetCurrentSongTime(dt);

            // Check if the song is currently skippable.
            IntroSkippable = (GameBase.SelectedBeatmap.Qua.HitObjects[0].StartTime - Timing.CurrentSongTime >= 5000);

            // Update the playfield
            Playfield.Update(dt); ;

            // Update the Notes
            NoteRendering.Update(dt);

            // Check the input for this particular game state.
            InputManager.CheckInput(GameBase.SelectedBeatmap.Qua, IntroSkippable);

            // Update Loggers
            LogManager.UpdateLogger("KeyCount", $"Key Count: {GameBase.SelectedBeatmap.Qua.KeyCount}");
            LogManager.UpdateLogger("DeltaTime", "Delta Time: " + dt + "ms");
            LogManager.UpdateLogger("SongTime", "Current Song Time: " + Timing.CurrentSongTime + "ms");
            LogManager.UpdateLogger("SongPos", "Current Track Position: " + NoteRendering.TrackPosition);
            LogManager.UpdateLogger("HitObjects", "Total Remaining Notes: " + NoteRendering.HitObjectPool.Count);
            LogManager.UpdateLogger("Skippable", $"Intro Skippable: {IntroSkippable}");
        }

        /// <summary>
        ///     TODO: Add Summary
        /// </summary>
        public void Draw()
        {
            Playfield.Boundary.Draw();
            NoteRendering.Boundary.Draw();
        }
    }
}