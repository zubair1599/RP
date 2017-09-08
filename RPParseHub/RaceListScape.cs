using HtmlAgilityPack;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RPParseHub
{
    public static class RaceListScape
    {
        public static string baseUrl = "https://www.racingpost.com";

        public static List<RPCourse> AllCourses { get; set; }

        public static void ScrapeRaceList(DateTime endDate)
        {
            using (RacingPostRacesEntities db = new RacingPostRacesEntities())
            {
                var startDate = db.ScrapeCourses.FirstOrDefault().LastDateScraped;

                //Check current date has all link downlaoded
                var alreadyDownloaded = db.ScrapeRaces.Select(s => s.RaceId).ToList();
                AllCourses = db.RPCourses.ToList();
               
                //scrape page start

                while (startDate <= endDate)
                {
                    Thread.Sleep(2000);
                    Console.Write(string.Format("Scrape links for date {0} \n", startDate));

                    DownloadRaceList(startDate, alreadyDownloaded,db);
                    db.ScrapeCourses.FirstOrDefault().LastDateScraped = startDate;
                    db.SaveChanges();
                    startDate = startDate.AddDays(1);
                }

            }

        }

        public static void DownloadRaceList(DateTime date, List<int?> raceIds, RacingPostRacesEntities db)
        {
            string country;
            var url = string.Format(@"https://www.racingpost.com/results/{0}/time-order", String.Format("{0:yyyy-MM-dd}", date));
            var Browser = new ScrapingBrowser();
            Browser.AllowAutoRedirect = true; // Browser has many settings you can access in setup
            Browser.AllowMetaRedirect = true;
            //go to the home page
            //var PageResult = Browser.NavigateToPage(new Uri(url));
            var web = new HtmlWeb();
            var doc = web.Load(url);
            
            var nodes = doc.QuerySelectorAll("div .rp-timeView__raceInfo").ToList();

           // List<HtmlNode> nodes = doc.QuerySelectorAll("div .rp-timeView__buttons > a").ToList();
            foreach (var item in nodes)
            {
                var courseUrl = baseUrl + item.ChildNodes[1].ChildNodes[1].Attributes["href"].Value;
                var courseId = Helper.GetIdfromUrl(courseUrl, "https://www.racingpost.com/profile/course/");

                string raceUrl = "";
                if (item.ChildNodes[3].ChildNodes[1].Attributes.Any(a=>a.Name == "href"))
                {
                    raceUrl= baseUrl + item.ChildNodes[3].ChildNodes[1].Attributes["href"].Value;
                }
                else
                {
                    continue;
                }
             
               
                int? raceId = Convert.ToInt32(raceUrl.Split('/').LastOrDefault());
                if(!raceIds.Any(r=> r == raceId))
                {
                    //save url to be scraped
                    ScrapeRace scrapeRace = new ScrapeRace();
                    scrapeRace.Link = raceUrl;
                    scrapeRace.RaceId = raceId;
                    scrapeRace.RaceDate = date;
                    scrapeRace.Scraped = false;
                    scrapeRace.Required = true;
                    scrapeRace.CourseUrl = courseUrl;
                    var course = AllCourses.Where(c => c.Id == courseId).FirstOrDefault();
                    if(course== null)
                    {
                        RPCourse c = new RPCourse { Id = courseId, Name = courseUrl.Split('/').LastOrDefault().ToUpper() };
                        db.RPCourses.Add(c);
                        db.SaveChanges();
                        AllCourses.Add(c);
                    }
                    country = AllCourses.Where(c => c.Id == courseId).FirstOrDefault().Country;
                    scrapeRace.Country = string.IsNullOrEmpty(country) ? "GB" : country;
                    db.ScrapeRaces.Add(scrapeRace);
                    db.SaveChanges();
                }
            }
           
        }
    }
}
