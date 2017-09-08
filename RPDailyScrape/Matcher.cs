using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace RPDailyScrape
{
    internal class Matcher
    {
        private static RacingPostRacesDataContext db_racing_update;
        private static RacingPostRacesDataContext db_racing_read;

        private static Dictionary<int, PPHorse> pp_horses;
        private static ILookup<string, int> pp_horse_lookup;
        private static ILookup<int, int> pp_sire_lookup;

        private static Dictionary<string, PQHorse> pq_horses;
        private static ILookup<string, string> pq_horse_lookup;
        private static ILookup<string, string> pq_sire_lookup;

        public static void Match_RP_PP()
        {
            db_racing_update =
                new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            db_racing_read = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());

            pp_horses = db_racing_read.PP_Horse_Selects.ToDictionary(x => x.Id, x => new PPHorse
            {
                HorseName = x.HorseName,
                Id = x.Id,
                SireId = x.SireId,
                SireName = x.SireName,
                DamName = x.DamName,
                Country = x.Country,
                FoalYear = x.FoalYear,
                OrigName = x.OrigName
            });
            pp_horse_lookup = pp_horses.Select(x => x.Value).ToLookup(x => x.HorseName, x => x.Id);
            pp_sire_lookup =
                pp_horses.Where(x => x.Value.SireId != null)
                    .Select(x => x.Value)
                    .ToLookup(x => (int) x.SireId, x => x.Id);

            while (db_racing_update.Horses.Any(x => x.PPMatchBasis == null))
            {
                foreach (Horse rp_horse in db_racing_update.Horses.Where(x => x.PPMatchBasis == null).Take(5000))
                {
                    Logger.WriteLog("Match to PP " + rp_horse.Name + " " + rp_horse.Id);
                    rp_horse.PPMatchBasis = "Failed RP_PP";

                    string horse_name = FlattenName(rp_horse.Name);
                    string sire_name = null;
                    string dam_name = null;
                    int? year = null;
                    if (rp_horse.FoalYear != null)
                    {
                        year = rp_horse.FoalYear;
                    }
                    string country = rp_horse.Country == null ? "" : rp_horse.Country;

                    Horse rp_sire = null;
                    if (rp_horse.SireId != null)
                    {
                        rp_sire = db_racing_read.Horses.Where(x => x.Id == rp_horse.SireId).FirstOrDefault();
                        if (rp_sire != null)
                        {
                            sire_name = FlattenName(rp_sire.Name);
                        }
                    }

                    Horse rp_dam = null;
                    if (rp_horse.DamId != null)
                    {
                        rp_dam = db_racing_read.Horses.Where(x => x.Id == rp_horse.DamId).FirstOrDefault();
                        if (rp_dam != null)
                        {
                            dam_name = FlattenName(rp_dam.Name);
                        }
                    }

                    int? ppid = null;
                    string match_basis = null;
                    if (MatchToPPByName(horse_name, sire_name, dam_name, year, country, ref ppid, ref match_basis))
                    {
                        rp_horse.PPId = (int) ppid;
                        rp_horse.PPMatchBasis = match_basis;
                    }

                    if (ppid == null && rp_sire != null)
                    {
                        int? sire_ppid = null;
                        if (rp_sire.PPId != null)
                        {
                            sire_ppid = rp_sire.PPId;
                        }

                        match_basis = "Sire";
                        if (MatchToPPBySire(sire_ppid, horse_name, sire_name, dam_name, year, country, ref ppid,
                            ref match_basis))
                        {
                            rp_horse.PPId = (int) ppid;
                            rp_horse.PPMatchBasis = match_basis;
                        }
                    }

                    // this option for dams for which we have minimal info
                    if (ppid == null && rp_sire != null)
                    {
                        int offspring_year_min = 9999;
                        int offspring_year_max = 0;
                        foreach (Horse offspring in db_racing_read.Horses.Where(x => x.DamId == rp_horse.Id))
                        {
                            if (offspring.FoalYear != null)
                            {
                                if (offspring.FoalYear < offspring_year_min)
                                {
                                    offspring_year_min = (int) offspring.FoalYear;
                                }

                                if (offspring.FoalYear > offspring_year_max)
                                {
                                    offspring_year_max = (int) offspring.FoalYear;
                                }
                            }
                        }

                        if (offspring_year_min != 9999)
                        {
                            if (MatchToPPDamSpecial(horse_name, sire_name, offspring_year_min, offspring_year_max,
                                ref ppid, ref match_basis))
                            {
                                rp_horse.PPId = (int) ppid;
                                rp_horse.PPMatchBasis = match_basis;
                            }
                        }
                    }

                    db_racing_update.SubmitChanges();
                }
                db_racing_update =
                    new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            }
        }

        public static void Add_RP_Merge()
        {
            db_racing_update =
                new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            db_racing_read = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());

            db_racing_read.CommandTimeout = 180;

            while (db_racing_read.RP_Match_By_Parents.Any())
            {
                List<RP_Match_By_Parent> view_recs = db_racing_read.RP_Match_By_Parents.Take(500).ToList();
                foreach (RP_Match_By_Parent view_rec in view_recs)
                {
                    Logger.WriteLog("Attempting RP Merge for " + view_rec.Name + " " + view_rec.Id);

                    Horse rp_horse = db_racing_update.Horses.Where(x => x.Id == view_rec.Id).FirstOrDefault();
                    if (rp_horse == null)
                    {
                        continue;
                    }

                    rp_horse.PPMatchBasis = "Failed";

                    if (rp_horse.FoalYear == null || view_rec.SireFoalYear == null || view_rec.DamFoalYear == null)
                    {
                        db_racing_update.SubmitChanges();
                        continue;
                    }

                    int sire_diff = (int) rp_horse.FoalYear - (int) view_rec.SireFoalYear;
                    if (sire_diff < 3 || sire_diff > 25)
                    {
                        db_racing_update.SubmitChanges();
                        continue;
                    }

                    int dam_diff = (int) rp_horse.FoalYear - (int) view_rec.DamFoalYear;
                    if (dam_diff < 3 || dam_diff > 25)
                    {
                        db_racing_update.SubmitChanges();
                        continue;
                    }

                    var merge_horse = new Horse_Merged();
                    db_racing_update.Horse_Mergeds.InsertOnSubmit(merge_horse);

                    merge_horse.RHId = rp_horse.Id;

                    merge_horse.Name = rp_horse.Name;
                    merge_horse.Country = rp_horse.Country;
                    merge_horse.FoalDate = rp_horse.FoalDate;
                    merge_horse.FoalYear = rp_horse.FoalYear;
                    merge_horse.Colour = rp_horse.Colour;

                    if (rp_horse.Sex != null)
                    {
                        switch (rp_horse.Sex)
                        {
                            case "f":
                            case "m":
                                merge_horse.Sex = "f";
                                break;
                            case "c":
                            case "h":
                                merge_horse.Sex = "c";
                                break;
                            case "g":
                                merge_horse.Sex = "g";
                                break;
                            case "r":
                                merge_horse.Sex = "r";
                                break;
                        }
                    }

                    merge_horse.SireId = view_rec.SireId;
                    merge_horse.DamId = view_rec.DamId;
                    merge_horse.Haplo =
                        db_racing_read.Horse_Mergeds.Where(x => x.Id == merge_horse.DamId)
                            .Select(x => x.Haplo)
                            .FirstOrDefault();
                    merge_horse.MergeBasis = "RP Added";

                    db_racing_update.SubmitChanges();

                    rp_horse.PPId = merge_horse.Id;
                    rp_horse.PPMatchBasis = "RP Added";

                    db_racing_update.SubmitChanges();
                }
                db_racing_update =
                    new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            }

            string cmd = "UPDATE Horse SET PPMatchBasis = 'Failed' WHERE PPMatchBasis = 'Failed RP_PQ_PP'";
            db_racing_update.ExecuteCommand(cmd);
        }

        public static void Match_RP_PQ()
        {
            db_racing_update =
                new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            db_racing_read = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());

            pq_horses = db_racing_read.PQ_Horse_Selects.ToDictionary(x => x.Id, x => new PQHorse
            {
                HorseName = x.FlatName,
                Id = x.Id,
                SireId = x.SireId,
                SireName = x.SireName,
                DamName = x.DamName,
                Country = x.Country,
                FoalYear = x.FoalYear
            });
            pq_horse_lookup = pq_horses.Select(x => x.Value).ToLookup(x => x.HorseName, x => x.Id);
            pq_sire_lookup = pq_horses.Where(x => x.Value.SireId != null)
                .Select(x => x.Value)
                .ToLookup(x => x.SireId, x => x.Id);

            while (db_racing_update.Horses.Where(x => x.PQMatchBasis == null).Any())
            {
                foreach (Horse rp_horse in db_racing_update.Horses.Where(x => x.PQMatchBasis == null).Take(500))
                {
                    rp_horse.PQMatchBasis = "Failed";

                    Logger.WriteLog("Match RP to PQ " + rp_horse.Name + " " + rp_horse.Id);

                    string horse_name = FlattenName(rp_horse.Name);
                    string sire_name = null;
                    string dam_name = null;
                    int? year = null;
                    if (rp_horse.FoalYear != null)
                    {
                        year = rp_horse.FoalYear;
                    }
                    string country = rp_horse.Country == null ? "" : rp_horse.Country;

                    Horse rp_sire = null;
                    if (rp_horse.SireId != null)
                    {
                        rp_sire = db_racing_update.Horses.Where(x => x.Id == rp_horse.SireId).FirstOrDefault();
                        if (rp_sire != null)
                        {
                            sire_name = FlattenName(rp_sire.Name);
                        }
                    }

                    Horse rp_dam = null;
                    if (rp_horse.DamId != null)
                    {
                        rp_dam = db_racing_update.Horses.Where(x => x.Id == rp_horse.DamId).FirstOrDefault();
                        if (rp_dam != null)
                        {
                            dam_name = FlattenName(rp_dam.Name);
                        }
                    }

                    string pqid = null;
                    string match_basis = null;
                    if (MatchToPQByName(horse_name, sire_name, dam_name, year, country, ref pqid, ref match_basis))
                    {
                        rp_horse.PQId = pqid;
                        rp_horse.PQMatchBasis = match_basis;
                    }

                    if (pqid == null && rp_sire != null)
                    {
                        string sire_pqid = null;
                        if (rp_sire.PQId != null)
                        {
                            sire_pqid = rp_sire.PQId;
                        }

                        match_basis = "Sire";
                        if (MatchToPQBySire(sire_pqid, horse_name, sire_name, dam_name, year, country, ref pqid,
                            ref match_basis))
                        {
                            rp_horse.PQId = pqid;
                            rp_horse.PQMatchBasis = match_basis;
                        }
                    }


                    // this option for dams for which we have minimal info
                    if (pqid == null && rp_sire != null)
                    {
                        int offspring_year_min = 9999;
                        int offspring_year_max = 0;
                        foreach (Horse offspring in db_racing_read.Horses.Where(x => x.DamId == rp_horse.Id))
                        {
                            if (offspring.FoalYear != null)
                            {
                                if (offspring.FoalYear < offspring_year_min)
                                {
                                    offspring_year_min = (int) offspring.FoalYear;
                                }

                                if (offspring.FoalYear > offspring_year_max)
                                {
                                    offspring_year_max = (int) offspring.FoalYear;
                                }
                            }
                        }

                        if (offspring_year_min != 9999)
                        {
                            if (MatchToPQDamSpecial(horse_name, sire_name, offspring_year_min, offspring_year_max,
                                ref pqid, ref match_basis))
                            {
                                rp_horse.PQId = pqid;
                                rp_horse.PQMatchBasis = match_basis;
                            }
                        }
                    }

                    db_racing_update.SubmitChanges();
                }
                db_racing_update =
                    new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            }
        }

        public static void Match_PQ_PP()
        {
            db_racing_update =
                new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            db_racing_read = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());

            pp_horses = db_racing_read.PP_Horse_Selects.ToDictionary(x => x.Id, x => new PPHorse
            {
                HorseName = x.HorseName,
                Id = x.Id,
                SireId = x.SireId,
                SireName = x.SireName,
                DamName = x.DamName,
                Country = x.Country,
                FoalYear = x.FoalYear,
                OrigName = x.OrigName
            });
            pp_horse_lookup = pp_horses.Select(x => x.Value).ToLookup(x => x.HorseName, x => x.Id);
            pp_sire_lookup =
                pp_horses.Where(x => x.Value.SireId != null)
                    .Select(x => x.Value)
                    .ToLookup(x => (int) x.SireId, x => x.Id);

            while (db_racing_update.PQ_Horses.Where(x => x.MergeMatchBasis == null).Any())
            {
                foreach (PQ_Horse pq_horse in db_racing_update.PQ_Horses.Where(x => x.MergeMatchBasis == null).Take(500)
                    )
                {
                    pq_horse.MergeMatchBasis = "Failed";

                    Logger.WriteLog("Matching PQ to PP " + pq_horse.Name + " " + pq_horse.Id);

                    string horse_name = FlattenName(pq_horse.Name);
                    string sire_name = null;
                    string dam_name = null;
                    int? year = null;
                    if (pq_horse.FoalYear != null)
                    {
                        year = pq_horse.FoalYear;
                    }
                    string country = pq_horse.Country == null ? "" : pq_horse.Country;

                    PQ_Horse pq_sire = null;
                    if (pq_horse.SireId != null)
                    {
                        pq_sire = db_racing_read.PQ_Horses.Where(x => x.Id == pq_horse.SireId).FirstOrDefault();
                        if (pq_sire != null)
                        {
                            sire_name = FlattenName(pq_sire.Name);
                        }
                    }

                    PQ_Horse pq_dam = null;
                    if (pq_horse.DamId != null)
                    {
                        pq_dam = db_racing_read.PQ_Horses.Where(x => x.Id == pq_horse.DamId).FirstOrDefault();
                        if (pq_dam != null)
                        {
                            dam_name = FlattenName(pq_dam.Name);
                        }
                    }

                    int? ppid = null;
                    string match_basis = null;
                    if (MatchToPPByName(horse_name, sire_name, dam_name, year, country, ref ppid, ref match_basis))
                    {
                        pq_horse.MergeId = (int) ppid;
                        pq_horse.MergeMatchBasis = match_basis;
                    }

                    if (ppid == null && pq_sire != null)
                    {
                        int? sire_merge_id = null;
                        if (pq_sire.MergeId != null)
                        {
                            sire_merge_id = pq_sire.MergeId;
                        }

                        match_basis = "Sire";
                        if (MatchToPPBySire(sire_merge_id, horse_name, sire_name, dam_name, year, country, ref ppid,
                            ref match_basis))
                        {
                            pq_horse.MergeId = (int) ppid;
                            pq_horse.MergeMatchBasis = match_basis;
                        }
                    }
                }

                db_racing_update.SubmitChanges();
            }
            db_racing_update =
                new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
        }

        public static void Merge_RP_PQ_PP()
        {
            db_racing_update =
                new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            db_racing_read = new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());

            while (db_racing_update.Horses.Where(x => x.PPMatchBasis == "Pending RP_PQ").Any())
            {
                foreach (
                    Horse rp_horse in
                        db_racing_update.Horses.Where(x => x.PPMatchBasis == "Pending RP_PQ")
                            .OrderBy(x => x.Id)
                            .ThenBy(x => x.Name)
                            .Take(500))
                {
                    rp_horse.PPId = null;
                    rp_horse.PPMatchBasis = "Failed RP_PQ_PP";

                    Logger.WriteLog("Merging RP/PQ to PP " + rp_horse.Name + " " + rp_horse.Id);

                    PQ_Horse pq_horse = db_racing_update.PQ_Horses.Where(x => x.Id == rp_horse.PQId).FirstOrDefault();
                    if (pq_horse != null)
                    {
                        if (pq_horse.MergeId != null)
                        {
                            Horse_Merged merge_horse =
                                db_racing_update.Horse_Mergeds.Where(x => x.PPId == pq_horse.MergeId).FirstOrDefault();
                            if (merge_horse != null)
                            {
                                merge_horse.RHId = rp_horse.Id;
                                merge_horse.Name = rp_horse.Name;
                                if (merge_horse.Country == null)
                                {
                                    merge_horse.Country = rp_horse.Country;
                                }
                                if (merge_horse.FoalDate == null)
                                {
                                    merge_horse.FoalDate = rp_horse.FoalDate;
                                }
                                if (merge_horse.FoalYear == null)
                                {
                                    merge_horse.FoalYear = rp_horse.FoalYear;
                                }
                                merge_horse.MergeBasis = "RP-PQ Matched";
                            }
                            rp_horse.PPId = pq_horse.MergeId;
                            rp_horse.PPMatchBasis = "PQ Direct";
                        }

                        else if (Merge_RP_PQ_Merge(pq_horse, 1, rp_horse.Id) != null)
                        {
                            rp_horse.PPId = pq_horse.MergeId;
                            rp_horse.PPMatchBasis = "PQ Added";
                        }
                    }

                    db_racing_update.SubmitChanges();
                }

                db_racing_update =
                    new RacingPostRacesDataContext(ConfigurationManager.ConnectionStrings["Racing"].ToString());
            }
        }

        private static int? Merge_RP_PQ_Merge(PQ_Horse pq_horse, int gen, int rh_id)
        {
            int? merge_sire_id = null;
            int? merge_dam_id = null;

            PQ_Horse pq_sire = db_racing_read.PQ_Horses.Where(x => x.Id == pq_horse.SireId).FirstOrDefault();
            if (pq_sire == null)
            {
                return null;
            }
            else
            {
                if (pq_sire.MergeId != null)
                {
                    merge_sire_id = pq_sire.MergeId;
                }
                else
                {
                    merge_sire_id = Merge_RP_PQ_Merge(pq_sire, gen + 1, 0);
                    if (merge_sire_id == null)
                    {
                        return null;
                    }
                }
            }

            PQ_Horse pq_dam = db_racing_read.PQ_Horses.Where(x => x.Id == pq_horse.DamId).FirstOrDefault();
            if (pq_dam == null)
            {
                return null;
            }
            else
            {
                if (pq_dam.MergeId != null)
                {
                    merge_dam_id = pq_dam.MergeId;
                }
                else
                {
                    merge_dam_id = Merge_RP_PQ_Merge(pq_dam, gen + 1, 0);
                    if (merge_dam_id == null)
                    {
                        return null;
                    }
                }
            }

            var merge_horse = new Horse_Merged();
            db_racing_update.Horse_Mergeds.InsertOnSubmit(merge_horse);

            if (rh_id != 0)
            {
                merge_horse.RHId = rh_id;
            }

            CultureInfo cult_info = Thread.CurrentThread.CurrentCulture;
            TextInfo text_info = cult_info.TextInfo;
            merge_horse.Name = text_info.ToTitleCase(pq_horse.Name.ToLower());
            merge_horse.Country = pq_horse.Country;
            if (pq_horse.FoalYear != null)
            {
                merge_horse.FoalDate = new DateTime((int) pq_horse.FoalYear, 1, 1);
                merge_horse.FoalYear = pq_horse.FoalYear;
            }
            merge_horse.Colour = pq_horse.Colour;
            merge_horse.Sex = pq_horse.Sex;
            merge_horse.SireId = merge_sire_id;
            merge_horse.DamId = merge_dam_id;
            merge_horse.Haplo =
                db_racing_read.Horse_Mergeds.Where(x => x.Id == merge_dam_id).Select(x => x.Haplo).FirstOrDefault();
            merge_horse.MergeBasis = "RP-PQ Added";

            db_racing_update.SubmitChanges();

            pq_horse.MergeId = merge_horse.Id;
            pq_horse.MergeMatchBasis = "Added";

            return merge_horse.Id;
        }

        private static bool MatchToPPByName(string horse_name, string sire_name, string dam_name, int? year,
            string country, ref int? ppid, ref string match_basis)
        {
            foreach (int horse_match_id in pp_horse_lookup[horse_name])
            {
                PPHorse horse_match = pp_horses[horse_match_id];

                bool sires_available = sire_name != null && horse_match.SireName != null;
                bool dams_available = dam_name != null && horse_match.DamName != null;
                bool years_available = year != null && horse_match.FoalYear != null;

                double ld_sire = LD.FuzzyMatch(sire_name, horse_match.SireName);
                double ld_dam = LD.FuzzyMatch(dam_name, horse_match.DamName);

                bool sire_match = ld_sire >= 0.8;
                bool dam_match = ld_dam >= 0.8;
                bool year_match = year != null && horse_match.FoalYear != null && year == horse_match.FoalYear;
                bool year_approx = year != null && horse_match.FoalYear != null &&
                                   Math.Abs((int) year - (int) horse_match.FoalYear) <= 3;
                bool country_match = country != null && horse_match.Country != null && country == horse_match.Country;

                match_basis = null;
                if (sires_available && dams_available && years_available && sire_match && dam_match && year_approx)
                {
                    match_basis = "Name, Sire Name, Dam Name, Year";
                }
                else if (!sires_available && dams_available && years_available && dam_match && year_match)
                {
                    match_basis = "Name, Dam Name, Year";
                }
                else if (sires_available && !dams_available && years_available && sire_match && year_match)
                {
                    match_basis = "Name, Sire Name, Year";
                }
                else if (sires_available && dams_available && !years_available && sire_match && dam_match)
                {
                    match_basis = "Name, Sire Name, Dam Name";
                }

                if (match_basis != null)
                {
                    ppid = horse_match.Id;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchToPPDamSpecial(string horse_name, string sire_name, int offspring_year_min,
            int offspring_year_max, ref int? ppid, ref string match_basis)
        {
            foreach (int horse_match_id in pp_horse_lookup[horse_name])
            {
                PPHorse horse_match = pp_horses[horse_match_id];

                if (horse_match.FoalYear == null)
                {
                    continue;
                }

                bool sires_available = sire_name != null && horse_match.SireName != null;
                double ld_sire = LD.FuzzyMatch(sire_name, horse_match.SireName);
                bool sire_match = ld_sire >= 0.8;

                match_basis = null;
                if (sires_available && sire_match && horse_match.FoalYear <= offspring_year_min - 3 &&
                    horse_match.FoalYear >= offspring_year_max - 28)
                {
                    match_basis = "Dam Special";
                }

                if (match_basis != null)
                {
                    ppid = horse_match.Id;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchToPPBySire(int? sire_ppid, string horse_name, string sire_name, string dam_name,
            int? horse_year, string horse_country, ref int? ppid, ref string match_basis)
        {
            if (dam_name == null || horse_year == null)
            {
                return false;
            }

            List<PPHorse> p_sires = null;
            if (sire_ppid != null && pp_horses.ContainsKey((int) sire_ppid))
            {
                p_sires = new List<PPHorse> {pp_horses[(int) sire_ppid]};
            }
            else
            {
                p_sires = new List<PPHorse>();
                foreach (int p_sire_id in pp_horse_lookup[sire_name])
                {
                    p_sires.Add(pp_horses[p_sire_id]);
                }
            }

            foreach (PPHorse p_sire in p_sires)
            {
                IEnumerable<int> prog_ids = pp_sire_lookup[p_sire.Id];
                foreach (int prog_id in prog_ids)
                {
                    PPHorse prog = pp_horses[prog_id];
                    double ld = LD.FuzzyMatch(horse_name, prog.HorseName);

                    bool temp_pp_name = false;
                    if (prog.OrigName.Contains("/"))
                    {
                        temp_pp_name = true;
                    }

                    var regex = new Regex("'[0-9]{4}$");
                    Match match = regex.Match(prog.OrigName);
                    if (match.Success)
                    {
                        temp_pp_name = true;
                    }

                    if (ld < 0.8 && !temp_pp_name)
                    {
                        continue;
                    }

                    if (prog.DamName != null && prog.FoalYear != null)
                    {
                        double ld_dam = LD.FuzzyMatch(dam_name, prog.DamName);
                        if (ld_dam >= 0.8 && horse_year == prog.FoalYear)
                        {
                            ppid = prog.Id;
                            match_basis = match_basis + ", Name, Dam Name, Year";
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool MatchToPQByName(string horse_name, string sire_name, string dam_name, int? year,
            string country, ref string pqid, ref string match_basis)
        {
            foreach (string horse_match_id in pq_horse_lookup[horse_name])
            {
                PQHorse horse_match = pq_horses[horse_match_id];

                bool sires_available = sire_name != null && horse_match.SireName != null;
                bool dams_available = dam_name != null && horse_match.DamName != null;
                bool years_available = year != null && horse_match.FoalYear != null;

                double ld_sire = LD.FuzzyMatch(sire_name, horse_match.SireName);
                double ld_dam = LD.FuzzyMatch(dam_name, horse_match.DamName);

                bool sire_match = ld_sire >= 0.8;
                bool dam_match = ld_dam >= 0.8;
                bool year_match = year != null && horse_match.FoalYear != null && year == horse_match.FoalYear;
                bool year_approx = year != null && horse_match.FoalYear != null &&
                                   Math.Abs((int) year - (int) horse_match.FoalYear) <= 3;
                bool country_match = country != null && horse_match.Country != null && country == horse_match.Country;

                match_basis = null;
                if (sires_available && dams_available && years_available && sire_match && dam_match && year_approx)
                {
                    match_basis = "Name, Sire Name, Dam Name, Year";
                }
                else if (!sires_available && dams_available && years_available && dam_match && year_match)
                {
                    match_basis = "Name, Dam Name, Year";
                }
                else if (sires_available && !dams_available && years_available && sire_match && year_match)
                {
                    match_basis = "Name, Sire Name, Year";
                }
                else if (sires_available && dams_available && !years_available && sire_match && dam_match)
                {
                    match_basis = "Name, Sire Name, Dam Name";
                }

                if (match_basis != null)
                {
                    pqid = horse_match.Id;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchToPQDamSpecial(string horse_name, string sire_name, int offspring_year_min,
            int offspring_year_max, ref string pqid, ref string match_basis)
        {
            foreach (string horse_match_id in pq_horse_lookup[horse_name])
            {
                PQHorse horse_match = pq_horses[horse_match_id];

                if (horse_match.FoalYear == null)
                {
                    continue;
                }

                bool sires_available = sire_name != null && horse_match.SireName != null;
                double ld_sire = LD.FuzzyMatch(sire_name, horse_match.SireName);
                bool sire_match = ld_sire >= 0.8;

                match_basis = null;
                if (sires_available && sire_match && horse_match.FoalYear <= offspring_year_min - 3 &&
                    horse_match.FoalYear >= offspring_year_max - 28)
                {
                    match_basis = "Dam Special";
                }

                if (match_basis != null)
                {
                    pqid = horse_match.Id;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchToPQBySire(string sire_pqid, string horse_name, string sire_name, string dam_name,
            int? horse_year, string horse_country, ref string pqid, ref string match_basis)
        {
            if (dam_name == null || horse_year == null)
            {
                return false;
            }

            List<PQHorse> p_sires = null;
            if (sire_pqid != null)
            {
                p_sires = new List<PQHorse> {pq_horses[sire_pqid]};
            }
            else
            {
                p_sires = new List<PQHorse>();
                foreach (string p_sire_id in pq_horse_lookup[sire_name])
                {
                    p_sires.Add(pq_horses[p_sire_id]);
                }
            }

            foreach (PQHorse p_sire in p_sires)
            {
                IEnumerable<string> prog_ids = pq_sire_lookup[p_sire.Id];
                foreach (string prog_id in prog_ids)
                {
                    PQHorse prog = pq_horses[prog_id];
                    double ld = LD.FuzzyMatch(horse_name, prog.HorseName);

                    if (prog.DamName != null && prog.FoalYear != null)
                    {
                        double ld_dam = LD.FuzzyMatch(dam_name, prog.DamName);
                        if (ld_dam >= 0.8 && horse_year == prog.FoalYear)
                        {
                            pqid = prog.Id;
                            match_basis = match_basis + ", Name, Dam Name, Year";
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static string FlattenName(string str)
        {
            str = str.ToUpper();

            var regex = new Regex(@"(.*?)\s+[IVX]+$");
            Match match = regex.Match(str);
            if (match.Success)
            {
                str = match.Groups[1].ToString();
            }

            regex = new Regex(@"[^A-Z0-9]");
            str = regex.Replace(str, "");

            return str;
        }
    }

    internal class LD
    {
        public static double FuzzyMatch(string s1, string s2)
        {
            if (s1 == null || s2 == null)
            {
                return 0;
            }

            return 1 - (double) Compute(s1, s2)/(s1.Length > s2.Length ? s1.Length : s2.Length);
        }

        private static int Compute(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            var d = new int[n + 1, m + 1]; // matrix
            int cost;

            // Step 1
            if (n == 0) return m;
            if (m == 0) return n;

            // Step 2
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            // Step 3
            for (int i = 1; i <= n; i++)
            {
                //Step 4
                for (int j = 1; j <= m; j++)
                {
                    // Step 5
                    cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    // Step 6
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            // Step 7
            return d[n, m];
        }
    }

    public class PQHorse
    {
        public string HorseName { get; set; }
        public string Id { get; set; }
        public string SireId { get; set; }
        public string SireName { get; set; }
        public string DamName { get; set; }
        public string Country { get; set; }
        public int? FoalYear { get; set; }
    }

    public class PPHorse
    {
        public string HorseName { get; set; }
        public int Id { get; set; }
        public int? SireId { get; set; }
        public string SireName { get; set; }
        public string DamName { get; set; }
        public string Country { get; set; }
        public int? FoalYear { get; set; }
        public string OrigName { get; set; }
    }
}