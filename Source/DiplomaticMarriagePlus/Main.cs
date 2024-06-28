using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiplomaticMarriagePlus.Controller;
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
                //读取本模组的特殊版边缘城市的属性
                if (def.defName == "DMP_Rimcities_PACombinedDefense")
                {
                    Rimcities_PACombinedDefenseEventController.initialBaseChance = def.baseChance;
                }

                //加载边缘城市的任务
                else if (def.defName.Contains("Quest_City_"))
                {
                    IncidentsRimcities.Add(def);
                }
            }
            if(IncidentsRimcities.Count > 0)
            {
                Log.Message("[DMP] Rimcities quests loaded for DiplomaticMarriagePlus. Totally " + IncidentsRimcities.Count);
            }
        }
    }
}
