using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace DiplomaticMarriagePlus
{
    [StaticConstructorOnStartup]
    public static class Main
    {
        public static List<IncidentDef> IncidentsRimcities;

        static Main() 
        {
            Log.Message("[DMP] DiplomaticMarriagePlus loaded");
            UpdateRimcitiesQuests();
        }

        public static void UpdateRimcitiesQuests()
        {
            IncidentsRimcities = new List<IncidentDef>();
            foreach (var def in DefDatabase<IncidentDef>.AllDefsListForReading.OrderBy(def => def.label).ToList())
            {
                if (!def.defName.Contains("Quest_City_"))
                {
                    continue;
                }
                IncidentsRimcities.Add(def);
            }
            Log.Message("[DMP] Rimcities quests loaded for DiplomaticMarriagePlus. Totally " + IncidentsRimcities.Count);
        }
    }
}
