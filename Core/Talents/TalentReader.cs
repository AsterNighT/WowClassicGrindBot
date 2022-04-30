﻿using System.Collections.Generic;
using Core.Talents;
using Core.Database;
using System.Linq;

namespace Core
{
    public class TalentReader
    {
        private readonly int cTalent;

        private readonly SquareReader reader;
        private readonly PlayerReader playerReader;
        private readonly TalentDB talentDB;
        public int Count => Talents.Sum(x => x.Value.CurrentRank);

        public Dictionary<int, Talent> Talents { get; } = new();

        public TalentReader(SquareReader reader, int cTalent, PlayerReader playerReader, TalentDB talentDB)
        {
            this.reader = reader;
            this.cTalent = cTalent;

            this.playerReader = playerReader;
            this.talentDB = talentDB;
        }

        public void Read()
        {
            int data = reader.GetInt(cTalent);
            if (data == 0 || Talents.ContainsKey(data)) return;

            int hash = data;

            int tab = (int)(data / 1000000f);
            data -= 1000000 * tab;

            int tier = (int)(data / 10000f);
            data -= 10000 * tier;

            int column = (int)(data / 10f);
            data -= 10 * column;

            var talent = new Talent
            {
                Hash = hash,
                TabNum = tab,
                TierNum = tier,
                ColumnNum = column,
                CurrentRank = data
            };

            if (talentDB.Update(ref talent, playerReader.Class))
            {
                Talents.Add(hash, talent);
            }
        }

        public void Reset()
        {
            Talents.Clear();
        }

        public bool HasTalent(string name, int rank)
        {
            foreach (var kvp in Talents)
            {
                if (!string.IsNullOrEmpty(kvp.Value.Name) &&
                    kvp.Value.Name.ToLower() == name.ToLower() &&
                    kvp.Value.CurrentRank >= rank)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
