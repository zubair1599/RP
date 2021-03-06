﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RPDailyScrape
{
    internal class RaceScraper
    {
        private static RacingPostRacesDataContext db_rph;
        private static List<TrackDirection> track_dirs;
        private static List<Season> seasons;

        public static void ScrapeRaces()
        {
            Common.rand = new Random();
            db_rph = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            track_dirs = db_rph.TrackDirections.ToList();
            seasons = db_rph.Seasons.ToList();

            foreach (ScrapeRaceView scrape in db_rph.ScrapeRaceViews)
            {
                Logger.WriteLog("Scraping race " + scrape.RaceId.ToString() + " " +
                                ((DateTime) scrape.DateOfMeeting).ToShortDateString());

                string status = "";
                string page = "";
                GetRace(scrape.Link, ref status, ref page);

                if (status == "Complete")
                {
                    if (!ScrapeRacePage(page, scrape))
                    {
                        Logger.WriteLog("Failed to find race header " + scrape.RaceId.ToString());
                        Common.Wait();
                        continue;
                    }

                    ScrapeRace scr_rec = db_rph.ScrapeRaces.Where(x => x.Id == scrape.Id).FirstOrDefault();
                    if (scr_rec != null)
                    {
                        scr_rec.Scraped = true;
                    }
                }
                else
                {
                    Logger.WriteLog("Page retrieval error " + status + " - " + scrape.RaceId.ToString());
                }

                db_rph.SubmitChanges();
                Common.Wait();
            }
        }

        public static void ReProcessRacesForGradeGroup()
        {
            db_rph = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            List<Race> racelist =
                db_rph.Races.Where(x => x.GradeGroup == null && x.Meeting.Course.Country == "USA")
                    .OrderBy(y => y.Id)
                    .ToList();
            int count = 1;
            foreach (var race in racelist)
            {
                Logger.WriteLog("Reprocessing race " + race.Id.ToString() + " " + race.Name + " - " + count++ + " out of " +
                                racelist.Count);
                ProcessGradeGroup(new ScrapeRaceView() {Country = race.Meeting.Course.Country}, race);
                db_rph.SubmitChanges();
            }
        }

        private static bool ScrapeRacePage(string page, ScrapeRaceView scrape)
        {
            var regex_header = new Regex("<div class=\"rp-results rp-container cf js-contentWrapper ng-scope\">(.*?)</div>", RegexOptions.Singleline);
            Match match_header = regex_header.Match(page);
            if (match_header.Success)
            {
                DateTime start;
                List<NonRunner> nonrunners;
                Race race = ScrapeRace(page, scrape, match_header, out start, out nonrunners);

                SaveNonRunners(scrape, nonrunners);

                ScrapeRunners(page, scrape, start);

                ProcessDistBeaten(race);
            }
            else
            {
                return false;
            }

            return true;
        }

        private static void SaveNonRunners(ScrapeRaceView scrape, List<NonRunner> nonrunners)
        {
            foreach (NonRunner nr in nonrunners)
            {
                Horse horse = db_rph.Horses.FirstOrDefault(x => x.RPId == nr.Id);
                if (horse == null)
                {
                    horse = new Horse() {PriorityProcess = true, DetailProcessed = 0};
                    db_rph.Horses.InsertOnSubmit(horse);
                    horse.RPId = nr.Id;
                    horse.Name = nr.Name;
                    horse.Country = nr.Country;
                    if (horse.Country == "")
                    {
                        horse.Country = "GB";
                    }
                }

                db_rph.SubmitChanges();

                Runner runner =
                    db_rph.Runners.FirstOrDefault(x => x.HorseId == nr.Id && x.RaceId == scrape.RaceId);
                if (runner == null)
                {
                    runner = new Runner();
                    runner.HorseId = horse.Id;
                    runner.RaceId = scrape.RaceId;
                    db_rph.Runners.InsertOnSubmit(runner);
                }
                runner.Status = "NonRunner";
                runner.Position = 0;
                runner.Placed = 0;

                db_rph.SubmitChanges();
            }
        }

        private static Race ScrapeRace(string page, ScrapeRaceView scrape, Match match_header, out DateTime start,
            out List<NonRunner> nonrunners)
        {
            //
            //  Header fields
            //
            string header = match_header.Groups[1].ToString();

            int hours = 0;
            int minutes = 0;
            var regex_time = new Regex("<span class=\"timeNavigation\">.*?([0-9]{1,2}):([0-9]{1,2}).*?</span>",
                RegexOptions.Singleline);
            Match match_time = regex_time.Match(header);
            if (match_time.Success)
            {
                hours = Convert.ToInt32(match_time.Groups[1].ToString());
                minutes = Convert.ToInt32(match_time.Groups[2].ToString());
            }

            if (hours < 11)
            {
                hours += 12;
            }

            start = (DateTime) scrape.DateOfMeeting.Value.Date;
            start = start.AddHours(hours);
            start = start.AddMinutes(minutes);

            string name = null;
            var regex_name = new Regex("^([^<]+)</h3>", RegexOptions.Multiline);
            Match match_name = regex_name.Match(header);
            if (match_name.Success)
            {
                name = match_name.Groups[1].ToString().Trim();
            }

            string race_class = null;
            var regex_class = new Regex(@"<ul>\s+<li>\s+(.*?)</li>", RegexOptions.Singleline);
            Match match_class = regex_class.Match(header);
            if (match_class.Success)
            {
                race_class = match_class.Groups[1].ToString().Trim().Replace("\n", "|");
            }

            string prize_money = null;
            var regex_prize_money = new Regex(@"</li>\s+<li>(.*?)</li>");
            Match match_prize_money = regex_prize_money.Match(header);
            if (match_prize_money.Success)
            {
                prize_money = match_prize_money.Groups[1].ToString().Trim();
            }

            //
            //  Footer fields
            //
            string footer = null;
            int? ran = null;
            string time = null;
            nonrunners = new List<NonRunner>();
            var regex_footer = new Regex("<div class=\"raceInfo\">(.*?)</div>", RegexOptions.Singleline);
            Match match_footer = regex_footer.Match(page);
            if (match_footer.Success)
            {
                footer = match_footer.Groups[1].ToString();

                var regex_ran = new Regex(@"<b>(\d+) ran</b>");
                Match match_ran = regex_ran.Match(footer);
                if (match_ran.Success)
                {
                    ran = Convert.ToInt32(match_ran.Groups[1].ToString());
                }

                var regex_race_time = new Regex(@"<b>TIME</b>([0-9ms\. ]*)");
                Match match_race_time = regex_race_time.Match(footer);
                if (match_race_time.Success)
                {
                    time = match_race_time.Groups[1].ToString().Trim();
                }

                var regex_nonrunners = new Regex("<b>NON RUNNER[S]{0,1}:</b>(.*?)<br />");
                Match match_nonrunners = regex_nonrunners.Match(footer);
                if (match_nonrunners.Success)
                {
                    string nonrunners_str = match_nonrunners.Groups[1].ToString().Trim();

                    var regex_nr =
                        new Regex(@"<a href.*?horse_id=(\d+)"".*?this HORSE"">([^<]+)</a>(?:\s*\(([^)]+)\)){0,1}");
                    Match match_nr = regex_nr.Match(nonrunners_str);
                    while (match_nr.Success)
                    {
                        var nr_horse = new NonRunner();
                        nr_horse.Id = Convert.ToInt32(match_nr.Groups[1].ToString());
                        nr_horse.Name = match_nr.Groups[2].ToString().Trim();
                        nr_horse.Country = match_nr.Groups[3].ToString().Trim();
                        if (nr_horse.Country == "")
                        {
                            nr_horse.Country = null;
                        }
                        nonrunners.Add(nr_horse);

                        match_nr = match_nr.NextMatch();
                    }
                }
            }

            Race race = SaveRace(scrape, start, name, race_class, prize_money, footer, ran, time);
            return race;
        }

        private static Race SaveRace(ScrapeRaceView scrape, DateTime start, string name, string race_class,
            string prize_money, string footer, int? ran, string time)
        {
            Race race = db_rph.Races.FirstOrDefault(x => x.Id == scrape.RaceId);
            if (race == null)
            {
                race = new Race();
                db_rph.Races.InsertOnSubmit(race);
                race.Id = (int) scrape.RaceId;
            }
            race.MeetingId = scrape.MeetingId;
            race.StartTime = start;
            race.Name = name;
            race.ClassRaw = race_class;
            race.PrizeMoney = prize_money;
            race.Runners = ran;
            race.Time = time;
            race.Notes = footer;

            var regex_class =
                new Regex(
                    @"(?:\(Class (\d)\) \| ){0,1}(?:\((?:([\d-]+), ){0,1}([\d-+yo]+)\)){0,1}(?: *\(([\dmfy]+)\)\|){0,1}(?:[| ]*([\dm&frac;]+)){0,1}(?: ([A-Za-z ]+)){0,1}(?:([\d]+) (?:fences|hdles)){0,1}(?: ([\d]+) omitted){0,1}");
            Match match_class = regex_class.Match(race.ClassRaw);
            if (match_class.Success)
            {
                ProcessClassRaw(scrape, race, match_class);

                ProcessRaceType(scrape, race);

                ProcessTime(race);

                ProcessGradeGroup(scrape, race);

                ProcessPrize(race);
            }
            else
            {
                Logger.WriteLog("Invalid Class field");
            }

            db_rph.SubmitChanges();

            return race;
        }

        private static void ProcessRaceType(ScrapeRaceView scrape, Race race)
        {
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
            else if (scrape.TrackTypeID != 1)
            {
                race.RaceType = 3;
            }
            else
            {
                race.RaceType = 4;
            }

            if (scrape.Country == "")
            {
                Season season =
                    seasons.FirstOrDefault(
                        x => race.RaceType == x.RaceType && race.StartTime >= x.DateFrom && race.StartTime <= x.DateTo);
                if (season != null)
                {
                    race.Season = season.Season1;
                    race.SeasonType = season.SeasonType;
                }
            }
        }

        private static void ProcessPrize(Race race)
        {
            var regex_prize = new Regex(@"&pound;([\d,]+).(\d+)");
            Match match_prize = regex_prize.Match(race.PrizeMoney);
            int ii = 0;
            while (match_prize.Success)
            {
                int pounds = Convert.ToInt32(match_prize.Groups[1].ToString().Replace(",", ""));
                int pence = Convert.ToInt32(match_prize.Groups[2].ToString());
                decimal prize = pounds + pence/100;

                ii++;
                switch (ii)
                {
                    case 1:
                        race.Prize1st = prize;
                        break;
                    case 2:
                        race.Prize2nd = prize;
                        break;
                    case 3:
                        race.Prize3rd = prize;
                        break;
                    case 4:
                        race.Prize4th = prize;
                        break;
                    case 5:
                        race.Prize5th = prize;
                        break;
                    case 6:
                        race.Prize6th = prize;
                        break;
                }

                match_prize = match_prize.NextMatch();
            }
        }

        private static void ProcessGradeGroup(ScrapeRaceView scrape, Race race)
        {
            var regexGroup = new Regex(@"(?:\(Group ([123])|\((Listed))");
            var regexGrade = new Regex(@"(?:(?:\(| )Grade ([123])|\((Listed))");

            if (scrape.Country == "" && race.Class == 1)
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

            if (scrape.Country == "IRE")
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

            if (scrape.Country == "ITY" || scrape.Country == "GER" || scrape.Country == "FR" || scrape.Country == "USA")
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

        private static void ProcessTime(Race race)
        {
            if (race.Time != null)
            {
                var regex_time = new Regex(@"(?:(\d)m){0,1}\s?(?:([\d.]+)s){0,1}");
                Match match_time = regex_time.Match(race.Time);
                if (match_time.Success)
                {
                    int minutes = 0;
                    double seconds = 0;
                    if (match_time.Groups[1].ToString() != "")
                    {
                        minutes = Convert.ToInt32(match_time.Groups[1].ToString());
                    }
                    if (match_time.Groups[2].ToString() != "")
                    {
                        seconds = Convert.ToDouble(match_time.Groups[2].ToString());
                    }
                    race.TimeSeconds = minutes*60 + seconds;
                }
            }
        }

        private static void ProcessClassRaw(ScrapeRaceView scrape, Race race, Match match_class)
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
                    race.DistanceYards = miles*1760 + furlongs*220 + yards;
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

            race.Handicap = race.Name.Contains("Handicap");

            TrackDirection direction =
                track_dirs.FirstOrDefault(x => x.CourseId == scrape.CourseId && race.DistanceYards >= x.DistFrom &&
                                               race.DistanceYards <= x.DistTo);
            if (direction != null)
            {
                race.TrackDirection = direction.Direction;
            }
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
                    race.DistanceYards = miles*1760 + furlongs*220 + half_furlong;
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
                Logger.WriteLog("Invalid Distance field");
            }
        }

        private static void ScrapeRunners(string page, ScrapeRaceView scrape, DateTime start)
        {
            int position = 0;
            var regex_runners = new Regex("<tbody>(.*?)</tbody>", RegexOptions.Singleline);
            Match match_runners = regex_runners.Match(page);
            while (match_runners.Success)
            {
                string runner_str = match_runners.Groups[1].ToString();
                if (runner_str.Length < 100)
                {
                    break;
                }

                position++;

                string horse_name = null;
                string horse_country = null;
                string price = null;
                var regex_horse =
                    new Regex(@"title=""Full details about this HORSE"">([^<]+)</a></b>\s+(\([^)]+\)){0,1}([^<]+)<",
                        RegexOptions.Singleline);
                Match match_horse = regex_horse.Match(runner_str);
                if (match_horse.Success)
                {
                    horse_name = match_horse.Groups[1].ToString().Trim();
                    horse_country = match_horse.Groups[2].ToString().Trim().Replace("(", "").Replace(")", "");
                    price = match_horse.Groups[3].ToString().Trim();
                }

                int placed = 0;
                string did_not_finish = null;
                var regex_placed = new Regex(@"<h3>(\S+)\s*</h3>");
                Match match_placed = regex_placed.Match(runner_str);
                if (match_placed.Success)
                {
                    string placed_str = match_placed.Groups[1].ToString();
                    if (!Int32.TryParse(placed_str, out placed))
                    {
                        did_not_finish = placed_str;
                    }
                }

                int? draw = null;
                var regex_draw = new Regex(@"<span class=""draw"">(\d+)</span>");
                Match match_draw = regex_draw.Match(runner_str);
                if (match_draw.Success)
                {
                    draw = Convert.ToInt32(match_draw.Groups[1].ToString());
                }

                string distance = null;
                var regex_distance = new Regex(@"<td rowspan=""2"" class=""dstDesc"">(.*?)</td>");
                Match match_distance = regex_distance.Match(runner_str);
                if (match_distance.Success)
                {
                    distance = match_distance.Groups[1].ToString().Trim();
                }

                int horse_id = 0;
                var regex_horse_id = new Regex(@"/horse_home\.sd\?horse_id=([^""]+)""");
                Match match_horse_id = regex_horse_id.Match(runner_str);
                if (match_horse_id.Success)
                {
                    horse_id = Convert.ToInt32(match_horse_id.Groups[1].ToString());
                }

                int? age = null;
                int? foal_year = null;
                var regex_age = new Regex(@"<td class=""black"">(\d+)</td>");
                Match match_age = regex_age.Match(runner_str);
                if (match_age.Success)
                {
                    age = Convert.ToInt32(match_age.Groups[1].ToString());
                    foal_year = start.Year - age;
                }

                string weight_raw = null;
                var regex_weight_raw = new Regex("<td class=\"nowrap black\"><span>(.*?)</span></td>");
                Match match_weight_raw = regex_weight_raw.Match(runner_str);
                if (match_weight_raw.Success)
                {
                    weight_raw = match_weight_raw.Groups[1].ToString().Trim();
                }

                string trainer_name = null;
                var regex_trainer = new Regex("\"Full details about this TRAINER\">([^<]+)</a>");
                Match match_trainer = regex_trainer.Match(runner_str);
                if (match_trainer.Success)
                {
                    trainer_name = match_trainer.Groups[1].ToString().Trim();
                }

                int trainer_id = 0;
                var regex_trainer_id = new Regex(@"/trainer_home\.sd\?trainer_id=(\d+)""");
                Match match_trainer_id = regex_trainer_id.Match(runner_str);
                if (match_trainer_id.Success)
                {
                    trainer_id = Convert.ToInt32(match_trainer_id.Groups[1].ToString());
                }

                string jockey_name = null;
                int? allowance = null;
                var regex_jockey = new Regex(@"Full details about this JOCKEY"">([^<]+)</a>(?:<sup>(\d+)</sup>){0,1}");
                Match match_jockey = regex_jockey.Match(runner_str);
                if (match_jockey.Success)
                {
                    jockey_name = match_jockey.Groups[1].ToString().Trim();
                    int allowance_int = 0;
                    if (Int32.TryParse(match_jockey.Groups[2].ToString().Trim(), out allowance_int))
                    {
                        allowance = allowance_int;
                    }
                }

                int jockey_id = 0;
                var regex_jockey_id = new Regex(@"jockey_home\.sd\?jockey_id=(\d+)""");
                Match match_jockey_id = regex_jockey_id.Match(runner_str);
                if (match_jockey_id.Success)
                {
                    jockey_id = Convert.ToInt32(match_jockey_id.Groups[1].ToString());
                }

                string or_rating = null;
                string ts_rating = null;
                string rpr_rating = null;
                var regex_ratings =
                    new Regex(
                        @" <td rowspan=""2"" class=""lightGray"">([^<]+)</td>\s+<td rowspan=""2"" class=""lightGray""><span class=""red bold"">([^<]+)</span></td>\s+<td rowspan=""2"" class=""last""><span class=""red bold"">([^<]+)</span></td>",
                        RegexOptions.Singleline);
                Match match_ratings = regex_ratings.Match(runner_str);
                if (match_ratings.Success)
                {
                    or_rating = match_ratings.Groups[1].ToString().Trim();
                    ts_rating = match_ratings.Groups[2].ToString().Trim();
                    rpr_rating = match_ratings.Groups[3].ToString().Trim();
                }

                string pedigree = null;
                var regex_pedigree = new Regex("<span class=\"pedigrees\">(.*?)</span?");
                Match match_pedigree = regex_pedigree.Match(runner_str);
                if (match_pedigree.Success)
                {
                    pedigree = match_pedigree.Groups[1].ToString().Trim();
                }

                string comment = null;
                var regex_comment = new Regex("<div class=\"commentText\">([^<]+)</div>");
                Match match_comment = regex_comment.Match(runner_str);
                if (match_comment.Success)
                {
                    comment = match_comment.Groups[1].ToString().Trim();
                }

                Horse horse = SaveHorse(horse_name, horse_country, horse_id, foal_year, pedigree);

                SaveRunner(scrape, position, price, placed, did_not_finish, draw, distance, horse_id, age, weight_raw,
                    trainer_id, allowance, jockey_id, or_rating, ts_rating, rpr_rating, comment, horse);

                if (trainer_id != 0)
                {
                    SaveTrainer(trainer_name, trainer_id);
                }

                if (jockey_id != 0)
                {
                    SaveJockey(jockey_name, jockey_id);
                }

                db_rph.SubmitChanges();

                match_runners = match_runners.NextMatch();
            }
        }

        private static void SaveJockey(string jockey_name, int jockey_id)
        {
            Jockey jockey = db_rph.Jockeys.FirstOrDefault(x => x.Id == jockey_id);
            if (jockey == null)
            {
                jockey = new Jockey();
                db_rph.Jockeys.InsertOnSubmit(jockey);
                jockey.Id = jockey_id;
                jockey.Name = jockey_name;
            }
        }

        private static void SaveTrainer(string trainer_name, int trainer_id)
        {
            Trainer trainer = db_rph.Trainers.FirstOrDefault(x => x.Id == trainer_id);
            if (trainer == null)
            {
                trainer = new Trainer();
                db_rph.Trainers.InsertOnSubmit(trainer);
                trainer.Id = trainer_id;
                trainer.Name = trainer_name;
            }
        }

        private static void SaveRunner(ScrapeRaceView scrape, int position, string price, int placed,
            string did_not_finish, int? draw, string distance, int horse_id, int? age, string weight_raw, int trainer_id,
            int? allowance, int jockey_id, string or_rating, string ts_rating, string rpr_rating, string comment,
            Horse horse)
        {
            Runner runner =
                db_rph.Runners.FirstOrDefault(x => x.HorseId == horse_id && x.RaceId == scrape.RaceId);
            if (runner == null)
            {
                runner = new Runner();
                runner.HorseId = horse.Id;
                runner.RaceId = scrape.RaceId;
                db_rph.Runners.InsertOnSubmit(runner);
            }
            runner.Status = "Runner";
            runner.Position = position;
            runner.Placed = placed;
            runner.DidNotFinish = did_not_finish;
            runner.Draw = draw;
            runner.Distance = distance;
            runner.Age = age;

            runner.Price = price;
            ProcessPrice(runner);

            runner.WeightRaw = weight_raw;
            ProcessWeight(runner);

            if (trainer_id != 0)
            {
                runner.TrainerId = trainer_id;
            }
            if (jockey_id != 0)
            {
                runner.JockeyId = jockey_id;
            }

            runner.Allowance = allowance;

            runner.OR_Rating = or_rating;
            if (runner.OR_Rating != null && runner.OR_Rating != "&mdash;")
            {
                runner.Rating = Convert.ToInt32(runner.OR_Rating);
            }

            runner.TS_Rating = ts_rating;
            runner.RPR_Rating = rpr_rating;
            runner.Comments = comment;
        }

        private static Horse SaveHorse(string horse_name, string horse_country, int horse_id, int? foal_year,
            string pedigree)
        {
            bool process_pedigree = false;
            Horse horse = db_rph.Horses.FirstOrDefault(x => x.RPId == horse_id);
            if (horse == null)
            {
                horse = new Horse() {PriorityProcess = true, DetailProcessed = 0};
                db_rph.Horses.InsertOnSubmit(horse);
                horse.RPId = horse_id;
            }
            horse.Name = Common.ProcessName(horse_name);
            horse.FlatName = Common.FlattenName(horse.Name);
            horse.Country = horse_country;
            if (horse.Country == "")
            {
                horse.Country = "GB";
            }
            horse.FoalYear = foal_year;

            if (pedigree != null && (horse.PedigreeRaw == null || horse.PedigreeRaw != pedigree))
            {
                horse.PedigreeRaw = pedigree;
                process_pedigree = true;
            }

            db_rph.SubmitChanges();

            if (process_pedigree)
            {
                ProcessPedigree(horse);
            }

            return horse;
        }

        private static void ProcessPrice(Runner runner)
        {
            switch (runner.Price)
            {
                case "":
                    runner.Price = "No Odds";
                    break;
                case "Evs":
                case "Evens":
                    runner.Numerator = 1;
                    runner.Denominator = 1;
                    runner.Favourite = false;
                    break;
                case "EvsF":
                case "EvensF":
                case "EvsJ":
                case "EvensJ":
                    runner.Numerator = 1;
                    runner.Denominator = 1;
                    runner.Favourite = true;
                    break;
                default:
                    var regex_price = new Regex(@"(\d+)/(\d+)(F|J?)");
                    Match match_price = regex_price.Match(runner.Price);
                    if (match_price.Success)
                    {
                        runner.Numerator = Convert.ToInt32(match_price.Groups[1].ToString());
                        runner.Denominator = Convert.ToInt32(match_price.Groups[2].ToString());
                        runner.Favourite = match_price.Groups[3].ToString() != "";
                    }
                    break;
            }
            runner.PriceProcessed = true;
        }

        private static void ProcessWeight(Runner runner)
        {
            if (runner.WeightRaw != null)
            {
                var regex_main =
                    new Regex(
                        @"(\d+)-(\d+)(?:<img.*?weight-ew.*?>(\d+)){0,1}(?:<img.*?weight-ow.*?>(\d+)){0,1}(?:<img.*?weight-oh.*?>(\d+)){0,1}&nbsp;<span class=""lightGray"">([^<]*)(?:<sup>(\d+)</sup>){0,1}</span>");
                Match match_main = regex_main.Match(runner.WeightRaw);
                if (match_main.Success)
                {
                    runner.Weight = Convert.ToInt32(match_main.Groups[1].ToString())*14 +
                                    Convert.ToInt32(match_main.Groups[2].ToString());

                    int parse_int = 0;
                    if (Int32.TryParse(match_main.Groups[3].ToString(), out parse_int))
                    {
                        runner.Weight_EW = parse_int;
                    }

                    if (Int32.TryParse(match_main.Groups[4].ToString(), out parse_int))
                    {
                        runner.Weight_OW = parse_int;
                    }

                    if (Int32.TryParse(match_main.Groups[5].ToString(), out parse_int))
                    {
                        runner.Weight_OH = parse_int;
                    }

                    runner.Weight_Desc = match_main.Groups[6].ToString();
                    string weight_sup = match_main.Groups[7].ToString();
                    if (weight_sup != "")
                    {
                        runner.Weight_Desc += "-" + weight_sup;
                    }

                    runner.WeightProcessed = 1;
                }
                else
                {
                    runner.WeightProcessed = -1;
                }
            }
            else
            {
                runner.WeightProcessed = -2;
            }
        }

        private static void GetRace(string link, ref string status, ref string page)
        {
            var uri = new Uri(link);
            var request = (HttpWebRequest) WebRequest.Create(uri);
            request.UserAgent =
                "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";
            request.Referer = "";
            request.Timeout = 30000;
            request.Method = "GET";

            try
            {
                var response = (HttpWebResponse) request.GetResponse();
                Stream stream = response.GetResponseStream();
                Encoding enc = Encoding.GetEncoding(28591); // iso-8859-1
                var reader = new StreamReader(stream, enc);
                page = reader.ReadToEnd();
                reader.Close();
                stream.Close();

                status = "Complete";
            }
            catch (WebException webexcpt)
            {
                switch (webexcpt.Status)
                {
                    case WebExceptionStatus.NameResolutionFailure:
                        status = "Host name not resolved";
                        break;
                    case WebExceptionStatus.Timeout:
                        status = "Timeout";
                        break;
                    case WebExceptionStatus.ProtocolError:
                        if (webexcpt.Message.IndexOf("404") != 0)
                        {
                            status = "Page not found";
                        }
                        else
                        {
                            status = "Other error" + webexcpt.Message;
                        }
                        break;
                    default:
                        status = "Other error " + webexcpt.Message;
                        break;
                }
            }
        }

        private static void ProcessDistBeaten(Race race)
        {
            List<Runner> runners = db_rph.Runners.Where(x => x.RaceId == race.Id).ToList();
            double dist_beaten = 0;
            foreach (Runner runner in runners.Where(x => x.Status == "Runner" && x.Placed != 0).OrderBy(x => x.Position)
                )
            {
                if (runner.Distance == "" && runner.Position != 1)
                {
                    break;
                }

                double dist = 0;
                switch (runner.Distance)
                {
                    case "":
                    case "dht":
                        dist = 0;
                        break;
                    case "dist":
                        dist = 100;
                        break;
                    case "hd":
                        dist = 0.2;
                        break;
                    case "nk":
                        dist = 0.3;
                        break;
                    case "nse":
                    case "shd":
                        dist = 0.1;
                        break;
                    default:
                        var regex_dist = new Regex(@"(\d*)(?:&frac(\d+);){0,1}");
                        Match match_dist = regex_dist.Match(runner.Distance);
                        if (match_dist.Success)
                        {
                            int units = 0;
                            double fract = 0;
                            Int32.TryParse(match_dist.Groups[1].ToString(), out units);
                            switch (match_dist.Groups[2].ToString())
                            {
                                case "14":
                                    fract = 0.25;
                                    break;
                                case "12":
                                    fract = 0.5;
                                    break;
                                case "34":
                                    fract = 0.75;
                                    break;
                            }

                            dist = units + fract;
                        }
                        break;
                }

                dist_beaten += dist;
                runner.DistBeaten = dist_beaten;
            }

            db_rph.SubmitChanges();
        }

        private static void ProcessPedigree(Horse horse)
        {
            var regex_main = new Regex(@"(.*?)<a href=(.*?)</a> - <a href=(.*?)</a> \(<a href=(.*?)</a>\)");
            Match match_main = regex_main.Match(horse.PedigreeRaw);
            if (match_main.Success)
            {
                string colour_sex = match_main.Groups[1].ToString().Trim();
                string sire_string = match_main.Groups[2].ToString().Trim();
                string dam_string = match_main.Groups[3].ToString().Trim();
                string dam_sire_string = match_main.Groups[4].ToString().Trim();

                var regex_colour_sex = new Regex("([a-z/]+)");
                MatchCollection matches_colour_sex = regex_colour_sex.Matches(colour_sex);
                if (matches_colour_sex.Count == 1)
                {
                    horse.Sex = matches_colour_sex[0].Groups[1].ToString().Trim();
                }
                else if (matches_colour_sex.Count == 2)
                {
                    horse.Colour = matches_colour_sex[0].Groups[1].ToString().Trim();
                    horse.Sex = matches_colour_sex[1].Groups[1].ToString().Trim();
                }

                var regex_parent = new Regex(@"horse_id=(\d+)[^>]+>(.*?)(\([^)]+\)){0,1}$");

                int? sire_id = null;
                string sire_name = null;
                string sire_country = null;
                Match match_sire = regex_parent.Match(sire_string);
                if (match_sire.Success)
                {
                    sire_id = Convert.ToInt32(match_sire.Groups[1].ToString());
                    sire_name = match_sire.Groups[2].ToString().Trim();
                    sire_country = match_sire.Groups[3].ToString().Trim().Replace("(", "").Replace(")", "");
                }

                int? dam_id = null;
                string dam_name = null;
                string dam_country = null;
                Match match_dam = regex_parent.Match(dam_string);
                if (match_dam.Success)
                {
                    dam_id = Convert.ToInt32(match_dam.Groups[1].ToString());
                    dam_name = match_dam.Groups[2].ToString().Trim();
                    dam_country = match_dam.Groups[3].ToString().Trim().Replace("(", "").Replace(")", "");
                }

                int? dam_sire_id = null;
                string dam_sire_name = null;
                string dam_sire_country = null;
                Match match_dam_sire = regex_parent.Match(dam_sire_string);
                if (match_dam_sire.Success)
                {
                    dam_sire_id = Convert.ToInt32(match_dam_sire.Groups[1].ToString());
                    dam_sire_name = match_dam_sire.Groups[2].ToString().Trim();
                    dam_sire_country = match_dam_sire.Groups[3].ToString().Trim().Replace("(", "").Replace(")", "");
                }

                if (sire_id != null)
                {
                    Horse sire_rec = db_rph.Horses.FirstOrDefault(x => x.RPId == sire_id);
                    if (sire_rec == null)
                    {
                        sire_rec = new Horse();
                        db_rph.Horses.InsertOnSubmit(sire_rec);
                        sire_rec.RPId = (int) sire_id;
                        sire_rec.PedigreeProcessed = 1;
                        sire_rec.DetailProcessed = 0;
                        sire_rec.PriorityProcess = true;
                    }

                    if (sire_rec.Name == null)
                    {
                        sire_rec.Name = Common.ProcessName(sire_name);
                        sire_rec.FlatName = Common.FlattenName(sire_name);
                    }

                    if (sire_rec.Country == null)
                    {
                        sire_rec.Country = sire_country;
                    }

                    db_rph.SubmitChanges();

                    if (horse.SireId == null)
                    {
                        horse.SireId = sire_rec.Id;
                    }
                }

                if (dam_id != null)
                {
                    Horse dam_rec = db_rph.Horses.FirstOrDefault(x => x.RPId == dam_id);
                    if (dam_rec == null)
                    {
                        dam_rec = new Horse();
                        db_rph.Horses.InsertOnSubmit(dam_rec);
                        dam_rec.RPId = (int) dam_id;
                        dam_rec.PedigreeProcessed = 1;
                        dam_rec.DetailProcessed = 0;
                        dam_rec.PriorityProcess = true;
                    }

                    if (dam_rec.Name == null)
                    {
                        dam_rec.Name = Common.ProcessName(dam_name);
                        dam_rec.FlatName = Common.FlattenName(dam_name);
                    }

                    if (dam_rec.Country == null)
                    {
                        dam_rec.Country = dam_country;
                    }

                    db_rph.SubmitChanges();

                    if (horse.DamId == null)
                    {
                        horse.DamId = dam_rec.Id;
                    }

                    if (dam_sire_id != null)
                    {
                        Horse dam_sire_rec = db_rph.Horses.FirstOrDefault(x => x.RPId == dam_sire_id);
                        if (dam_sire_rec == null)
                        {
                            dam_sire_rec = new Horse();
                            db_rph.Horses.InsertOnSubmit(dam_sire_rec);
                            dam_sire_rec.RPId = (int) dam_sire_id;
                            dam_sire_rec.PedigreeProcessed = 1;
                            dam_sire_rec.DetailProcessed = 0;
                            dam_sire_rec.PriorityProcess = true;
                        }

                        if (dam_sire_rec.Name == null)
                        {
                            dam_sire_rec.Name = Common.ProcessName(dam_sire_name);
                            dam_sire_rec.FlatName = Common.FlattenName(dam_sire_name);
                        }

                        if (dam_sire_rec.Country == null)
                        {
                            dam_sire_rec.Country = dam_sire_country;
                        }

                        db_rph.SubmitChanges();

                        if (dam_rec.SireId == null)
                        {
                            dam_rec.SireId = dam_sire_rec.Id;
                        }
                    }
                }

                horse.PedigreeProcessed = 1;
            }
            else
            {
                horse.PedigreeProcessed = -1;
            }

            db_rph.SubmitChanges();
        }
    }

    internal class NonRunner
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Country { get; set; }
    }
}