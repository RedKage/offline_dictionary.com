﻿using System.Collections.Generic;

namespace offline_dictionary.com_shared.Model
{
    public class GenericDictionary
    {
        public Dictionary<Meaning, List<Definition>> AllWords { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Website { get; set; }

        public GenericDictionary()
        {
            AllWords = new Dictionary<Meaning, List<Definition>>();
        }

        public override string ToString()
        {
            return $"{FullName} ({Version}) - {AllWords.Keys.Count} words";
        }
    }
}