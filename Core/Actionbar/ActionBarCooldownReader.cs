﻿using System;
using System.Collections.Generic;

namespace Core
{
    public class ActionBarCooldownReader
    {
        private readonly SquareReader reader;
        private readonly int cActionbarNum;

        private readonly float MAX_ACTION_IDX = 100000f;
        private readonly float MAX_VALUE_MUL = 100f;

        private readonly Dictionary<int, (int duration, DateTime startTime)> dict = new();

        public ActionBarCooldownReader(SquareReader reader, int cActionbarNum)
        {
            this.reader = reader;
            this.cActionbarNum = cActionbarNum;
        }

        public void Read()
        {
            // formula
            // MAX_ACTION_IDX * index + (cooldown / MAX_VALUE_MUL)
            float newCooldown = reader.GetInt(cActionbarNum);
            if (newCooldown == 0) return;

            int index = (int)(newCooldown / MAX_ACTION_IDX);
            newCooldown -= (int)MAX_ACTION_IDX * index;

            newCooldown /= MAX_VALUE_MUL;

            if (dict.TryGetValue(index, out var tuple) && tuple.duration != (int)newCooldown)
            {
                dict.Remove(index);
            }

            dict.TryAdd(index, ((int)newCooldown, DateTime.UtcNow));
        }

        public void Reset()
        {
            dict.Clear();
        }

        public int GetRemainingCooldown(PlayerReader playerReader, KeyAction keyAction)
        {
            if (KeyReader.ActionBarSlotMap.TryGetValue(keyAction.Key, out int slot))
            {
                if (slot <= 12)
                {
                    slot += Stance.RuntimeSlotToActionBar(keyAction, playerReader, slot);
                }

                if (dict.TryGetValue(slot, out var tuple))
                {
                    return tuple.duration == 0
                        ? 0
                        : Math.Clamp((int)(tuple.startTime.AddSeconds(tuple.duration) - DateTime.UtcNow).TotalMilliseconds, 0, int.MaxValue);
                }
            }

            return 0;
        }

    }
}
