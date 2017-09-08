using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RPParseHub
{

    public class Footer
    {
        public string NoOfRunners { get; set; }
        public string WinTime { get; set; }
        public string TotalSP { get; set; }
        public string NonRunners { get; set; }
        public string FirstOwner { get; set; }
        public string FirstOwner_url { get; set; }
        public string SecondOwner { get; set; }
        public string SecondOwner_url { get; set; }
        public string ThirdOwner { get; set; }
        public string ThirdOwner_url { get; set; }
        public string FirseBreeder { get; set; }
    }

    public class Horse
    {
        private string _sireUrl;
        private string _damUrl;
        private string _url;
        private string _profile;
        private string[] _details;
        private string _country;
        private DateTime _date;
        public int Id { get; set; }
        public string Name { get; set; }
        public string Country {
            get
            {
                return _country;
            }
            set
            {
                _country = value;
                if(_country!=null)
                {
                    _country = _country.Replace("(", "").Replace(")", "");
                }
            }
        }
        public string Age { get; set; }
        
        public string Profile
        {
            get
            {
                return _profile;
            }
            set
            {
                _profile = value;
                _profile = _profile.Replace("(", "").Replace(")", "");
                _details = _profile.Split(' ');
                if(_details!=null && _details.Count() > 0 && _details.Count() >= 3)
                {
                    DateTime.TryParse(_details[0], out _date);
                    this.DOB = _date;
                    this.Color = _details[1];
                    this.Sex = _details[2];
                }
                else if (_details != null && _details.Count() == 2)
                {
                    DateTime.TryParse(_details[0], out _date);
                    this.DOB = _date;
                    //this.Color = _details[1];
                    this.Sex = _details[1];
                }
            }
        }
        public DateTime DOB { get; set; }
        public string Color { get; set; }
        public string Sex { get; set; }
        public int SireId { get; set; }
        public string Sire { get; set; }
        public string Sire_url {
            get
            {
                return _sireUrl;
            }
            set
            {
                _sireUrl = value;
                if (_sireUrl.StartsWith("/profile"))
                {
                    _sireUrl = "https://www.racingpost.com" + _sireUrl;
                }           
                SireId = Helper.GetIdfromUrl(_sireUrl, "https://www.racingpost.com/profile/horse/");
            }
        }

        public int DamId;
        public string Dam { get; set; }

        public string Dam_url
        {
            get
            {
                return _damUrl;
            }
            set
            {
                _damUrl = value;
                if (_damUrl.StartsWith("/profile"))
                {
                    _damUrl = "https://www.racingpost.com" + _damUrl;
                }
                DamId = Helper.GetIdfromUrl(_damUrl, "https://www.racingpost.com/profile/horse/");
            }
        }

        public string DamSire { get; set; }
        public string DamSire_url { get; set; }
        public string Breeder { get; set; }
        public string FullHtml { get; set; }
        public string Header { get; set; }
        public string Url
        {
            get
            {
                return _url;
            }
            set
            {
                _url = value;
                this.Id = Helper.GetIdfromUrl(_url, "/profile/horse/tabs/");
            }
        }
    }
    public class RPHorses
    {
       public  List<Horse> Horses { get; set; }
    }
    public class Runner
    {
        private List<Horse> _subjectHorse;
        private string _trainerUrl;
        private string _jockeyUrl;
        private string _draw;
        private string subjectHorse_url;

        public int RaceId { get; set; }
        public int HorseId { get; set; }
        public string PosTemp { get; set; }
        public string Draw
        {
            get
            {
                return _draw;
            }
            set
            {
                _draw = value;
                _draw = _draw.Replace("(", "").Replace(")", "");
            }
        }
        public string DistBeaten { get; set; }
        public string Distance { get; set; }
        public int OwnerId { get; set; }
        public int JockeyId { get; set; }
        public string Jockey { get; set; }
        public string Jockey_url
        {
            get { return _jockeyUrl; }
            set
            {
                _jockeyUrl = value;
                JockeyId = Helper.GetIdfromUrl(_jockeyUrl, "https://www.racingpost.com/profile/jockey/");
            }
        }
        public int TrainerId {get;set;}
        public string Trainer_url
        {
            get { return _trainerUrl; }
            set
            {
                _trainerUrl = value;
                TrainerId = Helper.GetIdfromUrl(_trainerUrl, "https://www.racingpost.com/profile/trainer/");
            }
        }
        public string Trainer { get; set; }
        public string SP { get; set; }
        public string Age { get; set; }
        public string Weight { get; set; }
        public string WeightRaw { get; set; }
       
        public string WtRaw { get; set; }
        public string SubjectHorse_url {
            get { return subjectHorse_url; }
            set
            {
                subjectHorse_url = value;
                this.HorseId = Helper.GetIdfromUrl(subjectHorse_url, "https://www.racingpost.com/profile/horse/");
            }
        }
        public int SubjectHorse_Id { get; set; }

        //public List<Horse> SubjectHorse
        //{
        //    get
        //    {
        //        return _subjectHorse;
        //    }
        //    set
        //    {
        //        _subjectHorse = value;
        //        Horse = _subjectHorse[0];
        //        HorseId = Horse.Id;
        //    }
        //}
        public Horse Horse { get; set; }
    }

    public class Race
    {
        private string _raceUrl;
        private string _courseUrl;
        private string _prizeMoney;

        public int Id { get; set; }
     
        public string Name { get; set; }
        public string url
        {
            get { return _raceUrl; }
            set
            {
                _raceUrl = value;
                if (_raceUrl != null)
                    this.Id = Convert.ToInt32(this.url.Split('/').LastOrDefault());
            }
        }

        public string Course_url
        {
            get { return _courseUrl; }
            set
            {
                _courseUrl = value;
                CourseId= Helper.GetIdfromUrl(_courseUrl, "https://www.racingpost.com/profile/course/");
            }
        }
        public string RaceName { get; set; }
        public string Course { get; set; }
        public int CourseId { get; set; }
        public string Country { get; set; }
        public string ClassRaw { get; set; }
        public int? Class { get; set; }
        public string Rating { get; set; }
        public string Eligibility { get; set; }
        public string FencesHurdles { get; set; }
        public bool? Chase { get; set; }
        public bool? Handicap { get; set; }
        public int? RaceType { get; set; }
        public string Date { get; set; }
        public string GradeGroup { get; set; }
        public DateTime StartTime { get; set; }
        public string Time { get; set; }
        public string RaceClass { get; set; }
        public string CurrencyUnit { get; set; }
        public string PrizeMoney
        {
            get { return _prizeMoney; }
            set {
                _prizeMoney = value;
                var temp = _prizeMoney.Split(' ');
                Prizes = new List<string>();
                for (int i = 0;i< temp.Count(); i++)
                {
                    if (i % 2 != 0)
                        Prizes.Add(temp[i]);
                }
                CurrencyUnit = Prizes.First().Substring(0, 1);
            }
        }
        public List<string> Prizes { get; set; }
        public decimal? Prize1st { get; set; }
        public decimal? Prize2nd { get; set; }
        public decimal? Prize3rd { get; set; }
        public decimal? Prize4th { get; set; }
        public decimal? Prize5th { get; set; }
        public decimal? Prize6th { get; set; }
        public string RaceDisctance { get; set; }
        public string Distance { get; set; }
        public string DistanceStd { get; set; }
        public string Going { get; set; }
        public int? TrackTypeId { get; set; }
        public int? DistanceYards { get; set; }
        public string Yo { get; set; }
        public int? FencesOmitted { get; set; }
        public int? Fences { get; set; }
        public List<Footer> Footer { get; set; }
        public string Notes { get; set; }
        public double TimeSeconds { get; set; }
        public List<Runner> Runners { get; set; }
        public string NoOfRunners { get; set; }
        public string WinTime { get; set; }
        public string TotalSP { get; set; }
        public string NonRunners { get; set; }
        public string FirstOwner { get; set; }
        public string FirstOwner_url { get; set; }
        public string SecondOwner { get; set; }
        public string SecondOwner_url { get; set; }
        public string ThirdOwner { get; set; }
        public string ThirdOwner_url { get; set; }
        public string FirseBreeder { get; set; }
    }
    public class RPData
    {
        public List<Race> AllRaces { get; set; }
    }
    public static class Helper
    {
        public static int GetIdfromUrl(string url, string repVal)
        {
            if (url != null)
            {
                string temp = url.Replace(repVal, "");

                return Convert.ToInt32(temp.Substring(0, temp.IndexOf("/")));
            }
            return 0;
        }
    }
    public class NonRunner
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Country { get; set; }
    }
    public class HtmlContent
    {
        public System.Windows.Forms.HtmlDocument GetHtmlAjax(Uri uri, int AjaxTimeLoadTimeOut)
        {

            Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
            using (WebBrowser wb = new WebBrowser())
            {
                wb.Navigate(uri);
                while (wb.ReadyState != WebBrowserReadyState.Complete)
                    Application.DoEvents();
                Thread.Sleep(AjaxTimeLoadTimeOut);
                Application.DoEvents();
                return wb.Document;
            }
        }
    }
}
