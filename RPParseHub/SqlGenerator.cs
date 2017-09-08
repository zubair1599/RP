using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RPParseHub
{
    public static class Cleaner
    {
        public static List<RPCourse> Courses { get; set; }
        public static void GetBaseData()
        {
            using (RacingPostRacesEntities en = new RacingPostRacesEntities())
            {
                Courses = en.RPCourses.ToList();
            }

        }

        public static void ProcessClass(Race race)
        {
            var regex_class =
                new Regex(
                    @"(?:\(Class (\d)\) \| ){0,1}(?:\((?:([\d-]+), ){0,1}([\d-+yo]+)\)){0,1}(?: *\(([\dmfy]+)\)\|){0,1}(?:[| ]*([\dm&frac;]+)){0,1}(?: ([A-Za-z ]+)){0,1}(?:([\d]+) (?:fences|hdles)){0,1}(?: ([\d]+) omitted){0,1}");
            Match match_class = regex_class.Match(race.ClassRaw);
            if (match_class.Success)
            {
                ProcessClassRaw(race, match_class);

                //ProcessRaceType(scrape, race);

                //ProcessTime(race);

                //ProcessGradeGroup(scrape, race);

                //ProcessPrize(race);
            }
            else
            {
                //Logger.WriteLog("Invalid Class field");
            }
        }
        public static void ProcessClassRaw(Race race, Match match_class)
        {
            int? _class = null;
            int race_class_int = 0;
            if (Int32.TryParse(match_class.Groups[1].ToString(), out race_class_int))
            {
                _class = race_class_int;
            }
            race.Class = _class;

            string rating = match_class.Groups[2].ToString();
            race.Rating = rating == "" ? null : rating;

            string eligibility = match_class.Groups[3].ToString();
            race.Eligibility = eligibility == "" ? null : eligibility;

            string distance_exact = match_class.Groups[4].ToString();
            if (distance_exact != "")
            {
                var regex_distance_exact = new Regex(@"(?:(\d)m){0,1}(?:(\d)f){0,1}(?:(\d+)y){0,1}");
                Match match_distance_exact = regex_distance_exact.Match(distance_exact);
                if (match_distance_exact.Success)
                {
                    int miles = 0;
                    Int32.TryParse(match_distance_exact.Groups[1].ToString(), out miles);
                    int furlongs = 0;
                    Int32.TryParse(match_distance_exact.Groups[2].ToString(), out furlongs);
                    int yards = 0;
                    Int32.TryParse(match_distance_exact.Groups[3].ToString(), out yards);
                    var DistanceYards = miles * 1760 + furlongs * 220 + yards;
                }
            }

            race.Distance = match_class.Groups[5].ToString().Replace("&frac12;", "½");
            race.Going = match_class.Groups[6].ToString();

            int? fences = null;
            int fences_int = 0;
            if (Int32.TryParse(match_class.Groups[7].ToString(), out fences_int))
            {
                fences = fences_int;
            }
            race.Fences = fences;

            int? fences_omitted = null;
            int fences_omitted_int = 0;
            if (Int32.TryParse(match_class.Groups[8].ToString(), out fences_omitted_int))
            {
                fences_omitted = fences_omitted_int;
            }
            race.FencesOmitted = fences_omitted;

            race.FencesHurdles = "";
            if (race.ClassRaw.Contains("fences"))
            {
                race.FencesHurdles = "fences";
            }
            else if (race.ClassRaw.Contains("hdles"))
            {
                race.FencesHurdles = "hurdles";
            }

            ProcessDistance(race);
            ProcessRaceType(race);
            ProcessTime(race);
            ProcessGradeGroup(race);
            ProcessPrize(race);
            race.Handicap = race.Name.Contains("Handicap");
            race.Chase = race.Name.Contains("Chase");
            //TrackDirection direction =
            //    track_dirs.FirstOrDefault(x => x.CourseId == scrape.CourseId && race.DistanceYards >= x.DistFrom &&
            //                                   race.DistanceYards <= x.DistTo);
            //if (direction != null)
            //{
            //    race.TrackDirection = direction.Direction;
            //}
        }
        private static void ProcessDistance(Race race)
        {
            var regex_distance = new Regex(@"(?:(\d)m){0,1}(?:([\d½]+)f){0,1}");
            Match match_distance = regex_distance.Match(race.Distance);
            if (match_distance.Success)
            {
                string miles_str = match_distance.Groups[1].ToString();
                string furlongs_str = match_distance.Groups[2].ToString();
                int miles = 0;
                if (miles_str != "")
                {
                    miles = Convert.ToInt32(miles_str);
                }

                int furlongs = 0;
                int half_furlong = 0;
                if (furlongs_str != "")
                {
                    if (furlongs_str.Contains("½"))
                    {
                        half_furlong = 110;
                        furlongs_str = furlongs_str.Replace("½", "");
                    }

                    if (furlongs_str != "")
                    {
                        furlongs = Convert.ToInt32(furlongs_str);
                    }
                }

                if (race.DistanceYards == null)
                {
                    race.DistanceYards = miles * 1760 + furlongs * 220 + half_furlong;
                }

                if (miles == 4)
                {
                    race.DistanceStd = "4m";
                }
                else if (miles == 3)
                {
                    if (furlongs == 0)
                    {
                        race.DistanceStd = "3m";
                    }
                    else if (furlongs < 4)
                    {
                        race.DistanceStd = "3m 1f";
                    }
                    else
                    {
                        race.DistanceStd = "3m 4f";
                    }
                }
                else if (furlongs == 0)
                {
                    race.DistanceStd = String.Format("{0}m", miles);
                }
                else if (miles == 0)
                {
                    race.DistanceStd = String.Format("{0}f", furlongs);
                }
                else
                {
                    race.DistanceStd = String.Format("{0}m {1}f", miles, furlongs);
                }
            }
            else
            {
                //Logger.WriteLog("Invalid Distance field");
            }
        }
        private static void ProcessRaceType(Race race)
        {
            var selectCourse = Courses.FirstOrDefault(c => c.Id == race.CourseId);
            race.TrackTypeId = selectCourse.TrackTypeID;
            race.Country = selectCourse.Country;

            var regex_hurdle = new Regex(@"(?: |\(|\.)(?:hurdle|hdle|h'dle)", RegexOptions.IgnoreCase);
            var regex_chase = new Regex(@"(?: |\(|\.)(?:chase|steeplechase|s'chase|s'chse|schase)",
                RegexOptions.IgnoreCase);
            var regex_hunters = new Regex(@"(?:hunters' |hunter's |hunters |hunter |hunt\.)ch", RegexOptions.IgnoreCase);
            var regex_nhflat = new Regex(@"(?:flat race|(?:national hunt |nh |n\.h\.|n\.h\. )(?:race|flat))",
                RegexOptions.IgnoreCase);

            race.RaceType = null;
            if (race.FencesHurdles == "hurdles")
            {
                race.RaceType = 1;
            }
            else if (race.FencesHurdles == "fences")
            {
                race.RaceType = 2;
            }
            else if (regex_hurdle.Match(race.Name).Success && race.DistanceYards >= 3520)
            {
                race.RaceType = 1;
            }
            else if (regex_hunters.Match(race.Name).Success)
            {
                race.RaceType = 7;
            }
            else if (regex_chase.Match(race.Name).Success && race.DistanceYards >= 3520)
            {
                race.RaceType = 2;
            }
            else if (regex_nhflat.Match(race.Name).Success && race.DistanceYards >= 2540)
            {
                race.RaceType = 6;
            }
            else if (race.DistanceYards > 4874)
            {
                race.RaceType = 2;
            }
            else if (race.TrackTypeId != 1)
            {
                race.RaceType = 3;
            }
            else
            {
                race.RaceType = 4;
            }

            //if (scrape.Country == "")
            //{
            //    RPSeason season =
            //        seasons.FirstOrDefault(
            //            x => race.RaceType == x.RaceType && race.StartTime >= x.DateFrom && race.StartTime <= x.DateTo);
            //    if (season != null)
            //    {
            //        race.Season = season.Season1;
            //        race.SeasonType = season.SeasonType;
            //    }
            //}
        }
        public static void ProcessTime(Race race)
        {
            int hours = 0;
            int minutes = 0;
            var regex_time = new Regex("(\\d+)(:)(\\d+)",
                RegexOptions.Singleline);
            Match match_time = regex_time.Match(race.Time);
            if (match_time.Success)
            {
                hours = Convert.ToInt32(match_time.Groups[1].ToString());
                minutes = Convert.ToInt32(match_time.Groups[3].ToString());
            }

            if (hours < 11)
            {
                hours += 12;
            }

            race.StartTime = Convert.ToDateTime(race.Date);
            race.StartTime = race.StartTime.AddHours(hours);
            race.StartTime = race.StartTime.AddMinutes(minutes);

            //if (race.Footer.FirstOrDefault().WinTime != null)
            //{
            //    var regex_time = new Regex(@"(?:(\d)m){0,1}\s?(?:([\d.]+)s){0,1}");
            //    Match match_time = regex_time.Match(race.Footer.FirstOrDefault().WinTime);
            //    if (match_time.Success)
            //    {
            //        int minutes = 0;
            //        double seconds = 0;
            //        if (match_time.Groups[1].ToString() != "")
            //        {
            //            minutes = Convert.ToInt32(match_time.Groups[1].ToString());
            //        }
            //        if (match_time.Groups[2].ToString() != "")
            //        {
            //            seconds = Convert.ToDouble(match_time.Groups[2].ToString());
            //        }
            //        race.TimeSeconds = minutes * 60 + seconds;
            //    }
            //}
        }
        private static void ProcessGradeGroup(Race race)
        {
            var regexGroup = new Regex(@"(?:\(Group ([123])|\((Listed))");
            var regexGrade = new Regex(@"(?:(?:\(| )Grade ([123])|\((Listed))");

            if (race.Country == "" && race.Class == 1)
            {
                if (race.RaceType == 3 || race.RaceType == 4)
                {
                    Match matchGroup = regexGroup.Match(race.Name);
                    if (matchGroup.Success)
                    {
                        if (matchGroup.Groups[2].ToString() == "Listed")
                        {
                            race.GradeGroup = "Listed";
                        }
                        else
                        {
                            race.GradeGroup = matchGroup.Groups[1].ToString();
                        }
                    }
                }
                else
                {
                    Match matchGrade = regexGrade.Match(race.Name);
                    if (matchGrade.Success)
                    {
                        if (matchGrade.Groups[2].ToString() == "Listed")
                        {
                            race.GradeGroup = "Listed";
                        }
                        else
                        {
                            race.GradeGroup = matchGrade.Groups[1].ToString();
                        }
                    }
                    else
                    {
                        race.GradeGroup = "Listed";
                    }
                }
            }

            if (race.Country == "IRE")
            {
                if (race.RaceType == 3 || race.RaceType == 4)
                {
                    Match matchGroup = regexGroup.Match(race.Name);
                    if (matchGroup.Success)
                    {
                        if (matchGroup.Groups[2].ToString() == "Listed")
                        {
                            race.GradeGroup = "Listed";
                            race.Class = 1;
                        }
                        else
                        {
                            race.Class = 1;
                            race.GradeGroup = matchGroup.Groups[1].ToString();
                        }
                    }
                }
                else
                {
                    Match matchGroup = regexGroup.Match(race.Name);
                    Match matchGrade = regexGrade.Match(race.Name);

                    if (matchGroup.Success)
                    {
                        race.GradeGroup = matchGroup.Groups[2].ToString() == "Listed"
                            ? "Listed"
                            : matchGroup.Groups[1].ToString();
                    }
                    else if (matchGrade.Success)
                    {
                        race.GradeGroup = matchGrade.Groups[2].ToString() == "Listed"
                            ? "Listed"
                            : matchGrade.Groups[1].ToString();
                    }
                }
            }

            if (race.Country == "ITY" || race.Country == "GER" || race.Country == "FR" || race.Country == "USA")
            {
                Match matchGroup = regexGroup.Match(race.Name);
                Match matchGrade = regexGrade.Match(race.Name);

                if (matchGroup.Success)
                {
                    race.GradeGroup = matchGroup.Groups[2].ToString() == "Listed"
                        ? "Listed"
                        : matchGroup.Groups[1].ToString();
                }
                else if (matchGrade.Success)
                {
                    race.GradeGroup = matchGrade.Groups[2].ToString() == "Listed"
                        ? "Listed"
                        : matchGrade.Groups[1].ToString();
                }
            }
            if (race.Country == "UAE")
            {
                if (race.RaceType == 3 || race.RaceType == 4)
                {
                    Match matchGroup = regexGroup.Match(race.Name);
                    if (matchGroup.Success)
                    {
                        if (matchGroup.Groups[2].ToString() == "Listed")
                        {
                            race.GradeGroup = "Listed";
                        }
                        else
                        {
                            race.GradeGroup = matchGroup.Groups[1].ToString();
                        }
                    }
                }
                else
                {
                    Match matchGrade = regexGrade.Match(race.Name);
                    if (matchGrade.Success)
                    {
                        if (matchGrade.Groups[2].ToString() == "Listed")
                        {
                            race.GradeGroup = "Listed";
                        }
                        else
                        {
                            race.GradeGroup = matchGrade.Groups[1].ToString();
                        }
                    }
                    else
                    {
                        race.GradeGroup = "Listed";
                    }
                }
            }
        }
        private static void ProcessPrize(Race race)
        {
            var regex_prize = new Regex(@"&pound;([\d,]+).(\d+)");
            Match match_prize = regex_prize.Match(race.PrizeMoney);
            int i = 0;
            while (i < race.Prizes.Count)
            {

                decimal prize = Convert.ToDecimal(race.Prizes[i].Replace(race.CurrencyUnit, ""));

                switch (i)
                {
                    case 0:
                        race.Prize1st = prize;
                        break;
                    case 1:
                        race.Prize2nd = prize;
                        break;
                    case 2:
                        race.Prize3rd = prize;
                        break;
                    case 3:
                        race.Prize4th = prize;
                        break;
                    case 4:
                        race.Prize5th = prize;
                        break;
                    case 5:
                        race.Prize6th = prize;
                        break;
                }
                i++;

            }
        }
        public static void GetFooter(Race race)
        {
             HtmlDocument document = new HtmlDocument();
            string htmlString = race.Notes;

            if (String.IsNullOrEmpty(htmlString)) return;

            document.LoadHtml(htmlString);
            HtmlNodeCollection collection = document.DocumentNode.SelectNodes("//li");
            foreach (HtmlNode link in collection)
            {
                var target = link.ChildNodes;

                if (target.Any(t => t.InnerText.Contains(" ran")))
                {
                    race.NoOfRunners = target.FirstOrDefault(t => t.InnerText.Contains("ran")).InnerText.Replace("\n ", "").Replace("ran", "").Trim();
                }
                if (target.Any(t => t.InnerText.ToLower().Contains("winning time")))
                {
                    race.WinTime = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("winning time")).NextSibling.InnerText.Replace("\n ", "").Replace(" ", "").Trim();
                }
                if (target.Any(t => t.InnerText.ToLower().Contains("total sp")))
                {
                    race.TotalSP = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("total sp")).NextSibling.InnerText.Replace("\n ", "").Trim();
                }

                if (target.Any(t => t.InnerText.ToLower().Contains("non-runners")))
                {
                    race.NonRunners = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("non-runners")).NextSibling.InnerText.Replace("\n ", "").Trim();
                }
                if (target.Any(t => t.InnerText.ToLower().Contains("1st owner")))
                {
                    race.FirstOwner = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("1st owner")).NextSibling.InnerText.Replace("\n ", "").Trim();
                }
                if (target.Any(t => t.InnerText.ToLower().Contains("1st breeder")))
                {
                    race.FirseBreeder = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("1st breeder")).NextSibling.InnerText.Replace("\n ", "").Trim();
                }
                if (target.Any(t => t.InnerText.ToLower().Contains("2nd owner")))
                {
                    race.SecondOwner = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("2nd owner")).NextSibling.InnerText.Replace("\n ", "").Trim();
                }

                if (target.Any(t => t.InnerText.ToLower().Contains("3rd owner")))
                {
                    race.ThirdOwner = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("3rd owner")).NextSibling.InnerText.Replace("\n ", "").Trim();
                }
            }

          
                
            
        }

        public static void ProcessWt(Race race)
        {
            foreach (var runner in race.Runners)
            {
                HtmlDocument document = new HtmlDocument();
                string htmlString = runner.WtRaw;

                if (String.IsNullOrEmpty(htmlString)) return;

                document.LoadHtml(htmlString);
                HtmlNodeCollection collection = document.DocumentNode.SelectNodes("//span");
                foreach (var item in collection)
                {
                    if (item.Attributes.Any(a => a.Value.Equals("horse-weight-st")))
                    {
                        runner.WeightRaw = item.InnerHtml.Replace("\n ", "").Replace("<!---->", "");
                    }
                    if (item.Attributes.Any(a => a.Value.Equals("horse-weight-lb")))
                    {
                        runner.WeightRaw = runner.WeightRaw + "-" + item.InnerHtml.Replace("\n ", "").Replace("<!---->", "");
                    }
                    runner.WeightRaw = runner.WeightRaw.Trim();
                }
            }
          //  var runner = race.Runners[3];
           
        }

    }
}