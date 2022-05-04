﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Core
{
    public class BadZone
    {
        public int ZoneId { get; set; } = -1;
        public Vector3 ExitZoneLocation { get; set; }
    }

    public enum Mode
    {
        Grind = 0,
        CorpseRun = 1,
        AttendedGather = 2,
        AttendedGrind = 3
    }


    public class ClassConfiguration : IDisposable
    {
        public bool Log { get; set; } = true;

        public string ClassName { get; set; } = string.Empty;
        public bool Loot { get; set; } = true;
        public bool Skin { get; set; }
        public bool UseMount { get; set; } = true;
        public bool KeyboardOnly { get; set; }

        public string PathFilename { get; set; } = string.Empty;

        public string? OverridePathFilename { get; set; } = string.Empty;

        public bool PathThereAndBack { get; set; } = true;
        public bool PathReduceSteps { get; set; }

        public Mode Mode { get; set; } = Mode.Grind;

        public BadZone WrongZone { get; set; } = new BadZone();

        public int NPCMaxLevels_Above { get; set; } = 1;
        public int NPCMaxLevels_Below { get; set; } = 7;

        public bool CheckTargetGivesExp { get; set; }
        public List<string> Blacklist { get; } = new List<string>();

        public Dictionary<int, List<SchoolMask>> ImmunityBlacklist { get; } = new();

        public Dictionary<string, int> IntVariables { get; } = new();

        public KeyActions Pull { get; set; } = new();
        public KeyActions Combat { get; set; } = new();
        public KeyActions Adhoc { get; set; } = new();
        public KeyActions Parallel { get; set; } = new();
        public KeyActions NPC { get; set; } = new();

        public List<KeyAction> Form { get; } = new();
        public List<KeyAction> GatherFindKeyConfig { get; } = new();
        public List<string> GatherFindKeys { get; } = new();

        public KeyAction Jump { get; set; } = new();
        public string JumpKey { get; set; } = "Spacebar";

        public KeyAction Interact { get; set; } = new();
        public string InteractKey { get; set; } = "I";

        public KeyAction Approach { get; set; } = new();
        public KeyAction AutoAttack { get; set; } = new();

        public KeyAction TargetLastTarget { get; set; } = new();
        public string TargetLastTargetKey { get; set; } = "G";

        public KeyAction StandUp { get; set; } = new();
        public string StandUpKey { get; set; } = "X";

        public KeyAction ClearTarget { get; set; } = new();
        public string ClearTargetKey { get; set; } = "Insert";

        public KeyAction StopAttack { get; set; } = new();
        public string StopAttackKey { get; set; } = "Delete";

        public KeyAction TargetNearestTarget { get; set; } = new();
        public string TargetNearestTargetKey { get; set; } = "Tab";

        public KeyAction TargetTargetOfTarget { get; set; } = new();
        public string TargetTargetOfTargetKey { get; set; } = "F";
        public KeyAction TargetPet { get; set; } = new();
        public string TargetPetKey { get; set; } = "Multiply";

        public KeyAction PetAttack { get; set; } = new();
        public string PetAttackKey { get; set; } = "Subtract";

        public KeyAction Mount { get; set; } = new();
        public string MountKey { get; set; } = "O";

        public KeyAction Hearthstone { get; set; } = new();
        public string HearthstoneKey { get; set; } = "I";

        public ConsoleKey ForwardKey { get; set; } = ConsoleKey.UpArrow;  // 38
        public ConsoleKey BackwardKey { get; set; } = ConsoleKey.DownArrow; // 40
        public ConsoleKey TurnLeftKey { get; set; } = ConsoleKey.LeftArrow; // 37
        public ConsoleKey TurnRightKey { get; set; } = ConsoleKey.RightArrow; // 39

        public void Initialise(DataConfig dataConfig, AddonReader addonReader, RequirementFactory requirementFactory, ILogger logger, string? overridePathProfileFile)
        {
            requirementFactory.InitUserDefinedIntVariables(IntVariables);

            Jump.Key = JumpKey;
            Jump.Initialise(addonReader, requirementFactory, logger, Log);

            TargetLastTarget.Key = TargetLastTargetKey;
            TargetLastTarget.Initialise(addonReader, requirementFactory, logger, Log);

            StandUp.Key = StandUpKey;
            StandUp.Initialise(addonReader, requirementFactory, logger, Log);

            ClearTarget.Key = ClearTargetKey;
            ClearTarget.Initialise(addonReader, requirementFactory, logger, Log);

            StopAttack.Key = StopAttackKey;
            StopAttack.Initialise(addonReader, requirementFactory, logger, Log);
            StopAttack.PressDuration = 10;
            StopAttack.Cooldown = 400;

            TargetNearestTarget.Key = TargetNearestTargetKey;
            TargetNearestTarget.Cooldown = 400;
            TargetNearestTarget.Initialise(addonReader, requirementFactory, logger, Log);

            TargetPet.Key = TargetPetKey;
            TargetPet.Initialise(addonReader, requirementFactory, logger, Log);

            TargetTargetOfTarget.Key = TargetTargetOfTargetKey;
            TargetTargetOfTarget.Initialise(addonReader, requirementFactory, logger, Log);

            PetAttack.Key = PetAttackKey;
            PetAttack.PressDuration = 10;
            PetAttack.Cooldown = 400;
            PetAttack.Initialise(addonReader, requirementFactory, logger, Log);

            Mount.Key = MountKey;
            Mount.Initialise(addonReader, requirementFactory, logger, Log);

            Hearthstone.Key = HearthstoneKey;
            Hearthstone.Initialise(addonReader, requirementFactory, logger, Log);

            Interact.Key = InteractKey;
            Interact.Name = "Interact";
            Interact.WaitForGCD = false;
            Interact.DelayAfterCast = 0;
            Interact.PressDuration = 30;
            Interact.SkipValidation = true;
            Interact.Initialise(addonReader, requirementFactory, logger, Log);

            Approach.Key = InteractKey;
            Approach.Name = "Approach";
            Approach.WaitForGCD = false;
            Approach.DelayAfterCast = 0;
            Approach.PressDuration = 10;
            Approach.Cooldown = 400;
            Approach.SkipValidation = true;
            Approach.Initialise(addonReader, requirementFactory, logger, Log);

            AutoAttack.Key = InteractKey;
            AutoAttack.Name = "AutoAttack";
            AutoAttack.WaitForGCD = false;
            AutoAttack.DelayAfterCast = 0;
            AutoAttack.SkipValidation = true;
            AutoAttack.Initialise(addonReader, requirementFactory, logger, Log);

            StopAttack.Name = "StopAttack";
            StopAttack.WaitForGCD = false;
            StopAttack.PressDuration = 20;
            StopAttack.SkipValidation = true;

            InitializeKeyActions(Pull, Interact, Approach, AutoAttack, StopAttack);
            InitializeKeyActions(Combat, Interact, Approach, AutoAttack, StopAttack);

            logger.LogInformation($"[{nameof(Form)}] Initialise KeyActions.");
            Form.ForEach(i => i.InitialiseForm(addonReader, requirementFactory, logger, Log));

            Pull.PreInitialise(nameof(Pull), requirementFactory, logger);
            Combat.PreInitialise(nameof(Combat), requirementFactory, logger);
            Adhoc.PreInitialise(nameof(Adhoc), requirementFactory, logger);
            NPC.PreInitialise(nameof(NPC), requirementFactory, logger);
            Parallel.PreInitialise(nameof(Parallel), requirementFactory, logger);

            Pull.Initialise(nameof(Pull), addonReader, requirementFactory, logger, Log);
            Combat.Initialise(nameof(Combat), addonReader, requirementFactory, logger, Log);
            Adhoc.Initialise(nameof(Adhoc), addonReader, requirementFactory, logger, Log);
            NPC.Initialise(nameof(NPC), addonReader, requirementFactory, logger, Log);
            Parallel.Initialise(nameof(Parallel), addonReader, requirementFactory, logger, Log);

            GatherFindKeys.ForEach(key =>
            {
                GatherFindKeyConfig.Add(new KeyAction { Key = key, Name = $"Profession {GatherFindKeys.IndexOf(key)}" });
                GatherFindKeyConfig.Last().Initialise(addonReader, requirementFactory, logger, Log);
            });

            OverridePathFilename = overridePathProfileFile;
            if (!string.IsNullOrEmpty(OverridePathFilename))
            {
                PathFilename = OverridePathFilename;
            }

            if (!File.Exists(Path.Join(dataConfig.Path, PathFilename)))
            {
                if (!string.IsNullOrEmpty(OverridePathFilename))
                    throw new Exception($"The `{OverridePathFilename}` path file does not exists!");
                else
                    throw new Exception($"The loaded class config contains not existing `{PathFilename}` path file!");
            }

            CheckConfigConsistency(logger);
        }

        public void Dispose()
        {
            Pull.Dispose();
            Combat.Dispose();
            Parallel.Dispose();
            Adhoc.Dispose();
            NPC.Dispose();
        }

        private void CheckConfigConsistency(ILogger logger)
        {
            if (CheckTargetGivesExp)
            {
                logger.LogWarning("CheckTargetGivesExp is enabled. NPCMaxLevels_Above and NPCMaxLevels_Below will be ignored.");
            }
            if (KeyboardOnly)
            {
                logger.LogWarning("KeyboardOnly mode is enabled. The bot will not try to utilize your mouse. Skin will be disabled and the npc target function will be limited.");
                Skin = false;
            }
        }

        private static void InitializeKeyActions(KeyActions userActions, params KeyAction[] defaultActions)
        {
            KeyAction dummyDefault = new KeyAction();
            var defaults = defaultActions.ToList();

            userActions.Sequence.ForEach(user =>
            {
                defaults.ForEach(@default =>
                {
                    if (user.Name == @default.Name)
                    {
                        user.Key = @default.Key;
                        user.WaitForGCD = @default.WaitForGCD;

                        //if (!string.IsNullOrEmpty(@default.Requirement))
                        //    user.Requirement += " " + @default.Requirement;
                        //user.Requirements.AddRange(@default.Requirements);

                        if (user.DelayAfterCast == dummyDefault.DelayAfterCast)
                            user.DelayAfterCast = @default.DelayAfterCast;

                        if (user.DelayAfterCast == dummyDefault.DelayAfterCast)
                            user.PressDuration = @default.PressDuration;

                        if (user.Cooldown == dummyDefault.Cooldown)
                            user.Cooldown = @default.Cooldown;

                        if (user.SkipValidation == dummyDefault.SkipValidation)
                            user.SkipValidation = @default.SkipValidation;
                    }
                });
            });
        }
    }
}