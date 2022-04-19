﻿using Core.GOAP;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class ApproachTargetGoal : GoapGoal
    {
        public override float CostOfPerformingAction { get => 8f; }

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly MountHandler mountHandler;

        private const bool debug = true;

        private readonly Random random = new();

        private DateTime approachStart;

        private bool playerWasInCombat;
        private float lastPlayerDistance;
        private Vector3 lastPlayerLocation;

        private int initialTargetGuid;
        private float initialMinRange;

        private int SecondsSinceApproachStarted => (int)(DateTime.UtcNow - approachStart).TotalSeconds;

        private bool HasPickedUpAnAdd
        {
            get
            {
                return playerReader.Bits.PlayerInCombat && !playerReader.Bits.TargetOfTargetIsPlayer;
            }
        }

        public ApproachTargetGoal(ILogger logger, ConfigurableInput input, Wait wait, PlayerReader playerReader, StopMoving stopMoving, MountHandler mountHandler)
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.playerReader = playerReader;
            this.stopMoving = stopMoving;
            this.mountHandler = mountHandler;

            lastPlayerDistance = 0;
            lastPlayerLocation = playerReader.PlayerLocation;

            initialTargetGuid = playerReader.TargetGuid;
            initialMinRange = 0;

            AddPrecondition(GoapKey.hastarget, true);
            AddPrecondition(GoapKey.targetisalive, true);
            AddPrecondition(GoapKey.incombatrange, false);

            AddEffect(GoapKey.incombatrange, true);
        }

        public override ValueTask OnEnter()
        {
            playerWasInCombat = playerReader.Bits.PlayerInCombat;

            initialTargetGuid = playerReader.TargetGuid;
            initialMinRange = playerReader.MinRange;

            approachStart = DateTime.UtcNow;

            return ValueTask.CompletedTask;
        }

        public override ValueTask PerformAction()
        {
            lastPlayerLocation = playerReader.PlayerLocation;
            wait.Update(1);

            if (!playerReader.Bits.PlayerInCombat)
            {
                playerWasInCombat = false;
            }
            else
            {
                // we are in combat
                if (!playerWasInCombat && HasPickedUpAnAdd)
                {
                    logger.LogInformation("WARN Bodypull -- Looks like we have an add on approach");
                    logger.LogInformation($"Combat={playerReader.Bits.PlayerInCombat}, Is Target targetting me={playerReader.Bits.TargetOfTargetIsPlayer}");

                    stopMoving.Stop();
                    input.ClearTarget();
                    wait.Update(1);

                    if (playerReader.PetHasTarget)
                    {
                        input.TargetPet();
                        input.TargetOfTarget();
                        wait.Update(1);
                    }
                }

                playerWasInCombat = true;
            }

            if (input.ClassConfig.Approach.GetCooldownRemaining() == 0)
            {
                input.Approach();
            }

            lastPlayerDistance = playerReader.PlayerLocation.DistanceXYTo(lastPlayerLocation);

            if (lastPlayerDistance < 0.05 && playerReader.LastUIErrorMessage == UI_ERROR.ERR_AUTOFOLLOW_TOO_FAR)
            {
                playerReader.LastUIErrorMessage = UI_ERROR.NONE;

                Log("Too far, start moving forward!");
                input.SetKeyState(input.ForwardKey, true);
                wait.Update(1);
            }

            if (SecondsSinceApproachStarted > 1 && lastPlayerDistance < 0.05 && !playerReader.Bits.PlayerInCombat)
            {
                input.ClearTarget();
                wait.Update(1);
                Log($"Seems stuck! Clear Target. Turn away. d: {lastPlayerDistance}");
                input.KeyPress(random.Next(2) == 0 ? input.TurnLeftKey : input.TurnRightKey, 1000);

                approachStart = DateTime.UtcNow;
            }

            if (SecondsSinceApproachStarted > 15 && !playerReader.Bits.PlayerInCombat)
            {
                input.ClearTarget();
                wait.Update(1);
                Log("Too long time. Clear Target. Turn away.");
                input.KeyPress(random.Next(2) == 0 ? input.TurnLeftKey : input.TurnRightKey, 1000);

                approachStart = DateTime.UtcNow;
            }

            if (playerReader.TargetGuid == initialTargetGuid)
            {
                var initialTargetMinRange = playerReader.MinRange;
                if (!playerReader.Bits.PlayerInCombat)
                {
                    if (input.ClassConfig.TargetNearestTarget.GetCooldownRemaining() == 0)
                    {
                        Log("Try to find closer target...");
                        input.NearestTarget();
                        wait.Update(1);
                    }
                }

                if (playerReader.TargetGuid != initialTargetGuid)
                {
                    if (playerReader.HasTarget) // blacklist
                    {
                        if (playerReader.MinRange < initialTargetMinRange)
                        {
                            Log($"Found a closer target! {playerReader.MinRange} < {initialTargetMinRange}");
                            initialMinRange = playerReader.MinRange;
                        }
                        else
                        {
                            initialTargetGuid = -1;
                            Log("Stick to initial target!");
                            input.LastTarget();
                            wait.Update(1);
                        }
                    }
                    else
                    {
                        Log($"Lost the target due blacklist!");
                    }
                }
            }

            if (initialMinRange < playerReader.MinRange && !playerReader.Bits.PlayerInCombat)
            {
                Log($"We are going away from the target! {initialMinRange} < {playerReader.MinRange}");
                input.ClearTarget();
                wait.Update(1);

                approachStart = DateTime.UtcNow;
            }

            RandomJump();

            return ValueTask.CompletedTask;
        }

        public override void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.resume)
            {
                approachStart = DateTime.UtcNow;
            }
        }

        private void RandomJump()
        {
            if ((DateTime.UtcNow - approachStart).TotalSeconds > 2 && input.ClassConfig.Jump.MillisecondsSinceLastClick > random.Next(5000, 25_000))
            {
                input.Jump();
            }
        }

        private void Log(string text)
        {
            if (debug)
            {
                logger.LogInformation($"{nameof(ApproachTargetGoal)}: {text}");
            }
        }

    }
}