﻿namespace Core
{
    public class BuffStatus : BitStatus
    {
        public BuffStatus(int value) : base(value)
        {
        }

        // All
        public bool Eating => IsBitSet(0);
        public bool Drinking => IsBitSet(1);
        public bool WellFed => IsBitSet(2);
        public bool ManaRegeneration => IsBitSet(3);
        public bool Clearcasting => IsBitSet(4);

        // Priest
        public bool Fortitude => IsBitSet(10);
        public bool InnerFire => IsBitSet(11);
        public bool Renew => IsBitSet(12);
        public bool Shield => IsBitSet(13);
        public bool DivineSpirit => IsBitSet(14);

        // Druid
        public bool MarkOfTheWild => IsBitSet(10);
        public bool Thorns => IsBitSet(11);
        public bool TigersFury => IsBitSet(12);
        public bool Prowl => IsBitSet(13);
        public bool Rejuvenation => IsBitSet(14);
        public bool Regrowth => IsBitSet(15);

        // Paladin
        public bool Aura => IsBitSet(10);
        public bool Blessing => IsBitSet(11);
        public bool Seal => IsBitSet(12);

        // Mage
        public bool FrostArmor => IsBitSet(10);
        public bool ArcaneIntellect => IsBitSet(11);
        public bool IceBarrier => IsBitSet(12);
        public bool Ward => IsBitSet(13);
        public bool FirePower => IsBitSet(14);
        public bool ManaShield => IsBitSet(15);
        public bool PresenceOfMind => IsBitSet(16);
        public bool ArcanePower => IsBitSet(17);

        // Rogue
        public bool SliceAndDice => IsBitSet(10);
        public bool Stealth => IsBitSet(11);

        // Warrior
        public bool BattleShout => IsBitSet(10);

        // Warlock
        public bool Demon => IsBitSet(10); //Skin and Armor
        public bool SoulLink => IsBitSet(11);
        public bool SoulstoneResurrection => IsBitSet(12);
        public bool ShadowTrance => IsBitSet(13);

        // Shaman
        public bool LightningShield => IsBitSet(10);
        public bool WaterShield => IsBitSet(11);
        public bool ShamanisticFocus => IsBitSet(12);
        public bool Stoneskin => IsBitSet(13);

        // Hunter
        public bool Aspect => IsBitSet(10); //Any Aspect of
        public bool RapidFire => IsBitSet(11);
        public bool QuickShots => IsBitSet(12);
    }
}