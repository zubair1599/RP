using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RPDailyScrape
{
    internal class PedigreeExtract
    {
        public static List<PedigreeHorse> Extract(string source)
        {
            var horses = new List<PedigreeHorse>();

            PedigreeHorse q_horse = ExtractQueryHorse(source);
            horses.Add(q_horse);

            ExtractPedigreeHorses(source, horses);

            return horses;
        }

        private static void ExtractPedigreeHorses(string source, List<PedigreeHorse> horses)
        {
            PedigreeHorse pending = null;
            int row_num = 0;
            int rowspan = 0;

            var regex_table = new Regex("<table.*class=pedigreetable(.*)</table>", RegexOptions.Singleline);
            Match match_table = regex_table.Match(source);
            if (match_table.Success)
            {
                string table = match_table.Groups[1].ToString();

                var regex_row = new Regex("<tr>(.*?)</tr", RegexOptions.Singleline);
                Match match_row = regex_row.Match(table);
                while (match_row.Success)
                {
                    row_num++;
                    string row = match_row.Groups[1].ToString();
                    // <td  colspan=2 rowspan=16 class=m onmousedown="clickMenu('FOOTSTEPSINTHESAND',5,1,event);"><a href=/footstepsinthes
                    var regex_col = new Regex("<td([^>]*?)>(.*?)</td", RegexOptions.Singleline);
                    Match match_col = regex_col.Match(row);
                    while (match_col.Success)
                    {
                        string col_parms = match_col.Groups[1].ToString();
                        string col = match_col.Groups[2].ToString();

                        string pqid = "";
                        string name = "";
                        string remainder = "";
                        string country = "";
                        int year = 0;
                        string colour = "";

                        if (col_parms.Contains("rowspan"))
                        {
                            var regex_rowspan = new Regex(@"rowspan=(\d+)\s", RegexOptions.Singleline);
                            Match match_rowspan = regex_rowspan.Match(col_parms);
                            if (match_rowspan.Success)
                            {
                                rowspan = Convert.ToInt32(match_rowspan.Groups[1].ToString());
                            }

                            var horse = new PedigreeHorse();

                            if (ExtractHorse(col, ref pqid, ref name, ref remainder))
                            {
                                horse.PQId = pqid;
                                horse.Name = name;

                                if (ExtractCountry(remainder, ref country))
                                {
                                    horse.Country = country;
                                }

                                if (ExtractFoalYear(remainder, ref year))
                                {
                                    horse.FoalYear = year;
                                }

                                if (ExtractColour(remainder, ref colour))
                                {
                                    horse.Colour = colour;
                                }

                                horse.Generation = GetGeneration(rowspan);
                                horse.Pedigree = GetPedigree(row_num, rowspan);

                                horses.Add(horse);
                            }
                        }
                        else
                        {
                            rowspan = 1;

                            if (col.Contains("<a href"))
                            {
                                pending = new PedigreeHorse();
                                if (ExtractHorse(col, ref pqid, ref name, ref remainder))
                                {
                                    pending.PQId = pqid;
                                    pending.Name = name;

                                    if (ExtractCountry(remainder, ref country))
                                    {
                                        pending.Country = country;
                                    }
                                }
                            }
                            else if (col_parms.Contains("class=m") || col_parms.Contains("class=f"))
                            {
                                if (pending != null && pending.PQId != null)
                                {
                                    if (ExtractFoalYear(col, ref year))
                                    {
                                        pending.FoalYear = year;
                                    }

                                    if (ExtractColour(col, ref colour))
                                    {
                                        pending.Colour = colour;
                                    }

                                    pending.Generation = GetGeneration(rowspan);
                                    pending.Pedigree = GetPedigree(row_num, rowspan);

                                    horses.Add(pending);
                                    pending = null;
                                }
                            }
                        }
                        match_col = match_col.NextMatch();
                    }
                    match_row = match_row.NextMatch();
                }
            }
        }

        private static PedigreeHorse ExtractQueryHorse(string source)
        {
            string topline = "";
            string subtopline = "";
            string info = "";

            var horse = new PedigreeHorse();

            var regex_pqid = new Regex(@"<li><a href=""/(\S+)""[^>]*>Pedigree</a>", RegexOptions.Singleline);
            Match match_pqid = regex_pqid.Match(source);
            if (match_pqid.Success)
            {
                horse.PQId = match_pqid.Groups[1].ToString().Trim();
            }

            var regex_topline = new Regex(@"<font size='-1' class=normal>(.*?)</font>", RegexOptions.Singleline);
            Match match_topline = regex_topline.Match(source);
            if (match_topline.Success)
            {
                topline = match_topline.Groups[1].ToString().Trim();
            }

            var regex_name = new Regex(
                @"<a href=""javascript:nothing\(\);"" class=""nounderline""[^>]*>([^<]*)</a></b>",
                RegexOptions.Singleline);
            Match match_name = regex_name.Match(topline);
            if (match_name.Success)
            {
                horse.Name = match_name.Groups[1].ToString().Trim();
            }

            //var regex_subtopline = new Regex(@"</a>(\s*\([A-Z]+\).*)DP =", RegexOptions.Singleline);
            var regex_subtopline = new Regex(@".*</a>(.*?)DP =", RegexOptions.Singleline);
            Match match_subtopline = regex_subtopline.Match(topline);
            if (match_subtopline.Success)
            {
                subtopline = match_subtopline.Groups[1].ToString().Trim();
            }

            string country = "";
            if (ExtractCountry(subtopline, ref country))
            {
                horse.Country = country;
            }

            string colour = "";
            if (ExtractColour(subtopline, ref colour))
            {
                horse.Colour = colour;
            }

            string sex = "";
            if (ExtractSex(subtopline, ref sex))
            {
                horse.Sex = sex;
            }

            int year = 0;
            if (ExtractFoalYear(subtopline, ref year))
            {
                horse.FoalYear = year;
            }

            var regex_wins = new Regex(@"(\d+) Starts, (\d+|M) Wins, (\d+) Places", RegexOptions.Singleline);
            Match match_wins = regex_wins.Match(topline);
            if (match_wins.Success)
            {
                horse.Starts = Convert.ToInt32(match_wins.Groups[1].ToString());
                string wins_txt = match_wins.Groups[2].ToString();
                if (wins_txt == "M")
                {
                    horse.Wins = 0;
                }
                else
                {
                    horse.Wins = Convert.ToInt32(wins_txt);
                }
                horse.Places = Convert.ToInt32(match_wins.Groups[3].ToString());
            }

            var regex_earnings = new Regex(@"Career Earnings:</b>(.*)$", RegexOptions.Singleline);
            Match match_earnings = regex_earnings.Match(topline);
            if (match_earnings.Success)
            {
                horse.Earnings = match_earnings.Groups[1].ToString().Trim();
            }

            var regex_info = new Regex(@"<div id=""subjectinfo""(.*?)</div>", RegexOptions.Singleline);
            Match match_info = regex_info.Match(source);
            if (match_info.Success)
            {
                info = match_info.Groups[1].ToString().Trim();

                var regex_owner = new Regex(@"Owner</b>:([^<]*)<", RegexOptions.Singleline);
                Match match_owner = regex_owner.Match(info);
                if (match_owner.Success)
                {
                    horse.Owner = match_owner.Groups[1].ToString().Trim();
                }

                var regex_breeder = new Regex(@"Breeder</b>:([^<]*)<", RegexOptions.Singleline);
                Match match_breeder = regex_breeder.Match(info);
                if (match_breeder.Success)
                {
                    horse.Breeder = match_breeder.Groups[1].ToString().Trim();
                }
            }

            horse.Generation = 0;
            horse.Pedigree = "";
            return horse;
        }

        private static bool ExtractHorse(string content, ref string pqid, ref string name, ref string remainder)
        {
            var regex = new Regex(@"<a href=/([^>]*)>([^<]*)</a>(.*)", RegexOptions.Singleline);
            Match match = regex.Match(content);
            if (match.Success)
            {
                pqid = match.Groups[1].ToString();
                name = match.Groups[2].ToString();
                remainder = match.Groups[3].ToString();
                return true;
            }

            return false;
        }

        private static bool ExtractCountry(string content, ref string country)
        {
            var regex = new Regex(@"\(([A-Z]+)\)", RegexOptions.Singleline);
            Match match = regex.Match(content);
            if (match.Success)
            {
                country = match.Groups[1].ToString();
                return true;
            }

            return false;
        }

        private static bool ExtractFoalYear(string content, ref int year)
        {
            var regex = new Regex(@"(\d\d\d\d)", RegexOptions.Singleline);
            Match match = regex.Match(content);
            if (match.Success)
            {
                year = Convert.ToInt32(match.Groups[1].ToString());
                return true;
            }

            return false;
        }

        private static bool ExtractColour(string content, ref string colour)
        {
            var regex = new Regex(@"([a-z/]+)\.", RegexOptions.Singleline);
            Match match = regex.Match(content);
            if (match.Success)
            {
                colour = match.Groups[1].ToString();
                return true;
            }

            return false;
        }

        private static bool ExtractSex(string content, ref string sex)
        {
            var regex = new Regex(@"([A-Z]),", RegexOptions.Singleline);
            Match match = regex.Match(content);
            if (match.Success)
            {
                string sex1 = match.Groups[1].ToString();
                switch (match.Groups[1].ToString())
                {
                    case "C":
                    case "H":
                        sex = "c";
                        return true;
                    case "M":
                    case "F":
                        sex = "f";
                        return true;
                    case "G":
                        sex = "g";
                        return true;
                }
                return true;
            }

            return false;
        }

        private static int GetGeneration(int rowspan)
        {
            switch (rowspan)
            {
                case 16:
                    return 1;
                case 8:
                    return 2;
                case 4:
                    return 3;
                case 2:
                    return 4;
                case 1:
                    return 5;
            }
            return 0;
        }

        private static string GetPedigree(int row_num, int rowspan)
        {
            switch (rowspan)
            {
                case 16:
                    switch (row_num)
                    {
                        case 1:
                            return "S";
                        case 17:
                            return "D";
                    }
                    break;
                case 8:
                    switch (row_num)
                    {
                        case 1:
                            return "SS";
                        case 9:
                            return "SD";
                        case 17:
                            return "DS";
                        case 25:
                            return "DD";
                    }
                    break;
                case 4:
                    switch (row_num)
                    {
                        case 1:
                            return "SSS";
                        case 5:
                            return "SSD";
                        case 9:
                            return "SDS";
                        case 13:
                            return "SDD";
                        case 17:
                            return "DSS";
                        case 21:
                            return "DSD";
                        case 25:
                            return "DDS";
                        case 29:
                            return "DDD";
                    }
                    break;
                case 2:
                    switch (row_num)
                    {
                        case 1:
                            return "SSSS";
                        case 3:
                            return "SSSD";
                        case 5:
                            return "SSDS";
                        case 7:
                            return "SSDD";
                        case 9:
                            return "SDSS";
                        case 11:
                            return "SDSD";
                        case 13:
                            return "SDDS";
                        case 15:
                            return "SDDD";
                        case 17:
                            return "DSSS";
                        case 19:
                            return "DSSD";
                        case 21:
                            return "DSDS";
                        case 23:
                            return "DSDD";
                        case 25:
                            return "DDSS";
                        case 27:
                            return "DDSD";
                        case 29:
                            return "DDDS";
                        case 31:
                            return "DDDD";
                    }
                    break;
                case 1:
                    switch (row_num)
                    {
                        case 1:
                            return "SSSSS";
                        case 2:
                            return "SSSSD";
                        case 3:
                            return "SSSDS";
                        case 4:
                            return "SSSDD";
                        case 5:
                            return "SSDSS";
                        case 6:
                            return "SSDSD";
                        case 7:
                            return "SSDDS";
                        case 8:
                            return "SSDDD";
                        case 9:
                            return "SDSSS";
                        case 10:
                            return "SDSSD";
                        case 11:
                            return "SDSDS";
                        case 12:
                            return "SDSDD";
                        case 13:
                            return "SDDSS";
                        case 14:
                            return "SDDSD";
                        case 15:
                            return "SDDDS";
                        case 16:
                            return "SDDDD";
                        case 17:
                            return "DSSSS";
                        case 18:
                            return "DSSSD";
                        case 19:
                            return "DSSDS";
                        case 20:
                            return "DSSDD";
                        case 21:
                            return "DSDSS";
                        case 22:
                            return "DSDSD";
                        case 23:
                            return "DSDDS";
                        case 24:
                            return "DSDDD";
                        case 25:
                            return "DDSSS";
                        case 26:
                            return "DDSSD";
                        case 27:
                            return "DDSDS";
                        case 28:
                            return "DDSDD";
                        case 29:
                            return "DDDSS";
                        case 30:
                            return "DDDSD";
                        case 31:
                            return "DDDDS";
                        case 32:
                            return "DDDDD";
                    }
                    break;
            }
            return "";
        }
    }

    public class PedigreeHorse
    {
        public string Name { get; set; }
        public string PQId { get; set; }
        public int Generation { get; set; }
        public string Pedigree { get; set; }
        public string Country { get; set; }
        public int? FoalYear { get; set; }
        public string Colour { get; set; }
        public string Sex { get; set; }
        public int? Starts { get; set; }
        public int? Wins { get; set; }
        public int? Places { get; set; }
        public string Earnings { get; set; }
        public string Owner { get; set; }
        public string Breeder { get; set; }
    }
}