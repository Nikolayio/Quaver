/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 * Copyright (c) 2017-2018 Swan & The Quaver Team <support@quavergame.com>.
*/

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Quaver.Shared.Assets;
using Quaver.Shared.Audio;
using Quaver.Shared.Config;
using Quaver.Shared.Database.Maps;
using Quaver.Shared.Database.Scores;
using Quaver.Shared.Database.Settings;
using Quaver.Shared.Graphics.Backgrounds;
using Quaver.Shared.Graphics.Notifications;
using Quaver.Shared.Graphics.Overlays.Volume;
using Quaver.Shared.Graphics.Transitions;
using Quaver.Shared.Helpers;
using Quaver.Shared.Online;
using Quaver.Shared.Online.Chat;
using Quaver.Shared.Profiling;
using Quaver.Shared.Scheduling;
using Quaver.Shared.Screens;
using Quaver.Shared.Screens.Alpha;
using Quaver.Shared.Screens.Menu;
using Quaver.Shared.Skinning;
using Steamworks;
using Wobble;
using Wobble.Audio.Samples;
using Wobble.Audio.Tracks;
using Wobble.Bindables;
using Wobble.Discord;
using Wobble.Discord.RPC;
using Wobble.Graphics;
using Wobble.Graphics.UI.Debugging;
using Wobble.Graphics.UI.Dialogs;
using Wobble.Input;
using Wobble.IO;
using Wobble.Logging;
using Wobble.Window;
using Version = YamlDotNet.Core.Version;

namespace Quaver.Shared
{
    public class QuaverGame : WobbleGame
    {
        /// <inheritdoc />
        /// <summary>
        /// </summary>
        protected override bool IsReadyToUpdate { get; set; }

        /// <summary>
        ///     The volume controller for the game.
        /// </summary>
        public VolumeController VolumeController { get; private set; }

        /// <summary>
        ///     The current activated screen.
        /// </summary>
        public QuaverScreen CurrentScreen { get; set; }

        /// <summary>
        ///     Unique identifier of the client's assembly version.
        /// </summary>
        protected AssemblyName AssemblyName => Assembly.GetEntryAssembly()?.GetName() ?? new AssemblyName { Version = new System.Version() };

        /// <summary>
        ///     Determines if the build is deployed/an official release.
        ///     By default, it's 0.0.0.0 - Anything else is considered deployed.
        /// </summary>
        public bool IsDeployedBuild => AssemblyName.Version.Major != 0 || AssemblyName.Version.Minor != 0 || AssemblyName.Version.Revision != 0 ||
                                        AssemblyName.Version.Build != 0;

        /// <summary>
        ///     Stringified version name of the client.
        /// </summary>
        public string Version
        {
            get
            {
                if (!IsDeployedBuild)
                    return "Local Development Build";

                var assembly = AssemblyName;
                return $@"{assembly.Version.Major}.{assembly.Version.Minor}.{assembly.Version.Build}";
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public QuaverGame() => InitializeFpsLimiting();

        /// <inheritdoc />
        /// <summary>
        ///     Allows the game to perform any initialization it needs to before starting to run.
        ///     This is where it can query for any required services and load any non-graphic
        ///     related content.  Calling base.Initialize will enumerate through any components
        ///     and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            PerformGameSetup();

            WindowManager.ChangeVirtualScreenSize(new Vector2(1366, 768));
            WindowManager.ChangeScreenResolution(new Point(ConfigManager.WindowWidth.Value, ConfigManager.WindowHeight.Value));

            // Full-screen
            Graphics.IsFullScreen = ConfigManager.WindowFullScreen.Value;

            // Apply all graphics changes
            Graphics.ApplyChanges();

            // Handle file dropped event.
            Window.FileDropped += MapsetImporter.OnFileDropped;

            base.Initialize();
        }

         /// <inheritdoc />
        /// <summary>
        ///     LoadContent will be called once per game and is the place to load
        ///     all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();

            Resources.AddStore(new DllResourceStore("Quaver.Resources.dll"));
            SteamManager.SendAvatarRetrievalRequest(SteamUser.GetSteamID().m_SteamID);

            // Load all game assets.
            BitmapFonts.Load();
            FontAwesome.Load();
            UserInterface.Load();

            BackgroundHelper.Initialize();

            // Load the user's skin
            SkinManager.Load();

            // Create the global Profiler
            CreateProfiler();
            VolumeController = new VolumeController() {Parent = GlobalUserInterface};
            BackgroundManager.Initialize();
            Transitioner.Initialize();

            // Make the cursor appear over the volume controller.
            ListHelper.Swap(GlobalUserInterface.Children, GlobalUserInterface.Children.IndexOf(GlobalUserInterface.Cursor),
                                                            GlobalUserInterface.Children.IndexOf(VolumeController));

            IsReadyToUpdate = true;

            Logger.Debug($"Currently running Quaver version: `{Version}`", LogType.Runtime);

            Window.Title = !IsDeployedBuild ? $"Quaver - {Version}" : $"Quaver v{Version}";
            QuaverScreenManager.ScheduleScreenChange(() => new AlphaScreen());
        }

        /// <inheritdoc />
        /// <summary>
        ///     UnloadContent will be called once per game and is the place to unload
        ///     game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            OnlineManager.Client?.Disconnect();
            Transitioner.Dispose();
            base.UnloadContent();
        }

        /// <inheritdoc />
        /// <summary>
        ///     Allows the game to run logic such as updating the world,
        ///     checking for collisions, gathering input, and playing audio.
        /// </summary>
        protected override void Update(GameTime gameTime)
        {
            if (!IsReadyToUpdate)
                return;

            base.Update(gameTime);

            if (SteamManager.IsInitialized)
                SteamAPI.RunCallbacks();

            // Run scheduled background tasks
            CommonTaskScheduler.Run();

            BackgroundManager.Update(gameTime);
            BackgroundHelper.Update(gameTime);
            NotificationManager.Update(gameTime);
            ChatManager.Update(gameTime);
            DialogManager.Update(gameTime);

            // Handles FPS limiter changes
            if (KeyboardManager.IsUniqueKeyPress(Keys.F7))
            {
                var index = (int) ConfigManager.FpsLimiterType.Value;

                if (index + 1 < Enum.GetNames(typeof(FpsLimitType)).Length)
                    ConfigManager.FpsLimiterType.Value = (FpsLimitType) index + 1;
                else
                    ConfigManager.FpsLimiterType.Value = FpsLimitType.Unlimited;

                switch (ConfigManager.FpsLimiterType.Value)
                {
                    case FpsLimitType.Unlimited:
                        NotificationManager.Show(NotificationLevel.Info, "FPS is now unlimited.");
                        break;
                    case FpsLimitType.Limited:
                        NotificationManager.Show(NotificationLevel.Info, $"FPS is now limited to: 240 FPS");
                        break;
                    case FpsLimitType.Vsync:
                        NotificationManager.Show(NotificationLevel.Info, $"Vsync Enabled");
                        break;
                    case FpsLimitType.Custom:
                        NotificationManager.Show(NotificationLevel.Info, $"FPS is now custom limited to: {ConfigManager.CustomFpsLimit.Value}");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            QuaverScreenManager.Update(gameTime);
            Transitioner.Update(gameTime);
        }

        /// <inheritdoc />
        /// <summary>
        ///     This is called when the game should draw itself.
        /// </summary>
        protected override void Draw(GameTime gameTime)
        {
            if (!IsReadyToUpdate)
                return;

            base.Draw(gameTime);

            // Draw dialogs
            DialogManager.Draw(gameTime);

            NotificationManager.Draw(gameTime);

            // Draw the global container last.
            GlobalUserInterface.Draw(gameTime);

            Transitioner.Draw(gameTime);
        }

        /// <summary>
        ///     Performs any initial setup the game needs to run.
        /// </summary>
        private void PerformGameSetup()
        {
            ConfigManager.Initialize();

            DeleteTemporaryFiles();

            ScoreDatabaseCache.CreateTable();
            MapDatabaseCache.Load(false);
            QuaverSettingsDatabaseCache.Initialize();

            // Force garabge collection.
            GC.Collect();

            // Start watching for mapset changes in the folder.
            MapsetImporter.WatchForChanges();

            // Initially set the global volume.
            AudioTrack.GlobalVolume = ConfigManager.VolumeGlobal.Value;
            AudioSample.GlobalVolume = ConfigManager.VolumeEffect.Value;

            ConfigManager.VolumeGlobal.ValueChanged += (sender, e) => AudioTrack.GlobalVolume = e.Value;;
            ConfigManager.VolumeMusic.ValueChanged += (sender, e) => { if (AudioEngine.Track != null) AudioEngine.Track.Volume = e.Value;  };
            ConfigManager.VolumeEffect.ValueChanged += (sender, e) => AudioSample.GlobalVolume = e.Value;
            ConfigManager.Pitched.ValueChanged += (sender, e) => AudioEngine.Track.ToggleRatePitching(e.Value);
            ConfigManager.FpsLimiterType.ValueChanged += (sender, e) => InitializeFpsLimiting();
            ConfigManager.WindowFullScreen.ValueChanged += (sender, e) => Graphics.IsFullScreen = e.Value;

            // Handle discord rich presence.
            DiscordManager.CreateClient("376180410490552320");
            DiscordManager.Client.SetPresence(new RichPresence
            {
                Assets = new Wobble.Discord.RPC.Assets()
                {
                    LargeImageKey = "quaver",
                    LargeImageText = ConfigManager.Username.Value
                },
                Timestamps = new Timestamps()
            });

            // Create bindable for selected map.
            if (MapManager.Mapsets.Count != 0)
                MapManager.Selected = new Bindable<Map>(MapManager.Mapsets.First().Maps.First());
        }

        /// <summary>
        ///     Deletes all of the temporary files for the game if they exist.
        /// </summary>
        private static void DeleteTemporaryFiles()
        {
            try
            {
                foreach (var file in new DirectoryInfo(ConfigManager.DataDirectory + "/temp/").GetFiles("*", SearchOption.AllDirectories))
                    file.Delete();

                foreach (var dir in new DirectoryInfo(ConfigManager.DataDirectory + "/temp/").GetDirectories("*", SearchOption.AllDirectories))
                    dir.Delete(true);
            }
            catch (Exception)
            {
                // ignored
            }

            // Create a directory that displays the "Now playing" song.
            Directory.CreateDirectory($"{ConfigManager.DataDirectory}/temp/Now Playing");
        }

        /// <summary>
        ///     Creates the Profiler which displays system statistics such as FPS, Memory Usage and CPU Usage
        /// </summary>
        private void CreateProfiler()
        {
            var profiler = new Profiler(GlobalUserInterface);
            ConfigManager.FpsCounter.ValueChanged += (o, e) => UpdateVisiblityActivity(profiler);
        }

        /// <summary>
        ///     Shows the profiler based on the current config value.
        /// </summary>
        /// <param name="profiler"></param>
        private static void UpdateVisiblityActivity(Profiler profiler)
        {
            if (ConfigManager.FpsCounter.Value)
                profiler.ShowProfiler();

            else
                profiler.HideProfiler();
        }

        /// <summary>
        ///    Handles limiting/unlimiting FPS based on user config
        /// </summary>
        private void InitializeFpsLimiting()
        {
            switch (ConfigManager.FpsLimiterType.Value)
            {
                case FpsLimitType.Unlimited:
                    Graphics.SynchronizeWithVerticalRetrace = false;
                    IsFixedTimeStep = false;
                    break;
                case FpsLimitType.Limited:
                    Graphics.SynchronizeWithVerticalRetrace = false;
                    IsFixedTimeStep = true;
                    TargetElapsedTime = TimeSpan.FromSeconds(1d / 240d);
                    break;
                case FpsLimitType.Vsync:
                    Graphics.SynchronizeWithVerticalRetrace = true;
                    IsFixedTimeStep = true;
                    break;
                case FpsLimitType.Custom:
                    Graphics.SynchronizeWithVerticalRetrace = false;
                    TargetElapsedTime = TimeSpan.FromSeconds(1d / ConfigManager.CustomFpsLimit.Value);
                    IsFixedTimeStep = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Graphics.ApplyChanges();
        }
    }
}
