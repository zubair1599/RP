using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace RPDailyScrape
{
    internal class Common
    {
        public static Random rand;

        public static void Wait()
        {
            var interval = (int) Math.Floor(1 + (rand.NextDouble()*2));
            Thread.Sleep(interval*1000);
        }

        public static string ProcessName(string name)
        {
            name = name.Replace("&acute;", "'");

            var regex_name = new Regex(@"([^(]+)\(([A-Z\s]+)\)");
            Match match_name = regex_name.Match(name);
            if (match_name.Success)
            {
                name = match_name.Groups[1].ToString().Trim();
            }

            return name;
        }

        public static string FlattenName(string str)
        {
            str = str.ToUpper();

            var regex = new Regex(@"(.*?)\s+[IVX]+$");
            Match match = regex.Match(str);
            if (match.Success)
            {
                str = match.Groups[1].ToString();
            }

            regex = new Regex(@"[^A-Z0-9]");
            str = regex.Replace(str, "");

            return str;
        }
    }
}