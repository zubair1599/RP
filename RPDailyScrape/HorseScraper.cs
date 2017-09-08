using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RPDailyScrape
{
    internal class HorseScraper
    {
        private static RacingPostRacesDataContext db_rph;

        public static void ScrapeHorses()
        {
            Common.rand = new Random();
            db_rph = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());

            int count = db_rph.Horses.Count(x => x.DetailProcessed == 0 && x.PriorityProcess);

            while (db_rph.Horses.Any(x => x.DetailProcessed == 0 && x.PriorityProcess))
            {
                foreach (Horse scrape in db_rph.Horses.Where(x => x.DetailProcessed == 0 && x.PriorityProcess).Take(500))
                {
                    Logger.WriteLog((count--).ToString() + " - " + scrape.Name);

                    string status = "";
                    string page = "";
                    GetHorse((int)scrape.RPId, ref status, ref page);

                    scrape.DetailRaw = "Error";
                    scrape.DetailProcessed = -1;

                    if (status == "Complete")
                    {
                        var regex_name = new Regex(@"<h1>\s+([^(<]+)(?:\(([A-Z]+)\) ){0,1}</h1>");
                        Match match_name = regex_name.Match(page);
                        if (match_name.Success)
                        {
                            string country = match_name.Groups[2].ToString();
                            if (country == "")
                            {
                                country = "GB";
                            }
                            scrape.Country = country;
                        }

                        var regex_header = new Regex("<ul id=\"detailedInfo\">(.*?)</ul>", RegexOptions.Singleline);
                        Match match_header = regex_header.Match(page);
                        if (match_header.Success)
                        {
                            scrape.DetailRaw = match_header.Groups[1].ToString();
                            ProcessDetail(scrape);
                            scrape.DetailProcessed = 1;
                        }
                        else
                        {
                            Logger.WriteLog("Detail not found: " + scrape.Name);
                        }
                    }
                    else
                    {
                        Logger.WriteLog("Horse retrieval error " + status + " - " + scrape.Name);
                    }

                    db_rph.SubmitChanges();
                    Common.Wait();
                }

                db_rph = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            }
        }

        private static void GetHorse(int horseid, ref string status, ref string page)
        {
            var uri =
                new Uri(
                    String.Format(
                        "http://www.racingpost.com/horses/horse_home.sd?horse_id={0}#topHorseTabs=horse_race_record&bottomHorseTabs=horse_form",
                        horseid.ToString()));
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent =
                "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";
            request.Referer = "";
            request.Timeout = 30000;
            request.Method = "GET";

            try
            {
                var response = (HttpWebResponse)request.GetResponse();
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

        private static void ProcessDetail(Horse rec)
        {
            var regex_main = new Regex("<li>(.*?)</li>", RegexOptions.Singleline);
            Match match_main = regex_main.Match(rec.DetailRaw);
            int ii = 0;
            while (match_main.Success)
            {
                ii++;
                string item = match_main.Groups[1].ToString().Trim();

                if (ii == 1)
                {
                    var regex_dob_colour_sex =
                        new Regex(@"\((?:(\d{2})([A-Za-z]{3})(\d{2})){0,1}(?: ([a-z/]+)){0,1}(?: ([a-z/]+)){0,1}\s*\)");
                    Match match_dob_colour_sex = regex_dob_colour_sex.Match(item);
                    if (match_dob_colour_sex.Success)
                    {
                        if (match_dob_colour_sex.Groups[1].ToString() != "")
                        {
                            int dd = Convert.ToInt32(match_dob_colour_sex.Groups[1].ToString());
                            string mmm = match_dob_colour_sex.Groups[2].ToString();
                            int mm = 0;
                            switch (mmm)
                            {
                                case "Jan":
                                    mm = 1;
                                    break;
                                case "Feb":
                                    mm = 2;
                                    break;
                                case "Mar":
                                    mm = 3;
                                    break;
                                case "Apr":
                                    mm = 4;
                                    break;
                                case "May":
                                    mm = 5;
                                    break;
                                case "Jun":
                                    mm = 6;
                                    break;
                                case "Jul":
                                    mm = 7;
                                    break;
                                case "Aug":
                                    mm = 8;
                                    break;
                                case "Sep":
                                    mm = 9;
                                    break;
                                case "Oct":
                                    mm = 10;
                                    break;
                                case "Nov":
                                    mm = 11;
                                    break;
                                case "Dec":
                                    mm = 12;
                                    break;
                            }

                            int yy = Convert.ToInt32(match_dob_colour_sex.Groups[3].ToString());
                            int currentyear2Digit = DateTime.Now.Year % 2000;
                            if (yy <= currentyear2Digit)
                            {
                                yy += 2000;
                            }
                            else
                            {
                                yy += 1900;
                            }

                            if (mm != 0)
                            {
                                rec.FoalDate = new DateTime(yy, mm, dd);
                                rec.FoalYear = yy;
                            }
                        }

                        string field4 = match_dob_colour_sex.Groups[4].ToString().Trim();
                        string field5 = match_dob_colour_sex.Groups[5].ToString().Trim();

                        if (field4 != "" && field5 != "")
                        {
                            rec.Colour = field4;
                            rec.Sex = field5;
                        }
                        else if (field4 != "")
                        {
                            rec.Sex = field4;
                        }
                    }
                }
                else if (ii == 2)
                {
                    int? sire_id = null;
                    string sire_name = null;
                    string sire_country = null;
                    int? dam_id = null;
                    string dam_name = null;
                    string dam_country = null;
                    int? dam_sire_id = null;
                    string dam_sire_name = null;
                    string dam_sire_country = null;

                    var regex_sire =
                        new Regex(
                            @"stallionbook/stallion\.sd\?horse_id=(\d+).*?STALLION"">([^<(]+)(?:\(([A-Z]+)\)){0,1}</a>");
                    Match match_sire = regex_sire.Match(item);
                    ii = 0;
                    while (match_sire.Success)
                    {
                        ii++;
                        if (ii == 1)
                        {
                            sire_id = Convert.ToInt32(match_sire.Groups[1].ToString());
                            sire_name = match_sire.Groups[2].ToString().Trim();
                            sire_country = match_sire.Groups[3].ToString().Trim();
                        }
                        else if (ii == 2)
                        {
                            dam_sire_id = Convert.ToInt32(match_sire.Groups[1].ToString());
                            dam_sire_name = match_sire.Groups[2].ToString().Trim();
                            dam_sire_country = match_sire.Groups[3].ToString().Trim();
                        }

                        match_sire = match_sire.NextMatch();
                    }

                    var regex_dam = new Regex(@"dam_home\.sd\?horse_id=(\d+).*?DAM "">([^<(]+)(?:\(([A-Z]+)\)){0,1}</a>");
                    Match match_dam = regex_dam.Match(item);
                    if (match_dam.Success)
                    {
                        dam_id = Convert.ToInt32(match_dam.Groups[1].ToString());
                        dam_name = match_dam.Groups[2].ToString().Trim();
                        dam_country = match_dam.Groups[3].ToString().Trim();
                    }

                    if (sire_id != null && sire_id != 0)
                    {
                        Horse sire_rec = db_rph.Horses.FirstOrDefault(x => x.RPId == sire_id);
                        if (sire_rec == null)
                        {
                            sire_rec = new Horse();
                            db_rph.Horses.InsertOnSubmit(sire_rec);
                            sire_rec.RPId = (int)sire_id;
                            sire_rec.PedigreeProcessed = 1;
                            sire_rec.DetailProcessed = 0;
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

                        if (rec.SireId == null)
                        {
                            rec.SireId = sire_rec.Id;
                        }
                    }

                    if (dam_id != null && dam_id != 0)
                    {
                        Horse dam_rec = db_rph.Horses.FirstOrDefault(x => x.RPId == dam_id);
                        if (dam_rec == null)
                        {
                            dam_rec = new Horse();
                            db_rph.Horses.InsertOnSubmit(dam_rec);
                            dam_rec.RPId = (int)dam_id;
                            dam_rec.PedigreeProcessed = 1;
                            dam_rec.DetailProcessed = 0;
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

                        if (rec.DamId == null)
                        {
                            rec.DamId = dam_rec.Id;
                        }

                        if (dam_sire_id != null && dam_sire_id != 0)
                        {
                            Horse dam_sire_rec = db_rph.Horses.FirstOrDefault(x => x.RPId == dam_sire_id);
                            if (dam_sire_rec == null)
                            {
                                dam_sire_rec = new Horse();
                                db_rph.Horses.InsertOnSubmit(dam_sire_rec);
                                dam_sire_rec.RPId = (int)dam_sire_id;
                                dam_sire_rec.PedigreeProcessed = 1;
                                dam_sire_rec.DetailProcessed = 0;
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
                }

                match_main = match_main.NextMatch();
            }
        }
    }
}