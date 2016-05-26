using System.Collections.Generic;
using offline_dictionary.com_shared.Model;

namespace offline_dictionary.com_export_stardict
{
    public class IdxStructure
    {
        public IdxStructure ParentWord { get; set; }
        public uint DefinitionPosition { get; set; }
        public uint DefinitionLength { get; set; }
        public List<Meaning> Meanings { get; set; }

        public override string ToString()
        {
            return $"{DefinitionPosition} -> {DefinitionLength} ({(ParentWord != null ? "AlternateWord" : "MainWord")})";
        }
    }
}
