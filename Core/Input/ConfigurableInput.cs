﻿using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Game;

namespace Core
{
    public class ConfigurableInput : WowProcessInput
    {
        public ClassConfiguration ClassConfig { private set; get; }

        private const int defaultKeyPress = 50;

        public ConfigurableInput(ILogger logger, WowProcess wowProcess, ClassConfiguration classConfig) : base(logger, wowProcess)
        {
            ClassConfig = classConfig;
        }

        public async ValueTask TapStopKey(string desc = "")
        {
            await KeyPress(ConsoleKey.UpArrow, defaultKeyPress, $"TapStopKey: {desc}");
        }

        public async ValueTask TapInteractKey(string source)
        {
            await KeyPress(ClassConfig.Interact.ConsoleKey, defaultKeyPress, string.IsNullOrEmpty(source) ? "" : $"TapInteract ({source})");
            this.ClassConfig.Interact.SetClicked();
        }

        public async ValueTask TapApproachKey(string source)
        {
            await KeyPress(ClassConfig.Approach.ConsoleKey, ClassConfig.Approach.PressDuration, string.IsNullOrEmpty(source) ? "" : $"TapApproachKey ({source})");
            this.ClassConfig.Approach.SetClicked();
        }

        public async ValueTask TapLastTargetKey(string source)
        {
            await KeyPress(ClassConfig.TargetLastTarget.ConsoleKey, defaultKeyPress, $"TapLastTarget ({source})");
            this.ClassConfig.TargetLastTarget.SetClicked();
        }

        public async ValueTask TapStandUpKey(string desc = "")
        {
            await KeyPress(ClassConfig.StandUp.ConsoleKey, defaultKeyPress, $"TapStandUpKey: {desc}");
            this.ClassConfig.StandUp.SetClicked();
        }

        public async ValueTask TapClearTarget(string desc = "")
        {
            await KeyPress(ClassConfig.ClearTarget.ConsoleKey, defaultKeyPress, string.IsNullOrEmpty(desc) ? "" : $"TapClearTarget: {desc}");
            this.ClassConfig.ClearTarget.SetClicked();
        }

        public async ValueTask TapStopAttack(string desc = "")
        {
            await KeyPress(ClassConfig.StopAttack.ConsoleKey, ClassConfig.StopAttack.PressDuration, string.IsNullOrEmpty(desc) ? "" : $"TapStopAttack: {desc}");
            this.ClassConfig.StopAttack.SetClicked();
        }

        public async ValueTask TapNearestTarget(string desc = "")
        {
            await KeyPress(ClassConfig.TargetNearestTarget.ConsoleKey, defaultKeyPress, $"TapNearestTarget: {desc}");
            this.ClassConfig.TargetNearestTarget.SetClicked();
        }

        public async ValueTask TapTargetPet(string desc = "")
        {
            await KeyPress(ClassConfig.TargetPet.ConsoleKey, defaultKeyPress, $"TapTargetPet: {desc}");
            this.ClassConfig.TargetPet.SetClicked();
        }

        public async ValueTask TapTargetOfTarget(string desc = "")
        {
            await KeyPress(ClassConfig.TargetTargetOfTarget.ConsoleKey, defaultKeyPress, $"TapTargetsTarget: {desc}");
            this.ClassConfig.TargetTargetOfTarget.SetClicked();
        }

        public async ValueTask TapJump(string desc = "")
        {
            await KeyPress(ClassConfig.Jump.ConsoleKey, defaultKeyPress, $"TapJump: {desc}");
            this.ClassConfig.Jump.SetClicked();
        }

        public async ValueTask TapPetAttack(string source = "")
        {
            await KeyPress(ClassConfig.PetAttack.ConsoleKey, ClassConfig.PetAttack.PressDuration, $"TapPetAttack ({source})");
            this.ClassConfig.PetAttack.SetClicked();
        }

        public async ValueTask TapHearthstone()
        {
            await KeyPress(ConsoleKey.I, defaultKeyPress, "TapHearthstone");
        }

        public async ValueTask TapMount()
        {
            await KeyPress(ClassConfig.Mount.ConsoleKey, defaultKeyPress, "TapMount");
            this.ClassConfig.Mount.SetClicked();
        }

        public async ValueTask TapDismount()
        {
            await KeyPress(ClassConfig.Mount.ConsoleKey, defaultKeyPress, "TapDismount");
        }
    }
}
