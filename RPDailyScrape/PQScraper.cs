using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace RPDailyScrape
{
    internal class PQScraper
    {
        private static Random rand;
        private static RacingPostRacesDataContext db_ppdb;

        public static void ScrapeUnmatched()
        {
            rand = new Random();
            db_ppdb = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());

            while (db_ppdb.Horses.Any(x => x.PPMatchBasis == "Failed RP_PP" && x.PriorityProcess))
            {
                foreach (Horse scrape in db_ppdb.Horses.Where(x => x.PPMatchBasis == "Failed RP_PP" && x.PriorityProcess).Take(500))
                {
                    Logger.WriteLog("Scrape PQ for " + scrape.Name + " " + scrape.Id);
                    scrape.PPMatchBasis = "Pending RP_PQ";

                    string outcome = "";
                    string pqid = "";
                    string status = "";

                    string page = "";

                    SearchHorse(scrape.Name, ref status, ref page);
                    if (status == "Complete" && page.Contains("can't be found in the database"))
                    {
                        var regex = new Regex("(.*) [ivxIVX]+$");
                        Match match = regex.Match(scrape.Name);
                        if (match.Success)
                        {
                            status = "";
                            SearchHorse(match.Groups[1].ToString(), ref status, ref page);
                        }
                    }

                    if (status == "Complete")
                    {
                        if (page.Contains("can't be found in the database"))
                        {
                            outcome = "not found";
                            Logger.WriteLog(scrape.Name + " not found\r\n");
                        }
                        else if (page.Contains("more than \none horse named "))
                        {
                            List<string> pqids = ProcessMultiple(scrape, page);
                            foreach (string multi_pqid in pqids)
                            {
                                if (multi_pqid == "skiing" || multi_pqid == "generator")
                                {
                                    continue;
                                }

                                GetHorse(multi_pqid, ref status, ref page);

                                if (status == "Complete")
                                {
                                    if (page.Contains("can't be found in the database"))
                                    {
                                        Logger.WriteLog(scrape.Name + " multiple not found\r\n");
                                    }
                                    else
                                    {
                                        ScrapePage(db_ppdb, page, ref outcome, ref pqid);
                                        db_ppdb.SubmitChanges();
                                    }
                                }
                                else
                                {
                                    Logger.WriteLog(multi_pqid + " Already retrieved\r\n");
                                }
                            }
                        }
                        else
                        {
                            ScrapePage(db_ppdb, page, ref outcome, ref pqid);
                            db_ppdb.SubmitChanges();
                        }
                        Logger.WriteLog(" ");
                    }

                    scrape.PQOutcome = "Retrieved " + outcome;
                    db_ppdb.SubmitChanges();
                }

                db_ppdb = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            }
        }

        public static void ScrapePedigree()
        {
            rand = new Random();

            db_ppdb = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            //var cmd = "UPDATE PQ_Horse SET PQOutcome = 'Rework' WHERE SireId IS NULL OR DamId IS NULL";
            //db_ppdb.ExecuteCommand(cmd);

            foreach (
                PQ_Horse scrape in
                    db_ppdb.PQ_Horses.Where(x => x.MergeMatchBasis == null && (x.SireId == null || x.DamId == null))
                        .OrderBy(x => x.Name))
            {
                Logger.WriteLog("Scrape PQ for " + scrape.Name + " " + scrape.Id);

                string outcome = "";
                string pqid = "";
                string status = "";

                // get the page
                string page = "";

                GetHorse(scrape.Id, ref status, ref page);

                if (status == "Complete")
                {
                    if (page.Contains("can't be found in the database"))
                    {
                        outcome = "not found";
                        Logger.WriteLog(scrape.Name + " not found\r\n");
                    }
                    else
                    {
                        ScrapePage(db_ppdb, page, ref outcome, ref pqid);
                        db_ppdb.SubmitChanges();
                    }
                }
                else
                {
                    outcome = status;
                    Logger.WriteLog(scrape.Name + " " + status + "\r\n");
                }

                scrape.PQOutcome = "Retrieved " + outcome;
                db_ppdb.SubmitChanges();
            }
        }

        private static void Wait()
        {
            var interval = (int) Math.Floor(3 + (rand.NextDouble()*5));
            Thread.Sleep(interval*1000);
        }

        private static void SearchHorse(string name, ref string status, ref string page)
        {
            try
            {
                var uri = new Uri("http://www.pedigreequery.com/cgi-bin/new/check2.cgi");
                var request = (HttpWebRequest) WebRequest.Create(uri);
                request.UserAgent =
                    "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";
                request.Referer = "";
                request.Timeout = 30000;
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                var encoding = new ASCIIEncoding();
                string post_name = FlattenName(name, true);
                byte[] data =
                    encoding.GetBytes(
                        String.Format(
                            "query_type=check&search_bar=horse&wsid=1331117134&h={0}&g=5&inbred=Standard&x2=n",
                            post_name));
                request.ContentLength = data.Length;
                Stream stream = request.GetRequestStream();
                stream.Write(data, 0, data.Length);
                stream.Close();

                var response = (HttpWebResponse) request.GetResponse();
                stream = response.GetResponseStream();
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

            Wait();
        }

        private static void GetHorse(string pqid, ref string status, ref string page)
        {
            PQ_Horse pq_horse = db_ppdb.PQ_Horses.FirstOrDefault(x => x.Id == pqid);
            if (pq_horse != null && pq_horse.Sex != null)
            {
                status = "Already retrieved";
                return;
            }

            var uri = new Uri(String.Format("http://www.pedigreequery.com/{0}", pqid));
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

            Wait();
        }

        private static List<string> ProcessMultiple(Horse scrape, string page)
        {
            var ret = new List<string>();

            var regex_table =
                new Regex("<table cellspacing=0 width=600><tr><td class=header><b>Horse.*?</tr>(.*?)</table>",
                    RegexOptions.Singleline);
            Match match_table = regex_table.Match(page);
            if (match_table.Success)
            {
                string table = match_table.Groups[1].ToString();

                var regex_row = new Regex("<tr>(.*?)</tr", RegexOptions.Singleline);
                Match match_row = regex_row.Match(table);
                while (match_row.Success)
                {
                    string row = match_row.Groups[1].ToString();

                    string pqid = "";
                    string country = "";
                    int year = 0;
                    string sire = "";
                    string dam = "";

                    int col_num = 0;

                    var regex_col = new Regex("<td[^>]*>(.*?)</td", RegexOptions.Singleline);
                    Match match_col = regex_col.Match(row);
                    while (match_col.Success)
                    {
                        string col = match_col.Groups[1].ToString();

                        col_num++;
                        switch (col_num)
                        {
                            case 1:
                                var regex = new Regex(@"<a href='/([^']*)'", RegexOptions.Singleline);
                                Match match = regex.Match(col);
                                if (match.Success)
                                {
                                    pqid = match.Groups[1].ToString();
                                }

                                regex = new Regex(@"\(([A-Z]+)\)", RegexOptions.Singleline);
                                match = regex.Match(col);
                                if (match.Success)
                                {
                                    country = match.Groups[1].ToString();
                                }
                                break;
                            case 2:
                                regex = new Regex(@"(\d\d\d\d)", RegexOptions.Singleline);
                                match = regex.Match(col);
                                if (match.Success)
                                {
                                    year = Convert.ToInt32(match.Groups[1].ToString());
                                }
                                break;

                            case 5:
                                regex = new Regex(@"<a href=[^>]*>([^<]*)</a>", RegexOptions.Singleline);
                                match = regex.Match(col);
                                if (match.Success)
                                {
                                    sire = match.Groups[1].ToString();
                                }
                                break;
                            case 6:
                                regex = new Regex(@"<a href=[^>]*>([^<]*)</a>", RegexOptions.Singleline);
                                match = regex.Match(col);
                                if (match.Success)
                                {
                                    dam = match.Groups[1].ToString();
                                }
                                break;
                        }

                        match_col = match_col.NextMatch();
                    }

                    ret.Add(pqid);
                    match_row = match_row.NextMatch();
                }
            }

            return ret;
        }

        private static void ScrapePage(RacingPostRacesDataContext db_ppdb, string page, ref string outcome,
            ref string pqid)
        {
            List<PedigreeHorse> horses = PedigreeExtract.Extract(page);

            var added = new List<string>();
            foreach (PedigreeHorse horse in horses.OrderBy(x => x.Generation).ThenByDescending(x => x.Pedigree))
            {
                if (added.Contains(horse.PQId))
                {
                    continue;
                }

                PQ_Horse pq_horse = db_ppdb.PQ_Horses.FirstOrDefault(x => x.Id == horse.PQId);
                if (pq_horse == null)
                {
                    pq_horse = new PQ_Horse();
                    db_ppdb.PQ_Horses.InsertOnSubmit(pq_horse);
                    pq_horse.Id = horse.PQId;
                    pq_horse.Name = horse.Name;
                    pq_horse.FlatName = FlattenName(horse.Name, false);

                    added.Add(horse.PQId);
                }

                if (pq_horse.Country == null)
                {
                    pq_horse.Country = horse.Country;
                }

                if (pq_horse.FoalYear == null)
                {
                    pq_horse.FoalYear = horse.FoalYear;
                }

                if (pq_horse.Colour == null)
                {
                    pq_horse.Colour = horse.Colour;
                }

                if (pq_horse.Sex == null)
                {
                    pq_horse.Sex = horse.Sex;
                }

                if (pq_horse.SireId == null)
                {
                    PedigreeHorse sire1 = horses.FirstOrDefault(x => x.Pedigree == horse.Pedigree + "S");
                    if (sire1 != null)
                    {
                        pq_horse.SireId = sire1.PQId;
                    }
                }

                if (pq_horse.DamId == null)
                {
                    PedigreeHorse dam1 = horses.FirstOrDefault(x => x.Pedigree == horse.Pedigree + "D");
                    if (dam1 != null)
                    {
                        pq_horse.DamId = dam1.PQId;
                    }
                }

                if (horse.Generation == 0)
                {
                    pq_horse.Starts = horse.Starts;
                    pq_horse.Wins = horse.Wins;
                    pq_horse.Places = horse.Places;
                    pq_horse.Earnings = horse.Earnings;
                    pq_horse.Owner = horse.Owner;
                    pq_horse.Breeder = horse.Breeder;
                }

                Logger.WriteLog(horse.Pedigree + " " + horse.PQId + " " + horse.Name + " " + horse.Country + " " +
                                horse.FoalYear + " " + horse.Colour
                                + " " + horse.Starts + " " + horse.Wins + " " + horse.Places + " " + horse.Earnings +
                                " " + horse.Owner + " " + horse.Breeder);
            }
        }

        private static string FlattenName(string str, bool is_pq_name)
        {
            var regex = new Regex(@"[^a-zA-Z0-9 ]");
            if (!is_pq_name)
            {
                str = str.ToUpper();
                regex = new Regex(@"[^A-Z0-9]");
            }
            str = regex.Replace(str, "");
            str = str.Replace(" ", "+");
            return str;
        }
    }
}