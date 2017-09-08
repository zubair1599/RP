using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RPDailyScrape
{
    internal class DateScraper
    {
        private static RacingPostRacesDataContext db_rph;

        public static void ScrapeDates(DateTime to_date)
        {
            try
            {
                Common.rand = new Random();
                db_rph = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());

                //todo change LastDateScraped on 240 server
                //todo new scraperace structure on 240 server
                //todo amend ScrapeRaceView
                DateTime date_scrape = db_rph.ScrapeCourses.Select(x => x.LastDateScraped).FirstOrDefault();
                if (date_scrape == null)
                {
                    Logger.WriteLog("Last date scraped missing");
                    Environment.Exit(-1);
                }
                else
                {
                    date_scrape = date_scrape.AddDays(1);
                }

                while (date_scrape <= to_date)
                {
                    Logger.WriteLog("Scraping " + date_scrape.ToShortDateString());

                    string status = "";
                    string page = "";
                    GetDate(date_scrape, ref status, ref page);

                    if (status == "Complete")
                    {
                        ScrapeDatePage(page, date_scrape);
                        RaceScraper.ScrapeRaces();
                        HorseScraper.ScrapeHorses();
                        //Matcher.Match_RP_PP();
                        //PQScraper.ScrapeUnmatched();
                        //PQScraper.ScrapePedigree();
                        //Matcher.Match_RP_PQ();
                        //Matcher.Match_PQ_PP();
                        //Matcher.Merge_RP_PQ_PP();
                        //Matcher.Add_RP_Merge();
                    }
                    else
                    {
                        Logger.WriteLog("Page retrieval failed: " + status);
                        Environment.Exit(-1);
                    }

                    ScrapeCourse scr_rec = db_rph.ScrapeCourses.FirstOrDefault();
                    if (scr_rec == null)
                    {
                        scr_rec = new ScrapeCourse();
                        db_rph.ScrapeCourses.InsertOnSubmit(scr_rec);
                    }
                    scr_rec.LastDateScraped = date_scrape;
                    db_rph.SubmitChanges();

                    Common.Wait();
                    date_scrape = date_scrape.AddDays(1);
                    db_rph = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
                }
            }
            catch (Exception exception)
            {
                
                throw exception;

            }
        }

        private static void ScrapeDatePage(string page, DateTime date)
        {
            string meeting_head = "";
            string meeting_tail = "";
            string races = "";

            string re1 = "(<)"; // Any Single Character 1
            string re2 = "(div)";   // Word 1
            string re3 = "( )"; // Any Single Character 2
            string re4 = "(class)"; // Word 2
            string re5 = "(=)"; // Any Single Character 3
            string re6 = "(\"rp-resultsWrapper__content\")";    // Double Quote String 1
            string re7 = ".*?"; // Non-greedy match on filler
            string re8 = "()";	// Tag 1
      string re9 = ".*?";   // Non-greedy match on filler
            string re10 = "(<\\/h1>)";  // Tag 2
            string re11 = "(<\\/div>)"; // Tag 3

            Regex r = new Regex(re1 + re2 + re3 + re4 + re5 + re6 + re7 + re8 + re9 + re10 + re11, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            //var regex_meeting =
            //    new Regex(@"(<)(div)( )(class)(=)(")(rp)(-)(resultsWrapper__content)(")(>)(<[^>]+>).*?<.*?<.*?(<)(\/)(h1)(>)(<)(\/div)(>)",
            //        RegexOptions.Singleline);
            Match match_meeting = r.Match(page);
            while (match_meeting.Success)
            {
                meeting_head = match_meeting.Groups[1].ToString();
                meeting_tail = match_meeting.Groups[2].ToString();
                races = match_meeting.Groups[3].ToString();

                var regex_course =
                    new Regex(
                        @"<a href=""http://www.racingpost.com/horses/course_home.sd\?crs_id=(\d+)[^>]+>([^<]+)</a>");
                Match match_course = regex_course.Match(meeting_head);
                if (match_course.Success)
                {
                    int course_id = Convert.ToInt32(match_course.Groups[1].ToString());
                    string course_whole = match_course.Groups[2].ToString().Replace("(AW)", ":AW:");

                    string course = "";
                    string country = "";
                    var regex_course2 = new Regex(@"([^(]+)(\([^(]+\)){0,1}");
                    Match match_course2 = regex_course2.Match(course_whole);
                    if (match_course2.Success)
                    {
                        course = match_course2.Groups[1].ToString().Replace(":AW:", "(AW)").Trim();
                        country = match_course2.Groups[2].ToString().Replace("(", "").Replace(")", "").Trim();
                    }

                    string going = "";
                    var regex_going = new Regex(@"<strong>GOING:</strong>([^<\n]+)", RegexOptions.Singleline);
                    Match match_going = regex_going.Match(meeting_tail);
                    if (match_going.Success)
                    {
                        going = match_going.Groups[1].ToString().Trim();
                    }

                    string weather = "";
                    var regex_weather = new Regex(@"<strong>Weather conditions:</strong>([^<\n]+)",
                        RegexOptions.Singleline);
                    Match match_weather = regex_weather.Match(meeting_tail);
                    if (match_weather.Success)
                    {
                        weather = match_weather.Groups[1].ToString().Trim();
                    }

                    string stalls = "";
                    var regex_stalls = new Regex(@"<strong>STALLS:</strong>([^<\n]+)", RegexOptions.Singleline);
                    Match match_stalls = regex_stalls.Match(meeting_tail);
                    if (match_stalls.Success)
                    {
                        stalls = match_stalls.Groups[1].ToString().Trim();
                    }

                    Course course_rec = db_rph.Courses.Where(x => x.Id == course_id).FirstOrDefault();
                    if (course_rec == null)
                    {
                        course_rec = new Course();
                        course_rec.Id = course_id;
                        db_rph.Courses.InsertOnSubmit(course_rec);
                    }

                    course_rec.Name = course;
                    course_rec.Country = country;
                    db_rph.SubmitChanges();

                    Meeting meeting_rec =
                        db_rph.Meetings.Where(x => x.CourseId == course_id && x.DateOfMeeting == date).FirstOrDefault();
                    if (meeting_rec != null)
                    {
                        string cmd = String.Format("DELETE FROM Meeting WHERE Id = {0}", meeting_rec.Id);
                        db_rph.ExecuteCommand(cmd);
                        cmd = String.Format("DELETE FROM ScrapeRace WHERE MeetingId = {0}", meeting_rec.Id);
                        db_rph.ExecuteCommand(cmd);
                    }

                    meeting_rec = new Meeting();
                    db_rph.Meetings.InsertOnSubmit(meeting_rec);
                    meeting_rec.CourseId = course_id;
                    meeting_rec.DateOfMeeting = date;
                    meeting_rec.Going = going;
                    meeting_rec.Weather = weather;
                    meeting_rec.Stalls = stalls;
                    db_rph.SubmitChanges();

                    var regex_race = new Regex(@"<td(.*?)</td>", RegexOptions.Singleline);
                    Match match_race = regex_race.Match(races);
                    while (match_race.Success)
                    {
                        string race = match_race.Groups[1].ToString();

                        string race_link = "";
                        int race_id = 0;
                        var regex_race_link = new Regex(@"<a href=""(/horses/result_home\.sd\?race_id=(\d+)[^""]+)""",
                            RegexOptions.Singleline);
                        Match match_race_link = regex_race_link.Match(race);
                        if (match_race_link.Success)
                        {
                            race_link = match_race_link.Groups[1].ToString().Trim();
                            race_id = Convert.ToInt32(match_race_link.Groups[2].ToString().Trim());

                            var race_rec = new ScrapeRace();
                            db_rph.ScrapeRaces.InsertOnSubmit(race_rec);
                            race_rec.MeetingId = meeting_rec.Id;
                            race_rec.Link = "http://www.racingpost.com" + race_link;
                            race_rec.Scraped = false;
                            race_rec.RaceId = race_id;
                            race_rec.RaceDate = date;

                            db_rph.SubmitChanges();
                        }

                        match_race = match_race.NextMatch();
                    }
                }

                match_meeting = match_meeting.NextMatch();
            }
        }

        private static void GetDate(DateTime date, ref string status, ref string page)
        {
            var uri =
                new Uri(String.Format("https://www.racingpost.com/results/2017-02-02/time-order",
                    date.ToString("yyyy-MM-dd")));
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
    }
}