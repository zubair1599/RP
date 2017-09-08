using Newtonsoft.Json;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPParseHub
{
    public static class HorseScrape
    {
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
            Console.Write(string.Format("Downlaod horse {0} name {1} \n", result.profile.horseUid, result.profile.horseName));
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
