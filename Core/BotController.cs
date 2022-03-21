using Core.Goals;
using Core.GOAP;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Session;
using SharedLib;
using Game;
using WinAPI;
using Microsoft.Extensions.Configuration;
using SharedLib.NpcFinder;
using Cyotek.Collections.Generic;

namespace Core
{
    public sealed class BotController : IBotController, IDisposable
    {
        private readonly WowProcess wowProcess;
        private readonly ILogger logger;
        private readonly IPPather pather;
        private readonly NpcNameFinder npcNameFinder;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly IAddonDataProvider addonDataProvider;
        private readonly INodeFinder minimapNodeFinder;
        private readonly CancellationTokenSource cts;
        private readonly Wait wait;
        private readonly AutoResetEvent addonAutoResetEvent = new(false);
        private readonly AutoResetEvent npcNameFinderAutoResetEvent = new(false);

        public DataConfig DataConfig { get; }

        public AddonReader AddonReader { get; }

        public Thread? screenshotThread { get; }

        public Thread addonThread { get; }

        public Thread? botThread { get; set; }

        public GoapAgent? GoapAgent { get; set; }

        public RouteInfo? RouteInfo { get; set; }

        public WowScreen WowScreen { get; }
        public WowProcessInput WowProcessInput { get; }

        public ConfigurableInput? ConfigurableInput { get; set; }

        public ClassConfiguration? ClassConfig { get; set; }

        public IImageProvider? MinimapImageFinder { get; }

        public ExecGameCommand ExecGameCommand { get; }

        public ActionBarPopulator? ActionBarPopulator { get; set; }

        public IGrindSession GrindSession { get; }
        public IGrindSessionHandler GrindSessionHandler { get; }
        public string SelectedClassFilename { get; set; } = string.Empty;
        public string? SelectedPathFilename { get; set; }

        public event EventHandler? ProfileLoaded;
        public event EventHandler? StatusChanged;

        public double AvgScreenLatency
        {
            get
            {
                double avg = 0;
                for (int i = 0; i < ScreenLatencys.Size; i++)
                {
                    avg += ScreenLatencys.PeekAt(i);
                }
                return avg /= ScreenLatencys.Size;
            }
        }
        private readonly CircularBuffer<double> ScreenLatencys;

        public double AvgNPCLatency
        {
            get
            {
                double avg = 0;
                for (int i = 0; i < NPCLatencys.Size; i++)
                {
                    avg += NPCLatencys.PeekAt(i);
                }
                return avg /= NPCLatencys.Size;
            }
        }
        private readonly CircularBuffer<double> NPCLatencys;

        private const int screenshotTickMs = 200;
        private DateTime lastScreenshot;

        private GoalThread? actionThread;

        private readonly Stopwatch updatePlayerPostion = new Stopwatch();

        public BotController(ILogger logger, IPPather pather, DataConfig dataConfig, IConfiguration configuration)
        {
            this.logger = logger;
            this.pather = pather;
            this.DataConfig = dataConfig;

            cts = new CancellationTokenSource();

            if (pather is RemotePathingAPI)
            {
                updatePlayerPostion.Start();
            }

            wowProcess = new WowProcess();
            WowScreen = new WowScreen(logger, wowProcess);
            WowProcessInput = new WowProcessInput(logger, wowProcess);

            ExecGameCommand = new ExecGameCommand(logger, WowProcessInput);

            GrindSessionHandler = new LocalGrindSessionHandler(dataConfig.History);
            GrindSession = new GrindSession(this, GrindSessionHandler);

            var frames = DataFrameConfiguration.LoadFrames();

            var scad = new StartupConfigAddonData();
            configuration.GetSection(StartupConfigAddonData.Position).Bind(scad);
            if (scad.Mode == "Network")
            {
                logger.LogInformation("Using NetworkedAddonDataProvider");
                addonDataProvider = new NetworkedAddonDataProvider(logger, scad.myPort, scad.connectTo, scad.connectPort);
            }
            else
            {
                logger.LogInformation("Using AddonDataProvider");
                addonDataProvider = new AddonDataProvider(WowScreen, frames);
            }

            AddonReader = new AddonReader(logger, DataConfig, addonDataProvider);

            wait = new Wait(AddonReader, addonAutoResetEvent);

            minimapNodeFinder = new MinimapNodeFinder(WowScreen, new PixelClassifier());
            MinimapImageFinder = minimapNodeFinder as IImageProvider;

            ScreenLatencys = new CircularBuffer<double>(5);
            NPCLatencys = new CircularBuffer<double>(5);

            addonThread = new Thread(AddonRefreshThread);
            addonThread.Start();

            // wait for addon to read the wow state
            wait.Update(1);

            var sw = new Stopwatch();
            sw.Start();
            while (!Enum.GetValues(typeof(PlayerClassEnum)).Cast<PlayerClassEnum>().Contains(AddonReader.PlayerReader.Class))
            {
                if (sw.ElapsedMilliseconds > 5000)
                {
                    logger.LogWarning("There is a problem with the addon, I have been unable to read the player class. Is it running ?");
                    sw.Restart();
                }
                wait.Update(1);
            }

            logger.LogDebug($"Woohoo, I have read the player class. You are a {AddonReader.PlayerReader.Race} {AddonReader.PlayerReader.Class}.");

            npcNameFinder = new NpcNameFinder(logger, WowScreen, npcNameFinderAutoResetEvent);
            npcNameTargeting = new NpcNameTargeting(logger, npcNameFinder, WowProcessInput);
            WowScreen.AddDrawAction(npcNameFinder.ShowNames);
            WowScreen.AddDrawAction(npcNameTargeting.ShowClickPositions);

            screenshotThread = new Thread(ScreenshotRefreshThread);
            screenshotThread.Start();
        }

        public void AddonRefreshThread()
        {
            while (!cts.IsCancellationRequested)
            {
                this.AddonReader.AddonRefresh();
                this.GoapAgent?.UpdateWorldState();
                addonAutoResetEvent.Set();
                Thread.Sleep(1);
            }
            this.logger.LogInformation("Addon thread stoppped!");
        }

        public void ScreenshotRefreshThread()
        {
            var nodeFound = false;
            var stopWatch = new Stopwatch();
            while (!cts.IsCancellationRequested)
            {
                if ((DateTime.UtcNow - lastScreenshot).TotalMilliseconds > screenshotTickMs)
                {
                    if (this.WowScreen.Enabled)
                    {
                        stopWatch.Restart();
                        this.WowScreen.Update();
                        ScreenLatencys.Put(stopWatch.ElapsedMilliseconds);

                        stopWatch.Restart();
                        this.npcNameFinder.Update();
                        NPCLatencys.Put(stopWatch.ElapsedMilliseconds);

                        this.WowScreen.PostProcess();
                    }
                    else
                    {
                        this.npcNameFinder.FakeUpdate();
                    }

                    lastScreenshot = DateTime.UtcNow;
                }

                if (ClassConfig != null && this.ClassConfig.Mode == Mode.AttendedGather)
                {
                    nodeFound = this.minimapNodeFinder.Find(nodeFound) != null;
                }

                if (updatePlayerPostion.ElapsedMilliseconds > 500)
                {
                    this.pather.DrawSphere(new PPather.SphereArgs
                    {
                        Colour = AddonReader.PlayerReader.Bits.PlayerInCombat ? 1 : AddonReader.PlayerReader.HasTarget ? 6 : 2,
                        Name = "Player",
                        MapId = this.AddonReader.UIMapId.Value,
                        Spot = this.AddonReader.PlayerReader.PlayerLocation
                    });
                    updatePlayerPostion.Restart();
                }

                cts.Token.WaitHandle.WaitOne(5);
            }
            this.logger.LogInformation("Screenshot thread stoppped!");
        }

        public bool IsBotActive => actionThread == null ? false : actionThread.Active;

        public void ToggleBotStatus()
        {
            if (actionThread != null)
            {
                if (!actionThread.Active)
                {
                    this.GrindSession.StartBotSession();
                    this.pather.DrawLines();

                    actionThread.Active = true;
                    botThread = new Thread(() => Task.Factory.StartNew(() => BotThread()));
                    botThread.Start();
                }
                else
                {
                    actionThread.Active = false;
                    GrindSession.StopBotSession("Stopped By Player", false);
                    AddonReader.SoftReset();
                }

                StatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public ValueTask BotThread()
        {
            if (actionThread != null)
            {
                actionThread.ResumeIfNeeded();

                while (actionThread.Active && !cts.IsCancellationRequested)
                {
                    actionThread.GoapPerformGoal();
                }
            }

            if (ConfigurableInput != null)
                new StopMoving(ConfigurableInput, AddonReader.PlayerReader).Stop();

            logger.LogInformation("Bot thread stopped!");
            return ValueTask.CompletedTask;
        }

        public bool InitialiseFromFile(string classFile, string? pathFile)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ClassConfig?.Dispose();
                ClassConfig = ReadClassConfiguration(classFile, pathFile);
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                return false;
            }

            Initialize(ClassConfig);

            stopwatch.Stop();
            logger.LogInformation($"[{nameof(BotController)}] Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

            return true;
        }

        private void Initialize(ClassConfiguration config)
        {
            AddonReader.SoftReset();

            ConfigurableInput = new ConfigurableInput(logger, wowProcess, config);

            ActionBarPopulator = new ActionBarPopulator(logger, config, AddonReader, ExecGameCommand);

            IBlacklist blacklist = config.Mode != Mode.Grind ? new NoBlacklist() : new Blacklist(logger, AddonReader, config.NPCMaxLevels_Above, config.NPCMaxLevels_Below, config.CheckTargetGivesExp, config.Blacklist);

            var actionFactory = new GoalFactory(logger, AddonReader, ConfigurableInput, DataConfig, npcNameFinder, npcNameTargeting, pather, ExecGameCommand);

            var goapAgentState = new GoapAgentState();
            var availableActions = actionFactory.CreateGoals(config, blacklist, goapAgentState, wait);

            this.GoapAgent?.Dispose();
            this.GoapAgent = new GoapAgent(logger, goapAgentState, ConfigurableInput, AddonReader, availableActions, blacklist);

            RouteInfo = actionFactory.RouteInfo;
            this.actionThread = new GoalThread(logger, GoapAgent, AddonReader, RouteInfo);

            // hookup events between actions
            availableActions.ToList().ForEach(a =>
            {
                a.ActionEvent += this.actionThread.OnActionEvent;
                a.ActionEvent += GoapAgent.OnActionEvent;

                // tell other action about my actions
                availableActions.ToList().ForEach(b =>
                {
                    if (b != a) { a.ActionEvent += b.OnActionEvent; }
                });
            });
        }

        private ClassConfiguration ReadClassConfiguration(string classFilename, string? pathFilename)
        {
            if(!classFilename.ToLower().Contains(AddonReader.PlayerReader.Class.ToString().ToLower()))
            {
                throw new Exception($"[{nameof(BotController)}] Not allowed to load other class profile!");
            }

            var classFilePath = Path.Join(DataConfig.Class, classFilename);
            if (File.Exists(classFilePath))
            {
                ClassConfiguration classConfig = JsonConvert.DeserializeObject<ClassConfiguration>(File.ReadAllText(classFilePath));
                var requirementFactory = new RequirementFactory(logger, AddonReader, npcNameFinder);
                classConfig.Initialise(DataConfig, AddonReader, requirementFactory, logger, pathFilename);

                logger.LogInformation($"[{nameof(BotController)}] Profile Loaded `{classFilename}` with `{classConfig.PathFilename}`.");

                return classConfig;
            }

            throw new ArgumentOutOfRangeException($"Class config file not found {classFilename}");
        }

        public void Dispose()
        {
            cts?.Cancel();

            if (GrindSession is IDisposable disposable)
            {
                disposable.Dispose();
            }

            // cleanup eventlisteners between actions
            GoapAgent?.AvailableGoals.ToList().ForEach(a =>
            {
                if (this.actionThread != null)
                {
                    a.ActionEvent -= this.actionThread.OnActionEvent;
                }

                a.ActionEvent -= GoapAgent.OnActionEvent;

                // tell other action about my actions
                GoapAgent?.AvailableGoals.ToList().ForEach(b =>
                {
                    if (b != a) { a.ActionEvent -= b.OnActionEvent; }
                });
            });
            GoapAgent?.Dispose();

            npcNameFinderAutoResetEvent.Dispose();
            addonAutoResetEvent.Dispose();
            WowScreen.Dispose();
            addonDataProvider?.Dispose();
            AddonReader.Dispose();
        }

        public void StopBot()
        {
            if (actionThread != null)
            {
                actionThread.Active = false;
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Shutdown()
        {
            cts.Cancel();
        }

        public void LoadClassProfile(string classFilename)
        {
            StopBot();
            if(InitialiseFromFile(classFilename, SelectedPathFilename))
            {
                SelectedClassFilename = classFilename;
            }

            ProfileLoaded?.Invoke(this, EventArgs.Empty);
        }

        public List<string> ClassFileList()
        {
            DirectoryInfo directory = new DirectoryInfo(DataConfig.Class);
            var list = directory.GetFiles().Select(i => i.Name).ToList();
            list.Sort(new NaturalStringComparer());
            list.Insert(0, "Press Init State first!");
            return list;
        }

        public List<string> PathFileList()
        {
            var root = DataConfig.Path;

            var files = Directory.EnumerateFiles(root, "*.json*", SearchOption.AllDirectories)
                .Select(path => path.Replace(root, "")).ToList();

            files.Sort(new NaturalStringComparer());
            files.Insert(0, "Use Class Profile Default");
            return files;
        }

        public void LoadPathProfile(string pathFilename)
        {
            StopBot();
            if(InitialiseFromFile(SelectedClassFilename, pathFilename))
            {
                SelectedPathFilename = pathFilename;
            }

            ProfileLoaded?.Invoke(this, EventArgs.Empty);
        }

        public void OverrideClassConfig(ClassConfiguration classConfiguration)
        {
            this.ClassConfig = classConfiguration;
            Initialize(this.ClassConfig);
        }
    }
}