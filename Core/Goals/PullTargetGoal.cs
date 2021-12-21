using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class PullTargetGoal : GoapGoal
    {
        public override float CostOfPerformingAction { get => 7f; }

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly StuckDetector stuckDetector;
        private readonly ClassConfiguration classConfiguration;
        
        private readonly CastingHandler castingHandler;
        private readonly MountHandler mountHandler;

        private readonly Random random = new Random(DateTime.Now.Millisecond);

        private DateTime pullStart = DateTime.Now;

        private int SecondsSincePullStarted => (int)(DateTime.Now - pullStart).TotalSeconds;

        public PullTargetGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, StopMoving stopMoving, CastingHandler castingHandler, MountHandler mountHandler, StuckDetector stuckDetector, ClassConfiguration classConfiguration)
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.stopMoving = stopMoving;
            
            this.castingHandler = castingHandler;
            this.mountHandler = mountHandler;

            this.stuckDetector = stuckDetector;
            this.classConfiguration = classConfiguration;

            classConfiguration.Pull.Sequence.Where(k => k != null).ToList().ForEach(key => Keys.Add(key));

            AddPrecondition(GoapKey.targetisalive, true);
            AddPrecondition(GoapKey.incombat, false);
            AddPrecondition(GoapKey.hastarget, true);
            AddPrecondition(GoapKey.pulled, false);
            AddPrecondition(GoapKey.withinpullrange, true);

            AddEffect(GoapKey.pulled, true);
        }

        public override async ValueTask OnEnter()
        {
            await base.OnEnter();

            if (mountHandler.IsMounted())
            {
                await mountHandler.Dismount();
            }

            await input.TapApproachKey($"{GetType().Name}: OnEnter - Face the target and stop");
            await stopMoving.Stop();
            await wait.Update(1);

            pullStart = DateTime.Now;
        }

        public override void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.resume)
            {
                pullStart = DateTime.Now;
            }
        }

        public override async ValueTask PerformAction()
        {
            if (SecondsSincePullStarted > 7)
            {
                await input.TapClearTarget();
                await input.KeyPress(random.Next(2) == 0 ? ConsoleKey.LeftArrow : ConsoleKey.RightArrow, 1000, "Too much time to pull!");
                pullStart = DateTime.Now;

                return;
            }

            SendActionEvent(new ActionEventArgs(GoapKey.fighting, true));

            if (!await Pull())
            {
                if (HasPickedUpAnAdd)
                {
                    Log($"Combat={this.playerReader.Bits.PlayerInCombat}, Is Target targetting me={this.playerReader.Bits.TargetOfTargetIsPlayer}");
                    Log($"Add on approach");

                    await stopMoving.Stop();

                    await input.TapNearestTarget();
                    await wait.Update(1);

                    if (this.playerReader.HasTarget && playerReader.Bits.TargetInCombat)
                    {
                        if (this.playerReader.TargetTarget == TargetTargetEnum.TargetIsTargettingMe)
                        {
                            return;
                        }
                    }

                    await input.TapClearTarget();
                    await wait.Update(1);
                    pullStart = DateTime.Now;

                    return;
                }

                if (!stuckDetector.IsMoving())
                {
                    await stuckDetector.Unstick();
                }

                if (classConfiguration.Approach.GetCooldownRemaining() == 0)
                {
                    await input.TapApproachKey($"{GetType().Name}");
                    await wait.Update(1);
                }
            }
            else
            {
                SendActionEvent(new ActionEventArgs(GoapKey.pulled, true));
            }
        }

        protected bool HasPickedUpAnAdd
        {
            get
            {
                return this.playerReader.Bits.PlayerInCombat && !this.playerReader.Bits.TargetOfTargetIsPlayer && this.playerReader.HealthPercent > 98;
            }
        }

        protected async Task WaitForWithinMeleeRange(KeyAction item, bool lastCastSuccess)
        {
            await stopMoving.Stop();
            await wait.Update(1);

            var start = DateTime.Now;
            var lastKnownHealth = playerReader.HealthCurrent;
            int maxWaitTime = 10;

            Log($"Waiting for the target to reach melee range - max {maxWaitTime}s");

            while (playerReader.HasTarget && !playerReader.IsInMeleeRange && (DateTime.Now - start).TotalSeconds < maxWaitTime)
            {
                if (playerReader.HealthCurrent < lastKnownHealth)
                {
                    Log("Got damage. Stop waiting for melee range.");
                    break;
                }

                if (playerReader.IsTargetCasting)
                {
                    Log("Target started casting. Stop waiting for melee range.");
                    break;
                }

                if (lastCastSuccess && addonReader.UsableAction.Is(item))
                {
                    Log($"While waiting, repeat current action: {item.Name}");
                    lastCastSuccess = await castingHandler.CastIfReady(item, item.DelayBeforeCast);
                    Log($"Repeat current action: {lastCastSuccess}");
                }

                await wait.Update(1);
            }
        }

        public async Task<bool> Pull()
        {
            if (Keys.Count != 0)
            {
                await input.TapStopAttack();
                await wait.Update(1);
            }

            if (playerReader.Bits.HasPet && !playerReader.PetHasTarget)
            {
                await input.TapPetAttack();
            }

            bool castAny = false;
            foreach (var item in Keys)
            {
                var success = await castingHandler.CastIfReady(item, item.DelayBeforeCast);
                if (success)
                {
                    if (!playerReader.HasTarget)
                    {
                        return false;
                    }

                    castAny = true;

                    if (item.WaitForWithinMeleeRange)
                    {
                        await WaitForWithinMeleeRange(item, success);
                    }
                }
            }

            if (castAny)
            {
                (bool interrupted, double elapsedMs) = await wait.InterruptTask(1000,
                    () => playerReader.TargetTarget == TargetTargetEnum.TargetIsTargettingMe ||
                          playerReader.TargetTarget == TargetTargetEnum.TargetIsTargettingPet);
                if (!interrupted)
                {
                    Log($"Entered combat after {elapsedMs}ms");
                }
            }

            return playerReader.Bits.PlayerInCombat;
        }

        private void Log(string s)
        {
            logger.LogInformation($"{GetType().Name}: {s}");
        }
    }
}