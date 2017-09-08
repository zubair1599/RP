using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;


namespace RPParseHub
{
    class Program
    {
        public static List<string> DidNotFinsh = new List<string>{ "BD", "CO", "DSQ", "F", "LFT", "PU", "REF", "RO", "RR", "SU", "UR", "VOI", "WDU" };

        static void Main(string[] args)
        {
             RaceListScape.ScrapeRaceList(new DateTime(2017,08,31));
            //RaceScraper.ScrapeRace();
            // HorseScrape(args);
            // RaceScrape(args);
            //DamDownload();
            // RaceDownload();
        }

        static void RaceScrape(string[] args)
        {

            // SQL.Program1.Main1() ;
          //  JObject o1 = JObject.Parse(File.ReadAllText(@"C:\Users\MuhammadZubair\Documents\BELData\run_results-withoutHorse(Feb).json"));
            List<int> horseIds = new List<int>();
            
            using (StreamReader file = File.OpenText(@"C:\Users\MuhammadZubair\Documents\BELData\run_results-withoutHorse(Feb).json"))

            using (JsonTextReader reader = new JsonTextReader(file))
            {
                JObject o2 = (JObject)JToken.ReadFrom(reader);
                JsonSerializer serializer = new JsonSerializer();
                //var aa= JsonConvert.DeserializeObject<RPData>(text);
                var rpData = o2.ToObject<RPData>();//.Deserialize<RPData>(reader);
                Cleaner.GetBaseData();
                // Cleaner.GetFooter();
                int raceCount = 1;
                foreach (Race item in rpData.AllRaces)
                {
                    raceCount++;

                    if (item.url == null) continue;
                    item.ClassRaw = item.ClassRaw.Replace(item.PrizeMoney, "");
                    item.ClassRaw = item.ClassRaw.Replace("\n", "");
                    if (item.ClassRaw.Contains("Class"))
                    {
                        item.ClassRaw = item.ClassRaw.Insert(item.ClassRaw.IndexOf(")") + 1, " |");
                        //item.ClassRaw = item.ClassRaw.Insert(item.ClassRaw.LastIndexOf(")") + 1, "|| ");
                    }
                    if (item.ClassRaw.Contains(") ("))
                    {
                        item.ClassRaw = item.ClassRaw.Insert(item.ClassRaw.LastIndexOf(")") + 1, "|| ");
                    }
                    else
                    {
                        item.ClassRaw = item.ClassRaw.Insert(item.ClassRaw.LastIndexOf(")") + 1, " | ");

                    }
                    if (item.ClassRaw.Contains("yds"))
                    {
                        item.ClassRaw = item.ClassRaw.Replace("yds", "y");
                    }
                    // item.ClassRaw = item.ClassRaw.Replace(") ", ") | ");
                    Cleaner.ProcessClass(item);
                    Cleaner.GetFooter(item);
                    Cleaner.ProcessWt(item);
                    SaveRace(item);

                    horseIds.AddRange(item.Runners.Select(h => h.SubjectHorse_Id));
                }

                //below part is used for extracting new horse ids
                //horseIds = horseIds.Distinct().ToList();
                //foreach (var item in horseIds)
                //{
                //    System.Diagnostics.Trace.Write(item + " ,\n");
                //}

                // Cleaner.ProcessClass(rpData.AllRaces[20]);//  "(Class 4) | (4yo+) (2m5f82y)| | 2m5&frac12;f Heavy 10 hdles 1 omitted");
            }
        }

        public static void SaveRace(Race race)
        {
            if (race.Id == 0) throw new ArgumentNullException();
            using (RacingPostRacesEntities db = new RacingPostRacesEntities())
            {
                if (db.RPRaces.FirstOrDefault(r => r.Id == race.Id) == null)
                {
                    
                    RPRace rpRace = new RPRace();
                    rpRace.Id = race.Id;
                    rpRace.CourseId = race.CourseId;
                    rpRace.StartTime = race.StartTime;
                    rpRace.Name = race.Name;
                    rpRace.RaceType = race.RaceType;
                    rpRace.Handicap = race.Handicap;
                    rpRace.Chase = race.Chase;
                    rpRace.Fences = race.Fences;
                    rpRace.FencesOmitted = race.FencesOmitted;
                    rpRace.FencesHurdles = race.FencesHurdles;
                    rpRace.ClassRaw = race.ClassRaw;
                    rpRace.Class = race.Class;
                    rpRace.GradeGroup = race.GradeGroup;
                    rpRace.Rating = race.Rating;
                    rpRace.Eligibility = race.Eligibility;
                    rpRace.DistanceYards = race.DistanceYards;
                    rpRace.DistanceStd = race.DistanceStd;
                    rpRace.Distance = race.Distance;
                    rpRace.Going = race.Going;
                    rpRace.PrizeMoney = race.PrizeMoney;
                    rpRace.Prize1st = race.Prize1st;
                    rpRace.Prize2nd = race.Prize2nd;
                    rpRace.Prize3rd = race.Prize3rd;
                    rpRace.Prize4th = race.Prize4th;
                    rpRace.Prize5th = race.Prize5th;
                    rpRace.Prize6th = race.Prize6th;
                    rpRace.CurrencyUnit = race.CurrencyUnit;
                    rpRace.Runners = Convert.ToInt32(race.NoOfRunners);
                    rpRace.Time = race.WinTime;
                    rpRace.NonRunners = race.NonRunners;
                    rpRace.PostTemplate = true;
                    db.RPRaces.Add(rpRace);
                    SaveRunner(race);

                    db.SaveChanges();
                }
            }
        }

        public static void SaveRunner(Race race)
        {
            using (RacingPostRacesEntities db = new RacingPostRacesEntities())
            {
                int? prevPos = 0;
                foreach (var runner in race.Runners)
                {
                    RPRunner rpRunner = new RPRunner();
                    rpRunner.HorseId = runner.HorseId;
                    rpRunner.RaceId = race.Id;
                    if (DidNotFinsh.Any(df => df.Equals(runner.PosTemp)))
                    {
                        rpRunner.DidNotFinish = runner.PosTemp;
                        rpRunner.Position = prevPos+1;
                        prevPos = rpRunner.Position;
                    }
                    else
                    {
                        rpRunner.Position = Convert.ToInt32(runner.PosTemp);
                        prevPos = rpRunner.Position;
                    }
                    rpRunner.Status = "Runner";
                    rpRunner.Draw = string.IsNullOrEmpty(runner.Draw) ? 0 : Convert.ToInt32(runner.Draw);
                    rpRunner.Distance = runner.Distance;
                    // rpRunner.DistBeaten = Convert.ToDouble(runner.DistBeaten);
                    rpRunner.Price = runner.SP;
                    rpRunner.WeightRaw = runner.WeightRaw;
                    rpRunner.Age = Convert.ToInt32(runner.Age);

                    if (db.Jockeys.FirstOrDefault(j => j.Id == runner.JockeyId) == null)
                    {
                        db.Jockeys.Add(new Jockey { Id = runner.JockeyId, Name = runner.Jockey });
                    }
                    if (db.Trainers.FirstOrDefault(j => j.Id == runner.TrainerId) == null)
                    {
                        db.Trainers.Add(new Trainer { Id = runner.TrainerId, Name = runner.Trainer });
                    }

                    rpRunner.JockeyId = runner.JockeyId;
                    rpRunner.TrainerId = runner.TrainerId;
                    rpRunner.PostTemplate = true;
                    SaveHorse(new Horse { Id = (int)rpRunner.HorseId });
                    db.RPRunners.Add(rpRunner);
                    db.SaveChanges();
                }
            }
        }

        public static void SaveHorse(Horse horse)
        {
            if (horse.Id == 0) throw new ArgumentNullException();
            using (RacingPostRacesEntities db = new RacingPostRacesEntities())
            {
                if (db.RPHorses.FirstOrDefault(h => h.RPId == horse.Id) == null)
                {
                    RPHorse rpHorse = new RPHorse();
                    rpHorse.RPId = horse.Id;
                    rpHorse.Name = horse.Name;
                    rpHorse.FoalDate = Convert.ToDateTime(horse.DOB);
                    rpHorse.Country = horse.Country;
                    rpHorse.Colour = horse.Color;
                    rpHorse.Sex = horse.Sex;
                    rpHorse.SireId = horse.SireId;
                    rpHorse.DamId = horse.DamId;
                    db.RPHorses.Add(rpHorse);
                   // db.SaveChanges();
                }
            }

        }

        static void HorseScrape(string[] args)
        {

            List<int> horseIds = new List<int>();

            RPHorse rpHorse;

            using (StreamReader file = File.OpenText(@"C:\Users\MuhammadZubair\Documents\BELData\horse(Feb)(2).json"))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                JObject o2 = (JObject)JToken.ReadFrom(reader);
                JsonSerializer serializer = new JsonSerializer();
                //var aa= JsonConvert.DeserializeObject<RPData>(text);
                var rpHorses = o2.ToObject<RPHorses>();//.Deserialize<RPData>(reader);
                Cleaner.GetBaseData();
                // Cleaner.GetFooter();
                int raceCount = 1;
                using (RacingPostRacesEntities db = new RacingPostRacesEntities())
                {
                    foreach (var item in rpHorses.Horses)
                    {


                        HorseCleaning.PrcessHorse(item);
                        HorseCleaning.PrcessHeader(item);
                        raceCount++;

                        rpHorse = new RPHorse();
                        rpHorse.Name = item.Name;
                        rpHorse.RPId = item.Id;
                        rpHorse.Country = item.Country;
                        rpHorse.Colour = item.Color;
                        rpHorse.Sex = item.Sex;
                        rpHorse.SireId = item.SireId;
                        rpHorse.DamId = item.DamId;
                        rpHorse.FoalDate = item.DOB;
                        rpHorse.FoalYear = item.DOB.Year;
                        rpHorse.PostTemplate = true;
                     
                        db.RPHorses.Add(rpHorse);
                    }
                    db.SaveChanges();
                }
                
                //horseIds = horseIds.Distinct().ToList();
                //foreach (var item in horseIds)
                //{
                //    System.Diagnostics.Trace.Write(item + " ,\n");
                //}

                // Cleaner.ProcessClass(rpData.AllRaces[20]);//  "(Class 4) | (4yo+) (2m5f82y)| | 2m5&frac12;f Heavy 10 hdles 1 omitted");
            }
        }

        static void SireDownload()
        {
            bool fillSire = true;
            while (fillSire)
            {
                using (RacingPostRacesEntities db = new RacingPostRacesEntities())
                {

                    var missingSire = db.RPHorses.SqlQuery(@"select * from horse  h 
                                                    left outer join horse sire on sire.rpid = h.sireid
                                                     where h.PostTemplate = 1 and sire.rpid is null and h.sireid is not null").ToList();

                    Console.Write(string.Format("Total missing sire : {0} \n", missingSire.Count));

                    HorseDownload(missingSire.Select(s => s.SireId).ToList(), db);

                    missingSire = db.RPHorses.SqlQuery(@"select * from horse  h 
                                                    left outer join horse sire on sire.rpid = h.sireid
                                                    where h.PostTemplate = 1 and sire.rpid is null and h.sireid is not null").ToList();
                    if (!missingSire.Any())
                        fillSire = false;
                }
               
            }
        }

        static void DamDownload()
        {
            bool filldam = true;
            while (filldam)
            {
                using (RacingPostRacesEntities db = new RacingPostRacesEntities())
                {

                    var missingdam = db.RPHorses.SqlQuery(@"select * from horse  h 
                                                    left outer join horse dam on dam.rpid = h.damid
                                                     where h.PostTemplate = 1 and dam.rpid is null and h.damid is not null").ToList();

                    Console.Write(string.Format("Total missing dam : {0} \n", missingdam.Count));

                    HorseDownload(missingdam.Select(s => s.DamId).ToList(), db);

                    missingdam = db.RPHorses.SqlQuery(@"select * from horse  h 
                                                    left outer join horse dam on dam.rpid = h.damid
                                                    where h.PostTemplate = 1 and dam.rpid is null and h.damid is not null").ToList();
                    if (!missingdam.Any())
                        filldam = false;
                }

            }
        }

        static void HorseDownload(List<int?> horseIds, RacingPostRacesEntities db)
        {
            int count = 1;
            foreach (var Id in horseIds)
            {
                Console.Write(string.Format("Processng {0} out of {1}", count, horseIds.Count));

                var url = @"https://www.racingpost.com/profile/horse/" + Id + "/";
                var Browser = new ScrapingBrowser();
                Browser.AllowAutoRedirect = true; // Browser has many settings you can access in setup
                Browser.AllowMetaRedirect = true;
                //go to the home page
                var PageResult = Browser.NavigateToPage(new Uri(url));
                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(PageResult.Content);
                var script = doc.DocumentNode.ChildNodes[2].ChildNodes[3].Descendants()
                         .Where(n => n.Name == "script")
                         .First().InnerText;

                var jsonStr = script.Substring(script.IndexOf("window.PRELOADED_STATE"));
                jsonStr = jsonStr.Replace("window.PRELOADED_STATE = ", "");
                jsonStr = jsonStr.Replace("})();", "");
                jsonStr = jsonStr.Replace("}};", "}}");

                var result = JsonConvert.DeserializeObject<RootObject>(jsonStr);
                SaveHorse(db, result);
                count++;
            }

        }

        private static void SaveHorse(RacingPostRacesEntities db, RootObject result)
        {
            Console.Write(string.Format("Downlaod horse {0} name {1} \n" , result.profile.horseUid, result.profile.horseName));
            RPHorse rpHorse = new RPHorse();
            rpHorse.Name = result.profile.horseName;
            rpHorse.RPId = result.profile.horseUid;
            rpHorse.Country = result.profile.horseCountryOriginCode;
            rpHorse.Colour = result.profile.horseColour;
            rpHorse.Sex = result.profile.horseSexCode;
            rpHorse.SireId = result.profile.sireUid;
            rpHorse.DamId = result.profile.damUid;
            rpHorse.FoalYear = Convert.ToDateTime(result.profile.horseDateOfBirth).Year;
            if (rpHorse.FoalYear > 1)
            {
                rpHorse.FoalDate = Convert.ToDateTime(result.profile.horseDateOfBirth);

            }
            else
            {
                rpHorse.FoalDate = Convert.ToDateTime("1/1/1753");
            }
            rpHorse.PostTemplate = true;
            db.RPHorses.Add(rpHorse);
            db.SaveChanges();
        }

    }
}
