﻿using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;

namespace Core.Session
{
    public class GrindSession : IGrindSession, IDisposable
    {
        private readonly IBotController _botController;
        private readonly IGrindSessionHandler _grindSessionHandler;
        private readonly CancellationTokenSource cts;
        private Thread? _autoUpdateThread;

        public GrindSession(IBotController botController, IGrindSessionHandler grindSessionHandler)
        {
            cts = new CancellationTokenSource();
            _botController = botController;
            _grindSessionHandler = grindSessionHandler;
        }
        [JsonIgnore] 
        public bool Active { get; set; }
        public Guid SessionId { get; set; }
        public string PathName { get; set; } = "No path selected";
        public PlayerClassEnum PlayerClass { get; set; }
        public DateTime SessionStart { get; set; }
        public DateTime SessionEnd { get; set; }
        [JsonIgnore]
        public int TotalTimeInMinutes => (int)(SessionEnd - SessionStart).TotalMinutes;
        public int LevelFrom { get; set; }
        public float XpFrom { get; set; }
        public int LevelTo { get; set; }
        public float XpTo { get; set; }
        public int MobsKilled { get; set; }
        public float MobsPerMinute => MathF.Round(MobsKilled / (float)TotalTimeInMinutes, 2);
        public int Death { get; set; }
        public string? Reason { get; set; }
        [JsonIgnore]
        public float ExperiencePerHour => TotalTimeInMinutes == 0 ? 0 : MathF.Round(ExpGetInBotSession / TotalTimeInMinutes * 60f, 0);
        [JsonIgnore]
        public float ExpGetInBotSession
        {
            get
            {
                var expList = ExperienceProvider.GetExperienceList(); // eh should not load a file each time called this getter :(
                var maxLevel = expList.Length + 1;
                if (LevelFrom == maxLevel)
                    return 0;

                if (LevelFrom == maxLevel-1 && LevelTo == maxLevel)
                    return expList[LevelFrom - 1] - XpFrom;

                if (LevelTo == LevelFrom)
                {
                    return XpTo - XpFrom;
                }

                if (LevelTo > LevelFrom)
                {
                    var expSoFar = XpTo;

                    for (int i = 0; i < LevelTo-LevelFrom; i++)
                    {
                        expSoFar += expList[LevelFrom - 1 + i] - XpFrom;
                        XpFrom = 0;
                        if (LevelTo > maxLevel)
                            break;
                    }

                    return expSoFar;
                }

                return 0;
            }
        }

        public void Dispose()
        {
            cts?.Cancel();
        }

        public void StartBotSession()
        {
            Active = true;
            SessionId = Guid.NewGuid();
            PathName = _botController.SelectedPathFilename ?? _botController.ClassConfig?.PathFilename ?? "No Path Selected";
            PlayerClass = _botController.AddonReader.PlayerReader.Class;
            SessionStart = DateTime.UtcNow;
            LevelFrom = _botController.AddonReader.PlayerReader.Level.Value;
            XpFrom = _botController.AddonReader.PlayerReader.PlayerXp.Value;
            MobsKilled = _botController.AddonReader.LevelTracker.MobsKilled;
            _autoUpdateThread = new Thread(() => AutoUpdate());
            _autoUpdateThread.Start();
        }

        private void AutoUpdate()
        {
            while (Active && !cts.IsCancellationRequested)
            {
                StopBotSession("auto save", true);
                cts.Token.WaitHandle.WaitOne(TimeSpan.FromMinutes(1));
            }
        }

        public void StopBotSession(string reason = "stopped by player", bool active = false)
        {
            Active = active;
            SessionEnd = DateTime.UtcNow;
            LevelTo = _botController.AddonReader.PlayerReader.Level.Value;
            XpTo = _botController.AddonReader.PlayerReader.PlayerXp.Value;
            Reason = reason;
            Death = _botController.AddonReader.LevelTracker.Death;
            MobsKilled = _botController.AddonReader.LevelTracker.MobsKilled;
            Save();
        }
        
        public void Save()
        {
            _grindSessionHandler.Save(this);
        }

        public List<GrindSession> Load()
        {
            return _grindSessionHandler.Load();
        }
    }
}
