using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MyDBC.Helpers
{
    internal static class Utils
    {
        public static void OverrideNumberFormat()
        {
            var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            culture.NumberFormat = new CultureInfo("en-US").NumberFormat;
            CultureInfo.CurrentCulture = culture;
        }

        public static IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption = default)
        {
            var files = new List<string>();
            foreach (var sp in searchPattern.Split('|'))
                files.AddRange(Directory.GetFiles(path, sp, searchOption));

            return files;
        }
    }
}
