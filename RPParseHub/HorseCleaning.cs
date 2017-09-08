using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPParseHub
{
    public static class HorseCleaning
    {
        public static Horse PrcessHorse(Horse horse)
        {
            
            HtmlDocument document = new HtmlDocument();

            if (String.IsNullOrEmpty(horse.FullHtml)) return null;

            document.LoadHtml(horse.FullHtml);
            HtmlNodeCollection collection = document.DocumentNode.SelectNodes("//div");
            int index = 0;
            foreach (var item in collection)
            {
                if (index == 0)
                { index++; continue; }

                if (item.ChildNodes.Any(c => c.Name.Equals("dt")))
                {
                    if ((item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dt")).InnerHtml.Contains("yo")))
                    {
                        horse.Profile = item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dd")).InnerHtml.Replace("\n", "").Replace("(", "").Replace(")", "").Trim();
                    }
                    if ((item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dt")).InnerHtml.Contains("Breeder")))
                    {
                        horse.Breeder = item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dd")).InnerHtml.Replace("\n", "").Trim();
                    }
                    if ((item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dt")).InnerHtml.Contains("Trainer")))
                    {
                        // horse.Trainer = item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dd")).ChildNodes[1].InnerHtml.Replace("\n", "").Trim();
                    }
                    if ((item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dt")).InnerHtml.Contains("Sire")) && item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dt")).InnerHtml.Contains("Sire Comments"))
                    {
                        horse.Sire_url = item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dd")).ChildNodes[1].Attributes["href"].Value;
                    }
                    if ((item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dt")).InnerHtml.Contains("Dam")))
                    {
                        horse.Dam_url = item.ChildNodes.FirstOrDefault(c => c.Name.Equals("dd")).ChildNodes[1].Attributes["href"].Value;
                    }

                }





            }
            return horse;
        }
        public static void PrcessHeader(Horse horse)
        {
    
            HtmlDocument document = new HtmlDocument();

            if (String.IsNullOrEmpty(horse.Header)) return;

            document.LoadHtml(horse.Header);
            HtmlNodeCollection collection = document.DocumentNode.SelectNodes("//div");
            foreach (var item in collection)
            {
                if (item.Attributes.Any(s => s.Name.Equals("data-tab-data-url")))
                {
                    horse.Url = item.Attributes.First(s => s.Name.Equals("data-tab-data-url")).Value;
                    var subItem = item.SelectNodes("//span").Where(s=>s.Attributes[0].Value.Contains("horseDropDownCountryCode"));
                    if (subItem!=null)
                    {
                        horse.Country = subItem.FirstOrDefault().InnerText.Replace("\n", "").Replace("(", "").Replace(")", "").Trim();
                    }
                }

            }
        }
    }
}
