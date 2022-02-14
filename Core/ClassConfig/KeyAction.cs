﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Core
{
    public partial class KeyAction
    {
        public string Name { get; set; } = string.Empty;
        public bool HasCastBar { get; set; }
        public bool StopBeforeCast { get; set; }
        public ConsoleKey ConsoleKey { get; set; }
        public string Key { get; set; } = string.Empty;
        public int PressDuration { get; set; } = 50;
        public string Form { get; set; } = string.Empty;
        public Form FormEnum { get; set; } = Core.Form.None;
        public float Cooldown { get; set; }

        private int _charge;
        public int Charge { get; set; } = 1;
        public SchoolMask School { get; set; } = SchoolMask.None;
        public int MinMana { get; set; }
        public int MinRage { get; set; }
        public int MinEnergy { get; set; }
        public int MinComboPoints { get; set; }

        public string Requirement { get; set; } = string.Empty;
        public List<string> Requirements { get; } = new List<string>();

        public bool WhenUsable { get; set; }

        public bool WaitForWithinMeleeRange { get; set; }
        public bool ResetOnNewTarget { get; set; }

        public bool Log { get; set; } = true;
        public int DelayAfterCast { get; set; } = 1450; // GCD 1500 - but spell queue window 400 ms

        public bool WaitForGCD { get; set; } = true;

        public bool SkipValidation { get; set; }

        public bool AfterCastWaitBuff { get; set; }

        public bool AfterCastWaitNextSwing { get; set; }

        public bool DelayUntilCombat { get; set; }
        public int DelayBeforeCast { get; set; }
        public float Cost { get; set; } = 18;
        public string InCombat { get; set; } = "false";

        public bool? UseWhenTargetIsCasting { get; set; }

        public string PathFilename { get; set; } = string.Empty;
        public List<Vector3> Path { get; } = new List<Vector3>();

        public int StepBackAfterCast { get; set; }

        public Vector3 LastClickPostion { get; private set; }

        public List<Requirement> RequirementObjects { get; } = new List<Requirement>();

        public int ConsoleKeyFormHash { private set; get; }

        protected static ConcurrentDictionary<int, DateTime> LastClicked { get; } = new ConcurrentDictionary<int, DateTime>();

        public static int LastKeyClicked()
        {
            var last = LastClicked.OrderByDescending(s => s.Value).FirstOrDefault();
            if (last.Key == 0 || (DateTime.UtcNow - last.Value).TotalSeconds > 2)
            {
                return (int)ConsoleKey.NoName;
            }
            return last.Key;
        }

        private PlayerReader playerReader = null!;

        private ILogger logger = null!;

        public void InitDynamicBinding(RequirementFactory requirementFactory)
        {
            requirementFactory.InitDynamicBindings(this);
        }

        public void Initialise(AddonReader addonReader, RequirementFactory requirementFactory, ILogger logger, KeyActions? keyActions = null)
        {
            this.playerReader = addonReader.PlayerReader;
            this.logger = logger;

            ResetCharges();

            if (!KeyReader.ReadKey(logger, this))
            {
                throw new Exception($"[{Name}] has no valid Key={ConsoleKey}");
            }

            if (!string.IsNullOrEmpty(this.Requirement))
            {
                Requirements.Add(this.Requirement);
            }

            if (HasFormRequirement())
            {
                if (Enum.TryParse(Form, out Form desiredForm))
                {
                    this.FormEnum = desiredForm;
                    this.logger.LogInformation($"[{Name}] Required Form: {FormEnum}");

                    if (KeyReader.ActionBarSlotMap.TryGetValue(Key, out int slot))
                    {
                        int offset = Stance.RuntimeSlotToActionBar(this, playerReader, slot);
                        this.logger.LogInformation($"[{Name}] Actionbar Form key map: Key:{Key} -> Actionbar:{slot} -> Form Map:{slot + offset}");
                    }
                }
                else
                {
                    throw new Exception($"[{Name}] Unknown form: {Form}");
                }
            }

            ConsoleKeyFormHash = ((int)FormEnum * 1000) + (int)ConsoleKey;

            InitMinPowerType(playerReader, addonReader.ActionBarCostReader);

            requirementFactory.InitialiseRequirements(this, keyActions);
        }

        public void InitialiseForm(AddonReader addonReader, RequirementFactory requirementFactory, ILogger logger)
        {
            Initialise(addonReader, requirementFactory, logger);

            if (HasFormRequirement())
            {
                if (addonReader.PlayerReader.FormCost.ContainsKey(FormEnum))
                {
                    addonReader.PlayerReader.FormCost.Remove(FormEnum);
                }

                addonReader.PlayerReader.FormCost.Add(FormEnum, MinMana);
                logger.LogInformation($"[{Name}] Added {FormEnum} to FormCost with {MinMana}");
            }
        }

        public float GetCooldownRemaining()
        {
            var remain = MillisecondsSinceLastClick;
            if (remain == double.MaxValue) return 0;
            return MathF.Max(Cooldown - (float)remain, 0);
        }

        public bool CanDoFormChangeAndHaveMinimumMana()
        {
            return playerReader.FormCost.ContainsKey(FormEnum) &&
                playerReader.ManaCurrent >= playerReader.FormCost[FormEnum] + MinMana;
        }

        internal void SetClicked()
        {
            LastClickPostion = playerReader.PlayerLocation;

            if (!LastClicked.TryAdd(ConsoleKeyFormHash, DateTime.UtcNow))
            {
                LastClicked[ConsoleKeyFormHash] = DateTime.UtcNow;
            }
        }

        public double MillisecondsSinceLastClick =>
            LastClicked.TryGetValue(ConsoleKeyFormHash, out DateTime lastTime) ?
            (DateTime.UtcNow - lastTime).TotalMilliseconds :
            double.MaxValue;

        internal void ResetCooldown()
        {
            LastClicked.TryRemove(ConsoleKeyFormHash, out _);
        }

        public int GetChargeRemaining()
        {
            return _charge;
        }

        public void ConsumeCharge()
        {
            if (Charge > 1)
            {
                _charge--;
                if (_charge > 0)
                {
                    ResetCooldown();
                }
                else
                {
                    ResetCharges();
                    SetClicked();
                }
            }
        }

        internal void ResetCharges()
        {
            _charge = Charge;
        }

        public bool CanRun()
        {
            return !this.RequirementObjects.Any(r => !r.HasRequirement());
        }

        public bool HasFormRequirement()
        {
            return !string.IsNullOrEmpty(Form);
        }

        private void InitMinPowerType(PlayerReader playerReader, ActionBarCostReader actionBarCostReader)
        {
            var (type, cost) = actionBarCostReader.GetCostByActionBarSlot(playerReader, this);
            if (cost != 0)
            {
                int oldValue = 0;
                switch (type)
                {
                    case PowerType.Mana:
                        oldValue = MinMana;
                        MinMana = cost;
                        break;
                    case PowerType.Rage:
                        oldValue = MinRage;
                        MinRage = cost;
                        break;
                    case PowerType.Energy:
                        oldValue = MinEnergy;
                        MinEnergy = cost;
                        break;
                }

                int formCost = 0;
                if (HasFormRequirement() && FormEnum != Core.Form.None && playerReader.FormCost.ContainsKey(FormEnum))
                {
                    formCost = playerReader.FormCost[FormEnum];
                }

                logger.LogInformation($"[{Name}] Update {type} cost to {cost} from {oldValue}" + (formCost > 0 ? $" +{formCost} Mana to change {FormEnum} Form" : ""));
            }

            actionBarCostReader.OnActionCostChanged -= ActionBarCostReader_OnActionCostChanged;
            actionBarCostReader.OnActionCostChanged += ActionBarCostReader_OnActionCostChanged;
        }

        private void ActionBarCostReader_OnActionCostChanged(object? sender, ActionBarCostEventArgs e)
        {
            if (!KeyReader.ActionBarSlotMap.TryGetValue(Key, out int slot)) return;

            if (slot <= 12)
            {
                slot += Stance.RuntimeSlotToActionBar(this, playerReader, slot);
            }

            if (slot == e.index)
            {
                int oldValue = 0;
                switch (e.powerType)
                {
                    case PowerType.Mana:
                        oldValue = MinMana;
                        MinMana = e.cost;
                        break;
                    case PowerType.Rage:
                        oldValue = MinRage;
                        MinRage = e.cost;
                        break;
                    case PowerType.Energy:
                        oldValue = MinEnergy;
                        MinEnergy = e.cost;
                        break;
                }

                if (e.cost != oldValue)
                {
                    LogPowerCostChange(logger, Name, e.powerType, e.cost, oldValue);
                }
            }
        }

        #region Logging

        public void LogInformation(string message)
        {
            if (Log)
            {
                logger.LogInformation($"[{Name}]: {message}");
            }
        }

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Information,
            Message = "[{name}] Update {type} cost to {newCost} from {oldCost}")]
        static partial void LogPowerCostChange(ILogger logger, string name, PowerType type, int newCost, int oldCost);

        #endregion
    }
}