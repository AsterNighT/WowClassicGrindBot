﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using SharedLib;

namespace ReadDBC_CSV
{
    public class SpellExtractor : IExtractor
    {
        private readonly string path;

        public List<string> FileRequirement { get; } = new List<string>
        {
            "spellname.csv",
            "spelllevels.csv",
        };

        public SpellExtractor(string path)
        {
            this.path = path;
        }

        public void Run()
        {
            var spellname = Path.Join(path, FileRequirement[0]);
            var spells = ExtractNames(spellname);

            var spelllevels = Path.Join(path, FileRequirement[1]);
            ExtractLevels(spelllevels, spells);

            Console.WriteLine($"Spells: {spells.Count}");

            File.WriteAllText(Path.Join(path, "spells.json"), JsonConvert.SerializeObject(spells));
        }

        private static List<Spell> ExtractNames(string path)
        {
            int entryIndex = -1;
            int nameIndex = -1;

            var extractor = new CSVExtractor();
            extractor.HeaderAction = () =>
            {
                entryIndex = extractor.FindIndex("ID");
                nameIndex = extractor.FindIndex("Name_lang");
            };

            var spells = new List<Spell>();
            void extractLine(string[] values)
            {
                if (values.Length > entryIndex && values.Length > nameIndex)
                {
                    spells.Add(new Spell
                    {
                        Id = int.Parse(values[entryIndex]),
                        Name = values[nameIndex]
                    });
                }
            }

            extractor.ExtractTemplate(path, extractLine);

            return spells;
        }

        private static void ExtractLevels(string path, List<Spell> spells)
        {
            int entryIndex = -1;
            int spellIdIndex = -1;
            int baseLevelIndex = -1;

            var extractor = new CSVExtractor();
            extractor.HeaderAction = () =>
            {
                entryIndex = extractor.FindIndex("ID");
                spellIdIndex = extractor.FindIndex("SpellID", 6);
                baseLevelIndex = extractor.FindIndex("BaseLevel", 2);
            };

            void extractLine(string[] values)
            {
                if (values.Length > entryIndex &&
                    values.Length > spellIdIndex &&
                    values.Length > baseLevelIndex)
                {
                    int level = int.Parse(values[baseLevelIndex]);
                    if (level > 0 && int.TryParse(values[spellIdIndex], out int spellId))
                    {
                        int index = spells.FindIndex(0, x => x.Id == spellId);
                        if (index > -1)
                        {
                            spells[index] = new Spell
                            {
                                Id = spellId,
                                Level = level,
                                Name = spells[index].Name,
                            };
                        }
                    }
                }
            }

            extractor.ExtractTemplate(path, extractLine);
        }
    }
}
