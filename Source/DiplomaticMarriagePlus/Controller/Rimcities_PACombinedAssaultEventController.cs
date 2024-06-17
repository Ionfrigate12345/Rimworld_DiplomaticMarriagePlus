using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DiplomaticMarriagePlus.Global;
using DiplomaticMarriagePlus.Model;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DiplomaticMarriagePlus.Controller
{
    internal class Rimcities_PACombinedAssaultEventController : IncidentWorker
    {
        //触发边缘城市(Rimcities）的联合攻击任务，联合者规定为永久同盟
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return ModsConfig.IsActive("cabbage.rimcities");
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!ModsConfig.IsActive("cabbage.rimcities"))
            {
                //边缘城市没有开启时无法触发。
                Log.Message("[DMP] Rimcities Assault event aborted: Rimcities not installed!");
                return false;
            }

            PermanentAlliance permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
            if (permanentAlliance == null || permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
            {
                Log.Message("[DMP] Rimcities Assault event aborted: No permanent alliance");
                //只有永久同盟生效时才可能启动该事件
                return false;
            }

            if (permanentAlliance.NpcMarriageSeeker.Map != null || permanentAlliance.PlayerBetrothed.Map != null)
            {
                Log.Message("[DMP] Rimcities Assault event aborted: At least one of the couple is on player's colony map");
                //只有二人都不在小地图时才能触发。
                return false;
            }

            TemporaryStay temporaryStay = Find.World.GetComponent<TemporaryStay>();
            if (temporaryStay.IsCurrentlyOnVisit)
            {
                Log.Message("[DMP] Rimcities Assault event aborted: The couples are currently on visit of player colony.");
                //小人在回到玩家殖民地暂住期间无法触发。
                return false;
            }

            Settlement target;
            bool result = CreateAndAddQuest(permanentAlliance.WithFaction, out target);

            if(result)
            {
                //删除RimCities弹出的任务信，并替换成本mod的版本。
                if (Find.LetterStack.LettersListForReading.Count > 0)
                {
                    Find.LetterStack.LettersListForReading.RemoveAt(Find.LetterStack.LettersListForReading.Count - 1);
                }
                var textVocabularyPapaOrMama =
                    ("DMP_PermanentAllianceEventRandomVocabulary_"
                    + (permanentAlliance.PlayerFactionLeader.gender == Gender.Male ? "Father" : "Mother")
                    ).Translate();
                var letter = LetterMaker.MakeLetter(
                    label: "DMP_PermanentAllianceEventRimcitiesAssaultTitle".Translate().CapitalizeFirst(),
                    text: TranslatorFormattedStringExtensions.Translate("DMP_PermanentAllianceEventRimcitiesAssault",
                        NamedArgumentUtility.Named(textVocabularyPapaOrMama, "{0}"),
                        NamedArgumentUtility.Named(permanentAlliance.PlayerBetrothed.Name, "{1}"),
                        NamedArgumentUtility.Named(permanentAlliance.WithFaction.Name, "{2}"),
                        NamedArgumentUtility.Named(target.Faction.Name, "{3}"),
                        NamedArgumentUtility.Named(target.Name, "{4}")
                        ).CapitalizeFirst(),
                    def: LetterDefOf.NeutralEvent,
                    relatedFaction: permanentAlliance.WithFaction,
                    lookTargets:target
                );
                Find.LetterStack.ReceiveLetter(@let: letter);
            }
            
            return result;
        }

        private bool CreateAndAddQuest(Faction alliedFaction, out Settlement target)
        {
            target = null;

            //启动Rimcities的该事件
            var incidentRimcitiesAssaultQuest = Main.IncidentsRimcities.Where(i => i.defName == "Quest_City_Assault").First();
            if (incidentRimcitiesAssaultQuest == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities assault quest. Def Quest_City_Assault not found.");
                return false;
            }

            if(!Utils.RunIncident(incidentRimcitiesAssaultQuest))
            {
                Log.Error("[DMP] Failed to run RimCities assault quest incident.");
                return false;
            }

            //获取Rimcities的任务实例
            var rimCitiesIncidentWorkerQuestType = AccessTools.TypeByName("Cities.IncidentWorker_Quest");
            var questField = rimCitiesIncidentWorkerQuestType.GetField("quest", BindingFlags.NonPublic | BindingFlags.Instance);
            if (questField == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities assault quest. Field quest doesn't exist on Cities.IncidentWorker_Quest.");
                return false;
            }
            var rimCitiesQuestAssaultInstance = questField.GetValue(incidentRimcitiesAssaultQuest.Worker);

            //更改Rimcities该任务内容：友军阵营改为永久同盟
            var rimCitiesQuestAssaultType = AccessTools.TypeByName("Cities.Quest_Assault");
            var alliedFactionField = rimCitiesQuestAssaultType.GetField("alliedFaction", BindingFlags.NonPublic | BindingFlags.Instance);
            if (alliedFactionField == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities assault quest. Field alliedFaction doesn't exist on Cities.Quest_Assault.");
                return false;
            }
            alliedFactionField.SetValue(rimCitiesQuestAssaultInstance, alliedFaction);
            Log.Message("[DMP] Rimcities Assault quest: Allied Faction locked to Permanent Ally.");

            //更改Rimcities该任务内容：进攻目标（需要同时和玩家及永久同盟阵营敌对）
            var targetField = rimCitiesQuestAssaultType.GetField("target", BindingFlags.NonPublic | BindingFlags.Instance);
            if (targetField == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities assault quest. Field target doesn't exist on Cities.Quest_Assault.");
                return false;
            }
            var homeMapProperty = rimCitiesQuestAssaultType.GetProperty("HomeMap", BindingFlags.Public | BindingFlags.Instance);
            if (homeMapProperty == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities assault quest. Property HomeMap doesn't exist on Cities.Quest_Assault.");
                return false;
            }
            Map homeMap = homeMapProperty.GetValue(rimCitiesQuestAssaultInstance) as Map;
            if (homeMap == null)
            {
                Log.Warning("[DMP] Reflection warning for RimCities assault quest. Property HomeMap is null. Using player colony map determined by DMP instead");
                homeMap = Utils.GetPlayerMainColonyMap();
                if(homeMap == null)
                {
                    Log.Warning("[DMP] Reflection warning for RimCities assault quest. Player doesnt have a valid colony map. Incident aborted.");
                    return false;
                }
            }
            var settlements = (from settlement in Find.WorldObjects.Settlements
                               where settlement.def.defName.Equals("City_Faction")
                               && settlement.Faction.HostileTo(Faction.OfPlayer)
                               && settlement.Faction.HostileTo(alliedFaction)
                               select settlement)
                .ToList();
            if(settlements.Count == 0)
            {
                Log.Warning("[DMP] No valid city target for RimCities assault quest.");
                return false;
            }
            target = settlements.OrderBy(settlement => Find.WorldGrid.ApproxDistanceInTiles(settlement.Tile, homeMap.Tile)).FirstOrDefault();
            targetField.SetValue(rimCitiesQuestAssaultInstance, target);
            Log.Message("[DMP] Rimcities Assault quest target chosen : " + target.Name);

            return true;
        }
    }
}