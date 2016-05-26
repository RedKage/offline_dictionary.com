namespace offline_dictionary.com_export_stardict
{
    // ReSharper disable InconsistentNaming
    /// <summary>
    /// Ported to C# from gstrfuncs.c
    /// https://github.com/GNOME/glib/blob/master/glib/gstrfuncs.c
    /// </summary>
    public static class g_ascii_strcasecmp_port
    {
        private static bool IsUpper(char c)
        {
            return c >= 'A' && c <= 'Z';
        }

        private static char ToLower(char c)
        {
            return IsUpper(c)
                ? (char)(c - 'A' + 'a')
                : c;
        }

        public static int g_ascii_strcasecmp(string s1, string s2)
        {
            int indexS1 = 0, indexS2 = 0;

            if (string.IsNullOrEmpty(s1))
                return 0;

            if (string.IsNullOrEmpty(s2))
                return 0;

            while (indexS1 < s1.Length && indexS2 < s2.Length)
            {
                int c1 = ToLower(s1[indexS1]);
                int c2 = ToLower(s2[indexS2]);
                if (c1 != c2)
                    return c1 - c2;

                indexS1++;
                indexS2++;
            }

            if (indexS1 >= s1.Length && indexS2 < s2.Length)
                return -s2[indexS2]; // 0 - s2[indexS2]

            if (indexS2 >= s2.Length && indexS1 < s1.Length)
                return s1[indexS2]; // s1[indexS1] - 0

            return 0;
        }

    }
}
