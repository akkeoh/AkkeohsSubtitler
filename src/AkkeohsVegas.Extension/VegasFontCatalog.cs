using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;

namespace AkkeohsVegas.Extension
{

    internal static class VegasFontCatalog
    {
        public static string[] GetNames()
        {
            var names = new List<string>();
            try
            {
                using (var fonts = new InstalledFontCollection())
                {
                    foreach (FontFamily family in fonts.Families)
                    {
                        if (family == null || string.IsNullOrWhiteSpace(family.Name))
                            continue;
                        names.Add(family.Name);
                    }
                }
            }
            catch
            {

            }

            if (names.Count == 0)
            {
                names.Add("Arial");
                names.Add("Segoe UI");
                names.Add("Tahoma");
                names.Add("Times New Roman");
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names.ToArray();
        }

        public static string Resolve(string preferred)
        {
            string[] all = GetNames();
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                foreach (string name in all)
                {
                    if (string.Equals(name, preferred.Trim(), StringComparison.OrdinalIgnoreCase))
                        return name;
                }
            }

            foreach (string fallback in new[] { "Arial", "Segoe UI", "Tahoma" })
            {
                foreach (string name in all)
                {
                    if (string.Equals(name, fallback, StringComparison.OrdinalIgnoreCase))
                        return name;
                }
            }

            return all.Length > 0 ? all[0] : "Arial";
        }
    }
}
