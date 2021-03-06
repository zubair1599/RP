//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace RPParseHub
{
    using System;
    using System.Collections.Generic;
    
    public partial class HorseRacingAnalysi
    {
        public int RaceID { get; set; }
        public Nullable<int> MeetingId { get; set; }
        public Nullable<System.DateTime> StartTime { get; set; }
        public string RaceName { get; set; }
        public Nullable<int> RaceType { get; set; }
        public Nullable<bool> Handicap { get; set; }
        public Nullable<bool> Chase { get; set; }
        public Nullable<bool> Hurdle { get; set; }
        public Nullable<bool> NHFlat { get; set; }
        public Nullable<int> Class { get; set; }
        public string GradeGroup { get; set; }
        public string RaceRating { get; set; }
        public string Eligibility { get; set; }
        public string TrackDirection { get; set; }
        public Nullable<int> DistanceYards { get; set; }
        public string Distance { get; set; }
        public string DistanceStd { get; set; }
        public decimal Furlongs { get; set; }
        public string Going { get; set; }
        public string FencesHurdles { get; set; }
        public Nullable<int> Fences { get; set; }
        public Nullable<int> FencesOmitted { get; set; }
        public Nullable<int> Runners { get; set; }
        public string RaceTime { get; set; }
        public Nullable<double> TimeSeconds { get; set; }
        public Nullable<int> CourseID { get; set; }
        public int RunnerID { get; set; }
        public Nullable<int> HorseId { get; set; }
        public Nullable<int> Position { get; set; }
        public Nullable<int> Placed { get; set; }
        public string DidNotFinish { get; set; }
        public Nullable<bool> Disqualified { get; set; }
        public Nullable<int> Draw { get; set; }
        public string RunnerDistance { get; set; }
        public string Price { get; set; }
        public Nullable<bool> PriceProcessed { get; set; }
        public Nullable<int> Numerator { get; set; }
        public Nullable<int> Denominator { get; set; }
        public Nullable<bool> Favourite { get; set; }
        public Nullable<int> WeightProcessed { get; set; }
        public Nullable<int> Weight { get; set; }
        public Nullable<int> Weight_OW { get; set; }
        public Nullable<int> Weight_OH { get; set; }
        public Nullable<int> Weight_EW { get; set; }
        public string Weight_Desc { get; set; }
        public Nullable<int> TrainerId { get; set; }
        public Nullable<int> JockeyId { get; set; }
        public Nullable<int> RunnerAllowance { get; set; }
        public string OR_Rating { get; set; }
        public string TS_Rating { get; set; }
        public string RPR_Rating { get; set; }
        public Nullable<int> rating { get; set; }
        public Nullable<int> Age { get; set; }
        public Nullable<int> HorseRaceSequence { get; set; }
        public string HorseName { get; set; }
        public string FlatName { get; set; }
        public string Country { get; set; }
        public Nullable<System.DateTime> FoalDate { get; set; }
        public Nullable<int> FoalYear { get; set; }
        public string Colour { get; set; }
        public string Sex { get; set; }
        public Nullable<int> GoingID { get; set; }
        public Nullable<int> GoingGroupID { get; set; }
        public Nullable<int> NextGoingGroupID { get; set; }
        public Nullable<int> PrevGoingGroupID { get; set; }
        public int RatingOfWinner { get; set; }
        public int RatingOfLastRun { get; set; }
        public int RatingOfLastWin { get; set; }
        public int ClassPrevious { get; set; }
        public int ClassNext { get; set; }
        public decimal distancePrevious { get; set; }
        public decimal distanceNext { get; set; }
        public int Maiden { get; set; }
        public int Winner_On_Same_Type_Track { get; set; }
        public int Winner_On_Track { get; set; }
        public int Winner_Same_Distance { get; set; }
        public int Winner_Same_Going { get; set; }
        public int Winner_Same_Class { get; set; }
        public Nullable<int> Topweight { get; set; }
        public Nullable<decimal> C2_5__of_top_weight { get; set; }
        public string within_2_5__of_top_weight { get; set; }
        public Nullable<decimal> Within_5__top_weight { get; set; }
        public Nullable<decimal> Within_5__of_bottom_Weight { get; set; }
        public Nullable<decimal> within_2_5__bottom_Weight { get; set; }
        public Nullable<int> Bottom_Weight { get; set; }
        public Nullable<decimal> PriceDecimal { get; set; }
        public Nullable<int> TrackTypeID { get; set; }
        public int Winner_AnyTime { get; set; }
        public Nullable<System.DateTime> DateOfLastRace { get; set; }
        public Nullable<System.DateTime> DateOfLastWinRace { get; set; }
        public Nullable<int> DateOfLastRaceDateDifference { get; set; }
        public Nullable<int> DateOfLastWinRaceDateDifference { get; set; }
        public int WinnerLastRace { get; set; }
        public int WinnerInPrevious3Races { get; set; }
        public int WinnerInPrevious5Races { get; set; }
        public int Winner_Previous_Distance { get; set; }
        public int Winner_Next_Distance { get; set; }
        public int Winner_Previous_Going { get; set; }
        public int Winner_Next_Going { get; set; }
        public int Winner_Previous_Class { get; set; }
        public int Winner_Next_Class { get; set; }
        public decimal OddsFavoriteValue { get; set; }
        public string AnyRacewith4orlessrunners_WinneronlyPlaced { get; set; }
        public string AnyRacewith5_6or7runners_2placesPlaced { get; set; }
        public string AnyNonHandiCapWith8orMoreRunners_3PlacesPlaced { get; set; }
        public string HandicapsWith8_15Runners_3PlacesPlaced { get; set; }
        public string HandicapsWith16OrMoreRunners_4PlacesPlaced { get; set; }
        public Nullable<int> PlacedInRace { get; set; }
        public int IsTopWeight { get; set; }
        public int IsBottomweight { get; set; }
        public int Count_2_5__of_top_weight { get; set; }
        public int Count_5__of_top_weight { get; set; }
        public int Count_2_5__of_Bottom_weight { get; set; }
        public int Count_5__of_Bottom_weight { get; set; }
        public int Count_Other___Weight { get; set; }
        public int Count_ImprovedRatingSinceLastRun { get; set; }
        public int Count_LowerRatingSinceLastRun { get; set; }
        public int Count_ImprovedRatingSinceLastWin { get; set; }
        public int Count_LowerRatingSinceLastWin { get; set; }
        public int Favorite { get; set; }
        public decimal SecondFavoriteValue2 { get; set; }
        public int Odd_Is_2nd_Favorite { get; set; }
        public int Odds_On { get; set; }
        public int Evens___2_1 { get; set; }
        public int C_2_1___5_1 { get; set; }
        public int C_5_1___10_1 { get; set; }
        public int C_10_1___20_1 { get; set; }
        public int C_20___1 { get; set; }
        public int PlacedInPreviousRace { get; set; }
        public int PlacedInPrevious3Races { get; set; }
        public int PlacedInPrevious5Races { get; set; }
        public int UnPlacedInAllRaces { get; set; }
        public Nullable<int> NoRunsInCareer { get; set; }
        public int NoRunsInSeason { get; set; }
        public Nullable<int> PP { get; set; }
        public Nullable<int> PIR { get; set; }
        public int Winner_distance____1_OR { get; set; }
        public int Winner_Going____1_And { get; set; }
        public int Winner_Class____1_And { get; set; }
        public Nullable<int> HorseAge { get; set; }
        public int Within_Last_7_Days { get; set; }
        public int C8_14_Days { get; set; }
        public int C15_Days___28_Days { get; set; }
        public int C1Month__3months { get; set; }
        public int C3_Months { get; set; }
        public int Win_Within_Last_7_Days { get; set; }
        public int Win_8_14_Days { get; set; }
        public int Win_15_Days___28_Days { get; set; }
        public int Win_1Month__3months { get; set; }
        public int Win_3_Months { get; set; }
        public int WinnerLastRaceaANDWinnerSameGoing { get; set; }
        public int WinnerLastRaceaANDWinnerSameDistance { get; set; }
        public int WinnerLastRaceaANDWinnerSameClass { get; set; }
        public int WinLastRaceANDWinSameGoingAndWinSameDistance { get; set; }
        public int WinLastRaceAndSameGoingAndSameDistanceAndSameClass { get; set; }
        public int WinLastRaceaANDImprovedRatingSinceLastRun { get; set; }
        public int WinLastRaceANDImprovedRatingSinceLastRunAndSameTypeTrack { get; set; }
        public int WinLastRaceANDImprovedRatingSinceLastRunAndWinnerSameGoing { get; set; }
        public int WinLastRaceANDTopweight { get; set; }
        public int Win8_14daysAndWinnerOnTrack { get; set; }
        public int WinLastRaceANDFavorite { get; set; }
        public int WinnerSeason_Hurdle { get; set; }
        public int WinnerSeason_Chase { get; set; }
        public int WinnerSeason_AllWeatherFlat { get; set; }
        public int WinnerSeason_Flat { get; set; }
        public int WinnerSeason_Jump { get; set; }
        public int WinnerSeason_NationalHuntFlat { get; set; }
        public int WinnerSeason_HuntersChase { get; set; }
        public Nullable<decimal> newPrice { get; set; }
    }
}
