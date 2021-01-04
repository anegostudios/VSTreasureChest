﻿using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VSTreasureChest
{
    /// <summary>
    /// Redirects all log entries into the visual studio output window. Only for your convenience during development and testing.
    /// </summary>
    public class RedirectLogs : ModSystem
    {
        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Server.Logger.EntryAdded += OnServerLogEntry;
        }

        private void OnServerLogEntry(EnumLogType logType, string message, object[] args)
        {
            if (logType == EnumLogType.VerboseDebug)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Server {logType}] {message}", args);
        }
    }
}