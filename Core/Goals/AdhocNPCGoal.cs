﻿using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using System.Linq;
using System.Threading;

namespace Core.Goals
{
    public class AdhocNPCGoal : GoapGoal, IRouteProvider
    {
        private enum PathState
        {
            ApproachPathStart,
            FollowPath,
            Finished,
        }

        private PathState pathState;

        private readonly bool debug = false;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly KeyAction key;
        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly Navigation navigation;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly ClassConfiguration classConfig;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly IBlacklist blacklist;
        private readonly MountHandler mountHandler;

        private readonly ExecGameCommand execGameCommand;
        private readonly GossipReader gossipReader;

        private readonly int GossipTimeout = 5000;

        private bool shouldMount;

        public override string Name => Keys.Count == 0 ? base.Name : $"{base.Name} ({Keys[0].Name})";
        public override float CostOfPerformingAction => key.Cost;

        #region IRouteProvider

        public List<Vector3> PathingRoute()
        {
            return navigation.TotalRoute;
        }

        public bool HasNext()
        {
            return navigation.HasNext();
        }

        public Vector3 NextPoint()
        {
            return navigation.NextPoint();
        }

        public DateTime LastActive => navigation.LastActive;

        #endregion

        public AdhocNPCGoal(ILogger logger, ConfigurableInput input, KeyAction key, Wait wait, AddonReader addonReader, Navigation navigation, StopMoving stopMoving, NpcNameTargeting npcNameTargeting, ClassConfiguration classConfig, IBlacklist blacklist, MountHandler mountHandler, ExecGameCommand exec)
        {
            this.logger = logger;
            this.input = input;
            this.key = key;
            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.stopMoving = stopMoving;
            this.npcNameTargeting = npcNameTargeting;

            this.classConfig = classConfig;
            
            this.blacklist = blacklist;
            this.mountHandler = mountHandler;

            this.execGameCommand = exec;
            this.gossipReader = addonReader.GossipReader;

            this.navigation = navigation;
            navigation.OnDestinationReached += Navigation_OnDestinationReached;
            navigation.OnWayPointReached += Navigation_OnWayPointReached;

            if (key.InCombat == "false")
            {
                AddPrecondition(GoapKey.dangercombat, false);
            }
            else if (key.InCombat == "true")
            {
                AddPrecondition(GoapKey.incombat, true);
            }

            this.Keys.Add(key);
        }

        public override bool CheckIfActionCanRun()
        {
            return key.CanRun();
        }

        public override void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.resume)
            {
                navigation.ResetStuckParameters();
            }
        }

        public override ValueTask OnEnter()
        {
            SendActionEvent(new ActionEventArgs(GoapKey.fighting, false));

            input.TapClearTarget();
            stopMoving.Stop();

            var path = key.Path.ToList();
            navigation.SetWayPoints(path);

            pathState = PathState.ApproachPathStart;

            if (classConfig.UseMount &&
                mountHandler.CanMount() && !shouldMount &&
                mountHandler.ShouldMount(navigation.TotalRoute.Last()))
            {
                shouldMount = true;
                Log("Mount up since desination far away");
            }

            return base.OnEnter();
        }

        public override ValueTask OnExit()
        {
            navigation.Stop();

            return base.OnExit();
        }

        public override ValueTask PerformAction()
        {
            if (this.playerReader.Bits.PlayerInCombat && this.classConfig.Mode != Mode.AttendedGather) { return ValueTask.CompletedTask; }

            if (playerReader.Bits.IsDrowning)
            {
                input.TapJump("Drowning! Swim up");
            }

            if (pathState != PathState.Finished)
                navigation.Update();

            MountIfRequired();

            wait.Update(1);

            return ValueTask.CompletedTask;
        }


        private void Navigation_OnWayPointReached(object? sender, EventArgs e)
        {
            if (pathState is PathState.ApproachPathStart)
            {
                LogDebug("1 Reached the start point of the path.");
                navigation.SimplifyRouteToWaypoint = false;
            }
        }

        private void Navigation_OnDestinationReached(object? sender, EventArgs e)
        {
            if (pathState == PathState.ApproachPathStart)
            {
                LogDebug("Reached defined path end");
                stopMoving.Stop();

                input.TapClearTarget();
                wait.Update(1);

                npcNameTargeting.ChangeNpcType(NpcNames.Friendly | NpcNames.Neutral);
                npcNameTargeting.WaitForNUpdate(1);
                bool foundVendor = npcNameTargeting.FindBy(CursorType.Vendor, CursorType.Repair, CursorType.Innkeeper);
                if (!foundVendor)
                {
                    LogWarn($"No target found by cursor({nameof(CursorType.Vendor)}, {nameof(CursorType.Repair)}, {nameof(CursorType.Innkeeper)})! Attempt to use macro to aquire target");
                    input.KeyPress(key.ConsoleKey, input.defaultKeyPress);
                }

                (bool targetTimeout, double targetElapsedMs) = wait.Until(400, () => playerReader.HasTarget);
                if (targetTimeout)
                {
                    LogWarn("No target found!");
                    using var cts = new CancellationTokenSource();
                    input.KeyPressSleep(input.TurnLeftKey, 250, cts, "Turn left to find NPC");
                    return;
                }

                Log($"Found Target after {targetElapsedMs}ms");

                if (!foundVendor)
                {
                    input.TapInteractKey("Interact with target from macro");
                }

                if (OpenMerchantWindow())
                {
                    if (addonReader.BagReader.BagsFull)
                    {
                        LogWarn("There was no grey item to sell. Stuck here!");
                    }

                    input.KeyPress(ConsoleKey.Escape, input.defaultKeyPress);
                    input.TapClearTarget();
                    wait.Update(1);

                    var path = key.Path.ToList();
                    path.Reverse();
                    navigation.SetWayPoints(path);

                    pathState++;

                    LogDebug("Go back reverse to the start point of the path.");
                    navigation.ResetStuckParameters();

                    // At this point the BagsFull is false
                    // which mean it it would exit the Goal
                    // instead keep it trapped to follow the route back
                    while (navigation.HasWaypoint())
                    {
                        navigation.Update();
                        wait.Update(1);
                    }

                    pathState = PathState.Finished;

                    LogDebug("2 Reached the start point of the path.");
                    stopMoving.Stop();

                    navigation.SimplifyRouteToWaypoint = true;
                }
            }
        }


        private void MountIfRequired()
        {
            if (shouldMount && !mountHandler.IsMounted())
            {
                shouldMount = false;
                mountHandler.MountUp();
            }
        }

        private bool OpenMerchantWindow()
        {
            (bool timeout, double elapsedMs) = wait.Until(GossipTimeout, () => gossipReader.GossipStart || gossipReader.MerchantWindowOpened);
            if (gossipReader.MerchantWindowOpened)
            {
                LogWarn($"Gossip no options! {elapsedMs}ms");
            }
            else
            {
                (bool gossipEndTimeout, double gossipEndElapsedMs) = wait.Until(GossipTimeout, () => gossipReader.GossipEnd);
                if (timeout)
                {
                    LogWarn($"Gossip - {nameof(gossipReader.GossipEnd)} not fired after {gossipEndElapsedMs}ms");
                    return false;
                }
                else
                {
                    if (gossipReader.Gossips.TryGetValue(Gossip.Vendor, out int orderNum))
                    {
                        Log($"Picked {orderNum}th for {Gossip.Vendor}");
                        execGameCommand.Run($"/run SelectGossipOption({orderNum})--");
                    }
                    else
                    {
                        LogWarn($"Target({playerReader.TargetId}) has no {Gossip.Vendor} option!");
                        return false;
                    }
                }
            }

            Log($"Merchant window opened after {elapsedMs}ms");

            (bool sellStartedTimeout, double sellStartedElapsedMs) = wait.Until(GossipTimeout, () => gossipReader.MerchantWindowSelling);
            if (!sellStartedTimeout)
            {
                Log($"Merchant sell grey items started after {sellStartedElapsedMs}ms");

                (bool sellFinishedTimeout, double sellFinishedElapsedMs) = wait.Until(GossipTimeout, () => gossipReader.MerchantWindowSellingFinished);
                if (!sellFinishedTimeout)
                {
                    Log($"Merchant sell grey items finished, took {sellFinishedElapsedMs}ms");
                    return true;
                }
                else
                {
                    Log($"Merchant sell grey items timeout! Too many items to sell?! Increase {nameof(GossipTimeout)} - {sellFinishedElapsedMs}ms");
                    return true;
                }
            }
            else
            {
                Log($"Merchant sell nothing! {sellStartedElapsedMs}ms");
                return true;
            }
        }


        private void Log(string text)
        {
            logger.LogInformation($"[{nameof(AdhocNPCGoal)}]: {text}");
        }

        private void LogDebug(string text)
        {
            if(debug)
                logger.LogDebug($"[{nameof(AdhocNPCGoal)}]: {text}");
        }

        private void LogWarn(string text)
        {
            logger.LogWarning($"[{nameof(AdhocNPCGoal)}]: {text}");
        }
    }
}