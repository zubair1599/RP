using System;

namespace RPDailyScrape
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                
                DateScraper.ScrapeDates(new DateTime(2017, 02,03)); //last date (2014, 11, 23),(2014, 11, 16)
      
                //RaceScraper.ReProcessRacesForGradeGroup();
            }
            catch (Exception e)
            {
                Logger.WriteLog(e.Message + "\n" + e.StackTrace);
            }
        }
    }
}