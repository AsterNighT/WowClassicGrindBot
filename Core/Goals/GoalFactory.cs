﻿using Core.Goals;
using SharedLib.NpcFinder;
using Core.PPather;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.GOAP;
using System.Numerics;

namespace Core
{
    public class GoalFactory
    {
        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly DataConfig dataConfig;

        private readonly AddonReader addonReader;
        private readonly NpcNameFinder npcNameFinder;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly IPPather pather;

        private readonly ExecGameCommand exec;

        public RouteInfo? RouteInfo { get; private set; }

        public GoalFactory(ILogger logger, AddonReader addonReader, ConfigurableInput input, DataConfig dataConfig, NpcNameFinder npcNameFinder, NpcNameTargeting npcNameTargeting, IPPather pather, ExecGameCommand execGameCommand)
        {
            this.logger = logger;
            this.addonReader = addonReader;
            this.input = input;
            this.dataConfig = dataConfig;
            this.npcNameFinder = npcNameFinder;
            this.npcNameTargeting = npcNameTargeting;
            this.pather = pather;
            this.exec = execGameCommand;
        }

        public HashSet<GoapGoal> CreateGoals(ClassConfiguration classConfig, IBlacklist blacklist, GoapAgentState goapAgentState, Wait wait)
        {
            var availableActions = new HashSet<GoapGoal>();

            GetPaths(out List<Vector3> pathPoints, out List<Vector3> spiritPath, classConfig);

            var playerDirection = new PlayerDirection(logger, input, addonReader.PlayerReader);
            var stopMoving = new StopMoving(input, addonReader.PlayerReader);

            var castingHandler = new CastingHandler(logger, input, wait, addonReader, classConfig, playerDirection, stopMoving);

            var stuckDetector = new StuckDetector(logger, input, addonReader.PlayerReader, playerDirection, stopMoving);
            var combatUtil = new CombatUtil(logger, input, wait, addonReader.PlayerReader);
            var mountHandler = new MountHandler(logger, input, classConfig, wait, addonReader.PlayerReader, castingHandler, stopMoving);

            var targetFinder = new TargetFinder(logger, input, classConfig, wait, addonReader.PlayerReader, blacklist, npcNameTargeting);

            var followNav = new Navigation(logger, playerDirection, input, addonReader, stopMoving, stuckDetector, pather, mountHandler);
            var followRouteAction = new FollowRouteGoal(logger, input, wait, addonReader, classConfig, pathPoints, followNav, mountHandler, npcNameFinder, targetFinder);

            var corpseNav = new Navigation(logger, playerDirection, input, addonReader, stopMoving, stuckDetector, pather, mountHandler);
            var walkToCorpseAction = new WalkToCorpseGoal(logger, input, wait, addonReader, corpseNav, stopMoving);

            availableActions.Clear();

            if (classConfig.Mode == Mode.CorpseRun)
            {
                availableActions.Add(new WaitGoal(logger));
                availableActions.Add(new CorpseRunGoal(addonReader.PlayerReader, input, playerDirection, spiritPath, logger, stuckDetector));
            }
            else if (classConfig.Mode == Mode.AttendedGather)
            {
                availableActions.Add(followRouteAction);
                availableActions.Add(new CorpseRunGoal(addonReader.PlayerReader, input, playerDirection, spiritPath, logger, stuckDetector));
            }
            else
            {
                if (classConfig.Mode == Mode.AttendedGrind)
                {
                    availableActions.Add(new WaitGoal(logger));
                }
                else
                {
                    availableActions.Add(followRouteAction);
                    availableActions.Add(walkToCorpseAction);
                }

                availableActions.Add(new ApproachTargetGoal(logger, input, wait, addonReader.PlayerReader, stopMoving, mountHandler));

                if (classConfig.WrongZone.ZoneId > 0)
                {
                    availableActions.Add(new WrongZoneGoal(addonReader, input, playerDirection, logger, stuckDetector, classConfig));
                }

                if (classConfig.Parallel.Sequence.Count > 0)
                {
                    availableActions.Add(new ParallelGoal(logger, input, wait, addonReader.PlayerReader, stopMoving, classConfig.Parallel.Sequence, castingHandler, mountHandler));
                }

                if (classConfig.Loot)
                {
                    if (classConfig.KeyboardOnly)
                    {
                        var lootAction = new LastTargetLoot(logger, input, wait, addonReader, stopMoving, combatUtil);
                        lootAction.AddPreconditions();
                        availableActions.Add(lootAction);
                    }
                    else
                    {
                        var lootAction = new LootGoal(logger, input, wait, addonReader, stopMoving, classConfig, npcNameTargeting, combatUtil, playerDirection);
                        lootAction.AddPreconditions();
                        availableActions.Add(lootAction);
                    }

                    if (classConfig.Skin)
                    {
                        availableActions.Add(new SkinningGoal(logger, input, addonReader, wait, stopMoving, npcNameTargeting, combatUtil));
                    }
                }

                try
                {
                    var genericCombat = new CombatGoal(logger, input, wait, addonReader, stopMoving, classConfig, castingHandler, mountHandler);
                    availableActions.Add(genericCombat);

                    if (addonReader.PlayerReader.Class is
                        PlayerClassEnum.Hunter or
                        PlayerClassEnum.Warlock or
                        PlayerClassEnum.Mage)
                    {
                        availableActions.Add(new TargetPetTarget(input, addonReader.PlayerReader));
                    }

                    availableActions.Add(new PullTargetGoal(logger, input, wait, addonReader, blacklist, stopMoving, castingHandler, mountHandler, stuckDetector, classConfig));

                    availableActions.Add(new ConsumeCorpse(logger, classConfig));
                    availableActions.Add(new CorpseConsumed(logger, goapAgentState));

                    foreach (var item in classConfig.Adhoc.Sequence)
                    {
                        availableActions.Add(new AdhocGoal(logger, input, wait, item, addonReader, stopMoving, castingHandler, mountHandler));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                }


                foreach (var item in classConfig.NPC.Sequence)
                {
                    var nav = new Navigation(logger, playerDirection, input, addonReader, stopMoving, stuckDetector, pather, mountHandler);
                    availableActions.Add(new AdhocNPCGoal(logger, input, item, wait, addonReader, nav, stopMoving, npcNameTargeting, classConfig, blacklist, mountHandler, exec));
                    item.Path.Clear();
                    item.Path.AddRange(ReadPath(item.Name, item.PathFilename));
                }

                var pathProviders = availableActions.Where(a => a is IRouteProvider)
                    .Cast<IRouteProvider>()
                    .ToList();

                this.RouteInfo = new RouteInfo(pathPoints, spiritPath, pathProviders, addonReader);

                this.pather.DrawLines(new List<LineArgs>()
                {
                    new LineArgs  { Spots = pathPoints, Name = "grindpath", Colour = 2, MapId = addonReader.UIMapId.Value },
                    new LineArgs { Spots = spiritPath, Name = "spirithealer", Colour = 3, MapId = addonReader.UIMapId.Value }
                });
            }

            return availableActions;
        }

        private string FixPathFilename(string path)
        {
            if (!path.Contains(':') && !string.IsNullOrEmpty(path) && !path.Contains(dataConfig.Path))
            {
                return Path.Join(dataConfig.Path, path);
            }
            return path;
        }

        private void GetPaths(out List<Vector3> pathPoints, out List<Vector3> spiritPath, ClassConfiguration classConfig)
        {
            classConfig.PathFilename = FixPathFilename(classConfig.PathFilename);
            classConfig.SpiritPathFilename = FixPathFilename(classConfig.SpiritPathFilename);

            pathPoints = CreatePathPoints(classConfig);
            spiritPath = CreateSpiritPathPoints(pathPoints, classConfig);
        }

        private IEnumerable<Vector3> ReadPath(string name, string pathFilename)
        {
            try
            {
                if (string.IsNullOrEmpty(pathFilename))
                {
                    return new List<Vector3>();
                }
                else
                {
                    return JsonConvert.DeserializeObject<List<Vector3>>(File.ReadAllText(FixPathFilename(pathFilename)));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Reading path: {name}");
                throw;
            }
        }

        private static List<Vector3> CreateSpiritPathPoints(List<Vector3> pathPoints, ClassConfiguration classConfig)
        {
            List<Vector3> spiritPath;
            if (string.IsNullOrEmpty(classConfig.SpiritPathFilename))
            {
                spiritPath = new List<Vector3> { pathPoints.First() };
            }
            else
            {
                string spiritText = File.ReadAllText(classConfig.SpiritPathFilename);
                spiritPath = JsonConvert.DeserializeObject<List<Vector3>>(spiritText);
            }

            return spiritPath;
        }

        private static List<Vector3> CreatePathPoints(ClassConfiguration classConfig)
        {
            List<Vector3> output = new List<Vector3>();

            string text = File.ReadAllText(classConfig.PathFilename);
            var points = JsonConvert.DeserializeObject<List<Vector3>>(text);

            int step = classConfig.PathReduceSteps ? 2 : 1;
            for (int i = 0; i < points.Count; i += step)
            {
                if (i < points.Count)
                {
                    output.Add(points[i]);
                }
            }

            return output;
        }
    }
}