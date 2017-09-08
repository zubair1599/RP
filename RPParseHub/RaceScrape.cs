using HtmlAgilityPack;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RPParseHub
{
    public class RaceScraper
    {
        public static string baseUrl = "https://www.racingpost.com";
        public static List<string> DidNotFinsh = new List<string> { "BD", "CO", "DSQ", "F", "LFT", "PU", "REF", "RO", "RR", "SU", "UR", "VOI", "WDU" };

        public static List<RPCourse> Courses { get; set; }

        public static void ScrapeRace()
        {
            using (RacingPostRacesEntities db = new RacingPostRacesEntities())
            {
                var startDate = db.ScrapeCourses.FirstOrDefault().LastDateScraped;

                //Check current date has all link downlaoded
                var alreadyDownloaded = db.ScrapeRaces.Select(s => s.RaceId).ToList();
                Courses = db.RPCourses.ToList();

                //scrape page start

                var linksToDownload = db.ScrapeRaces.Where(link => link.Required == true && link.Scraped == false).OrderBy(d => d.RaceDate).ToList();

                foreach (var url in linksToDownload)
                {
                    Thread.Sleep(1000);
                    int retry = 0;
                    while (retry <= 3)
                    {
                        using (System.Data.Entity.DbContextTransaction dbTran = db.Database.BeginTransaction())
                        {
                            try
                            {
                                Console.Write(string.Format("Downloading race for  {0} - {1}\n", url.RaceDate, url.RaceId));

                                DownloadSingleRace(url.Link, db);
                                url.Scraped = true;
                                db.SaveChanges();
                                dbTran.Commit();
                                break;
                            }
                            catch (Exception ex)
                            {
                                dbTran.Rollback();
                                retry++;
                                //throw;
                            }
                        }
                    }
                }

            }
        }
        

        public static void DownloadSingleRace(string url, RacingPostRacesEntities db)
        {
            int raceId = Convert.ToInt32(url.Split('/').LastOrDefault());
            if (db.RPRaces.Any(r => r.Id == raceId)) return; 

            var Browser = new ScrapingBrowser();
            Browser.AllowAutoRedirect = true; // Browser has many settings you can access in setup
            Browser.AllowMetaRedirect = true;
            //go to the home page
            //var PageResult = Browser.NavigateToPage(new Uri(url));
            var web = new HtmlWeb();
            var doc = web.Load(url);

           
                //Extract Header
                RPRace race = new RPRace();

            race.Id = raceId;
                race.StartTime = Convert.ToDateTime(doc.QuerySelectorAll("span .rp-raceTimeCourseName__date").FirstOrDefault().InnerHtml);
                race.Time = doc.QuerySelectorAll("span .rp-raceTimeCourseName__time").FirstOrDefault().InnerHtml;
                race.Name = doc.QuerySelectorAll("h2 .rp-raceTimeCourseName__title").FirstOrDefault().InnerHtml;
                race.CourseId = Convert.ToInt32(url.Split('/')[4]);
                race.PostTemplate = true;

                if (doc.QuerySelectorAll("span .rp-raceTimeCourseName_class").FirstOrDefault() != null)
                {
                    string classDesc = doc.QuerySelectorAll("span .rp-raceTimeCourseName_class").FirstOrDefault().InnerHtml;
                    classDesc = classDesc.Replace("(Class ", "").Replace(")", "").Replace("\n", "").Trim();
                    race.Class = Convert.ToInt32(classDesc);
                }
                if (doc.QuerySelectorAll("span .rp-raceTimeCourseName_ratingBandAndAgesAllowed").FirstOrDefault() != null)
                {
                    string ageAllowed = doc.QuerySelectorAll("span .rp-raceTimeCourseName_ratingBandAndAgesAllowed").FirstOrDefault().InnerHtml;
                ageAllowed = ageAllowed.Replace("\n", "").Replace(")", "").Replace("(", "").Trim();//.Split(',')[1];
                    race.Eligibility = ageAllowed;
                }

                race.Distance = doc.QuerySelectorAll("span .rp-raceTimeCourseName_distance").FirstOrDefault().InnerHtml.Replace("\n", "").Trim();
                var classRaw = doc.QuerySelectorAll("span .rp-raceTimeCourseName__info_container").FirstOrDefault();
                classRaw.ChildNodes.ToList().ForEach(c =>
                {
                    if (c.Name.Equals("span"))
                    {
                        race.ClassRaw = race.ClassRaw + c.InnerHtml.Replace("\n", "").Trim() + " | ";
                    }
                    if (c.Name.Equals("div"))
                    {
                        c.ChildNodes.ToList().ForEach(p =>
                        {

                            race.PrizeMoney = race.PrizeMoney + p.InnerHtml.Replace("\n", "").Trim().Replace("&pound;", "£") + " ";
                        });
                        race.PrizeMoney = race.PrizeMoney.Trim();
                    }
                });

                race.Notes = doc.QuerySelectorAll("div .rp-raceInfo").FirstOrDefault().InnerHtml;
                ProcessRaceAttributes(race);
                ProcessFooter(race);
                ProcessRunner(race, doc, db);
                db.RPRaces.Add(race);
                db.SaveChanges();
            
        }

        public static void ProcessRaceAttributes(RPRace race)
        {
            var regex_class =
                new Regex(
                    @"(?:\(Class (\d)\) \| ){0,1}(?:\((?:([\d-]+), ){0,1}([\d-+yo]+)\)){0,1}(?: *\(([\dmfy]+)\)\|){0,1}(?:[| ]*([\dm&frac;]+)){0,1}(?: ([A-Za-z ]+)){0,1}(?:([\d]+) (?:fences|hdles)){0,1}(?: ([\d]+) omitted){0,1}");
            Match match_class = regex_class.Match(race.ClassRaw);
            if (match_class.Success)
            {
                ProcessClassRaw(race, match_class);
            }
            else
            {
                //Logger.WriteLog("Invalid Class field");
            }
            ProcessDistance(race);
            ProcessRaceType(race);
            ProcessTime(race);
            ProcessGradeGroup(race);
            ProcessPrize(race);
        }
        public static void ProcessClassRaw(RPRace race, Match match_class)
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
        private static void ProcessDistance(RPRace race)
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
        private static void ProcessRaceType(RPRace race)
        {
            var selectCourse = Courses.FirstOrDefault(c => c.Id == race.CourseId);
            int? TrackTypeId = selectCourse.TrackTypeID;
            string Country = selectCourse.Country;

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
            else if (TrackTypeId != 1)
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
        public static void ProcessTime(RPRace race)
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

            //race.StartTime = Convert.ToDateTime(race.Date);
            race.StartTime = race.StartTime.Value.AddHours(hours);
            race.StartTime = race.StartTime.Value.AddMinutes(minutes);

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
        private static void ProcessGradeGroup(RPRace race)
        {
            var selectCourse = Courses.FirstOrDefault(c => c.Id == race.CourseId);
            string Country = selectCourse.Country;

            var regexGroup = new Regex(@"(?:\(Group ([123])|\((Listed))");
            var regexGrade = new Regex(@"(?:(?:\(| )Grade ([123])|\((Listed))");

            if (Country == "" && race.Class == 1)
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

            if (Country == "IRE")
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

            if (Country == "ITY" || Country == "GER" || Country == "FR" || Country == "USA")
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
            if (Country == "UAE")
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
        private static void ProcessPrize(RPRace race)
        {
            var regex_prize = new Regex(@"&pound;([\d,]+).(\d+)");
            Match match_prize = regex_prize.Match(race.PrizeMoney);
           
            var temp = race.PrizeMoney.Split(' ');
            List<string>  Prizes = new List<string>();
            for (int i = 0; i < temp.Count(); i++)
            {
                if (i % 2 != 0)
                    Prizes.Add(temp[i]);
            }
            race.CurrencyUnit = Prizes.First().Substring(0, 1);
            int index = 0;
            while (index < Prizes.Count)
            {

                decimal prize = Convert.ToDecimal(Prizes[index].Replace(race.CurrencyUnit, ""));

                switch (index)
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
                index++;

            }
        }
        public static string ProcessWt(string wtRaw)
        {
                HtmlDocument document = new HtmlDocument();
                string htmlString = wtRaw;

                if (String.IsNullOrEmpty(htmlString)) return "";

                document.LoadHtml(htmlString);
                HtmlNodeCollection collection = document.DocumentNode.SelectNodes("//span");
                foreach (var item in collection)
                {
                    if (item.Attributes.Any(a => a.Value.Equals("horse-weight-st")))
                    {
                        wtRaw = item.InnerHtml.Replace(" ", "").Replace("<!---->", "");
                    }
                    if (item.Attributes.Any(a => a.Value.Equals("horse-weight-lb")))
                    {
                        wtRaw = wtRaw + "-" + item.InnerHtml.Replace(" ", "").Replace("<!---->", "");
                    }
                    wtRaw = wtRaw.Trim();
                }

            return wtRaw;

        }
        public static void ProcessFooter(RPRace race)
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
                    race.NonRunners = target.FirstOrDefault(t => t.InnerText.Contains("ran")).InnerText.Replace("\n ", "").Replace("ran", "").Trim();
                }
                if (target.Any(t => t.InnerText.ToLower().Contains("winning time")))
                {
                    race.Time = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("winning time")).NextSibling.InnerText.Replace("\n ", "").Replace(" ", "").Trim();
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
                    //race.FirstOwner = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("1st owner")).NextSibling.InnerText.Replace("\n ", "").Trim();
                }
                if (target.Any(t => t.InnerText.ToLower().Contains("1st breeder")))
                {
                   // race.FirseBreeder = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("1st breeder")).NextSibling.InnerText.Replace("\n ", "").Trim();
                }
                if (target.Any(t => t.InnerText.ToLower().Contains("2nd owner")))
                {
                   // race.SecondOwner = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("2nd owner")).NextSibling.InnerText.Replace("\n ", "").Trim();
                }

                if (target.Any(t => t.InnerText.ToLower().Contains("3rd owner")))
                {
                    //race.ThirdOwner = target.FirstOrDefault(t => t.InnerText.ToLower().Contains("3rd owner")).NextSibling.InnerText.Replace("\n ", "").Trim();
                }
            }
        }
        public static void ProcessRunner (RPRace race, HtmlDocument doc, RacingPostRacesEntities db)
        {
            RPRunner runner;
           var rows = doc.QuerySelectorAll("tbody").FirstOrDefault().ChildNodes.Where(c=>c.Name == "tr").ToList().Where(row=>row.Attributes[0].Value == "rp-horseTable__mainRow").ToList();
            foreach (var item in rows)
            {
                runner = new RPRunner();
                runner.Status = "Runner";
                runner.PostTemplate = true;
                runner.RaceId = race.Id;

                var posTemp = item.QuerySelectorAll("div .rp-horseTable__pos").FirstOrDefault().ChildNodes[3].ChildNodes[1].ChildNodes[0].InnerHtml.Replace("\n", "").Trim();
                if (DidNotFinsh.Any(df => df.Equals(posTemp)))
                {
                    runner.DidNotFinish = posTemp;
                }
                else
                {
                    runner.Position = Convert.ToInt32(item.QuerySelectorAll("div .rp-horseTable__pos").FirstOrDefault().ChildNodes[3].ChildNodes[1].ChildNodes[0].InnerHtml.Replace("\n", "").Trim());
                }

                string draw = item.QuerySelectorAll("div .rp-horseTable__pos").FirstOrDefault().ChildNodes[3].ChildNodes[1].ChildNodes[2].InnerHtml.Replace("&nbsp;(", "").Replace(")", "").Trim();
                int DrawPos;
                int.TryParse(draw, out DrawPos);
                runner.Draw = DrawPos > 0 ? Convert.ToInt32(DrawPos) : (int?)null;

                var lendthAttr = item.QuerySelectorAll("span .rp-horseTable__pos__length").FirstOrDefault();
                if (lendthAttr.ChildNodes.Count >= 2)
                {
                    runner.Distance = lendthAttr.ChildNodes[1].InnerHtml;
                }
                if (lendthAttr.ChildNodes.Count >= 4)
                {
                    // var dis = FractionToDouble(lendthAttr.ChildNodes[3].InnerHtml.Replace("[", "").Replace("]", ""));
                }
                var horseUrl = item.QuerySelectorAll("a .rp-horseTable__horse__name").FirstOrDefault().Attributes[0].Value;
                runner.HorseId = Helper.GetIdfromUrl(horseUrl, "/profile/horse/");
                runner.Price = item.QuerySelectorAll("span .rp-horseTable__horse__price").FirstOrDefault().InnerHtml.Replace("\n", "").Trim();
                var persons = item.QuerySelectorAll("span .rp-horseTable__human__wrapper");

                //jockey info
                var jockeyUrl = persons.FirstOrDefault().ChildNodes[1];
                runner.JockeyId = Helper.GetIdfromUrl(jockeyUrl.Attributes[0].Value, "/profile/jockey/");
                Jockey jockey = new Jockey();
                jockey.Id = Convert.ToInt32(runner.JockeyId);
                jockey.Name = jockeyUrl.InnerHtml.Replace("\n", "").Trim();
                jockey.Name = jockey.Name.Substring(0, jockey.Name.IndexOf("<"));

                if(!db.Jockeys.Where(j=>j.Id == jockey.Id).Any())
                {
                    db.Jockeys.Add(jockey);
                    db.SaveChanges();
                }

                //trainer info
                var trainerUrl = persons[1].ChildNodes[1];
                runner.TrainerId = Helper.GetIdfromUrl(trainerUrl.Attributes[0].Value, "/profile/trainer/");
                Trainer trainer = new Trainer();
                trainer.Id = Convert.ToInt32(runner.TrainerId);
                trainer.Name = trainerUrl.InnerHtml.Replace("\n", "").Trim();
                if (trainer.Name.IndexOf("<") > 0)
                {
                    trainer.Name = trainer.Name.Substring(0, trainer.Name.IndexOf("<"));
                }
                if (!db.Trainers.Where(j => j.Id == trainer.Id).Any())
                {
                    db.Trainers.Add(trainer);
                    db.SaveChanges();
                }
                var age = item.ChildNodes[7].InnerHtml.Replace("\n", "").Trim();
                if (age.IndexOf("<") > 0)
                {
                    age = age.Substring(0, age.IndexOf("<"));
                }
                runner.Age = Convert.ToInt32(age);

                var wt = item.ChildNodes[9].InnerHtml.Replace("\n", "").Trim();
                runner.WeightRaw = ProcessWt(wt);
                db.RPRunners.Add(runner);
            }

        }
       
    }
}
