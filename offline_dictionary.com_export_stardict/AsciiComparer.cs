using System.Collections.Generic;

namespace offline_dictionary.com_export_stardict
{
    public class AsciiComparer : IComparer<string>
    {
        public int Compare(string s1, string s2)
        {
            return g_ascii_strcasecmp_port.g_ascii_strcasecmp(s1, s2);
        }
    }
}