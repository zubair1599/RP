using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPParseHub
{
    public class TrainerLast14Days
    {
        public int runs { get; set; }
        public int wins { get; set; }
        public object percent { get; set; }
    }

    public class Profile
    {
        public int horseUid { get; set; }
        public string horseName { get; set; }
        public string horseCountryOriginCode { get; set; }
        public string horseDateOfBirth { get; set; }
        public object horseDateOfDeath { get; set; }
        public string age { get; set; }
        public object dateGelded { get; set; }
        public int? sireUid { get; set; }
        public int? damUid { get; set; }
        public string sireHorseName { get; set; }
        public string sireCountryOriginCode { get; set; }
        public string damHorseName { get; set; }
        public string damCountryOriginCode { get; set; }
        public string damSireHorseName { get; set; }
        public string damSireCountryOriginCode { get; set; }
        public string ownerName { get; set; }
        public int? ownerUid { get; set; }
        public string ownerPtpTypeCode { get; set; }
        public string trainerName { get; set; }
        public int? trainerUid { get; set; }
        public string trainerLocation { get; set; }
        public object trainerPtpTypeCode { get; set; }
        public string breederName { get; set; }
        public string horseColourCode { get; set; }
        public string horseSexCode { get; set; }
        public string silkImagePath { get; set; }
        public object tips { get; set; }
        public object comments { get; set; }
        public TrainerLast14Days trainerLast14Days { get; set; }
        public object previousTrainers { get; set; }
        public object previousOwners { get; set; }
        public int? damSireUid { get; set; }
        public bool damStatus { get; set; }
        public bool sireStatus { get; set; }
        public string horseSex { get; set; }
        public string horseColour { get; set; }
        public object sireComment { get; set; }
        public object avgFlatWinDist { get; set; }
        public double? sireAvgFlatWinDist { get; set; }
        public double? damSireAvgFlatWinDist { get; set; }
        public object avgWinDistance { get; set; }
        public double? sireAvgWinDistance { get; set; }
        public double? damSireAvgWinDistance { get; set; }
        public object avgEarningsIndex { get; set; }
        public object studFee { get; set; }
        public object weatherbysUid { get; set; }
        public object toFollow { get; set; }
    }

  

    public class ExternalLinks
    {
        public string assetsURL { get; set; }
        public string imagesUrl { get; set; }
        public string helpCenterUrl { get; set; }
        public string weatherbysDomain { get; set; }
    }

    public class RootObject
    {
        public Profile profile { get; set; }
       // public object entries { get; set; }
       // public object quotes { get; set; }
      //  public List<Season> seasons { get; set; }
       // public ExternalLinks externalLinks { get; set; }
    }
}
