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
    internal class Rimcities_PACombinedDefenseEventController : IncidentWorker
    {
        public static float initialBaseChance = 2.0f;

        //触发边缘城市(Rimcities）的防御任务，联合者规定为永久同盟
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if(!ModsConfig.IsActive("cabbage.rimcities"))
            {
                return false;
            }
            def.baseChance = initialBaseChance;
            def.baseChanceWithRoyalty = initialBaseChance;
            //如果特殊版全球同盟事件进行中（永久同盟被全球派系联合针对），该事件概率翻3倍。每天最多一次。
            if (ModsConfig.IsActive("nilchei.dynamicdiplomacycontinued") || ModsConfig.IsActive("nilchei.dynamicdiplomacy"))
            {
                var allianceAgainstPA = Find.World.GetComponent<AllianceAgainstPA>();
                if(allianceAgainstPA != null && allianceAgainstPA.Status == AllianceAgainstPA.AllianceStatus.ACTIVE_RUNNING)
                {
                    def.baseChance = initialBaseChance * 3;
                    def.baseChanceWithRoyalty = initialBaseChance * 3;
                }
            }
            return true;
        }
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!ModsConfig.IsActive("cabbage.rimcities"))
            {
                //边缘城市没有开启时无法触发。
                Log.Message("[DMP] Rimcities Defense event aborted: Rimcities not installed!");
                return false;
            }

            PermanentAlliance permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
            if (permanentAlliance == null || permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
            {
                Log.Message("[DMP] Rimcities Defense event aborted: No permanent alliance");
                //只有永久同盟生效时才可能启动该事件
                return false;
            }

            if (permanentAlliance.NpcMarriageSeeker.Map != null || permanentAlliance.PlayerBetrothed.Map != null)
            {
                Log.Message("[DMP] Rimcities Defense event aborted: At least one of the couple is on player's colony map");
                //只有二人都不在小地图时才能触发。
                return false;
            }

            TemporaryStay temporaryStay = Find.World.GetComponent<TemporaryStay>();
            if (temporaryStay.IsCurrentlyOnVisit)
            {
                Log.Message("[DMP] Rimcities Defense event aborted: The couples are currently on visit of player colony.");
                //小人在回到玩家殖民地暂住期间无法触发。
                return false;
            }

            Settlement target;
            Faction enemyFaction;
            bool result = CreateAndAddQuest(permanentAlliance.WithFaction, out target, out enemyFaction);

            if (result)
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
                    label: "DMP_PermanentAllianceEventRimcitiesDefenseTitle".Translate().CapitalizeFirst(),
                    text: TranslatorFormattedStringExtensions.Translate("DMP_PermanentAllianceEventRimcitiesDefense",
                        NamedArgumentUtility.Named(textVocabularyPapaOrMama, "{0}"),
                        NamedArgumentUtility.Named(permanentAlliance.PlayerBetrothed.Name, "{1}"),
                        NamedArgumentUtility.Named(permanentAlliance.WithFaction.Name, "{2}"),
                        NamedArgumentUtility.Named(enemyFaction.Name, "{3}"),
                        NamedArgumentUtility.Named(target.Name, "{4}")
                        ).CapitalizeFirst(),
                    def: LetterDefOf.NeutralEvent,
                    relatedFaction: permanentAlliance.WithFaction,
                    lookTargets: target
                );
                Find.LetterStack.ReceiveLetter(@let: letter);
            }

            return result;
        }
        private bool CreateAndAddQuest(Faction alliedFaction, out Settlement target, out Faction enemyFaction)
        {
            target = null;
            enemyFaction = null;

            //启动Rimcities的该事件
            var incidentRimcitiesDefenseQuest = Main.IncidentsRimcities.Where(i => i.defName == "Quest_City_Defend").First();
            if (incidentRimcitiesDefenseQuest == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities defense quest. Def Quest_City_Defend not found.");
                return false;
            }

            if (!Utils.RunIncident(incidentRimcitiesDefenseQuest))
            {
                Log.Error("[DMP] Failed to run RimCities defense quest incident.");
                return false;
            }

            //获取Rimcities的任务实例
            var rimCitiesIncidentWorkerQuestType = AccessTools.TypeByName("Cities.IncidentWorker_Quest");
            var questField = rimCitiesIncidentWorkerQuestType.GetField("quest", BindingFlags.NonPublic | BindingFlags.Instance);
            if (questField == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities defense quest. Field quest doesn't exist on Cities.IncidentWorker_Quest.");
                return false;
            }
            var rimCitiesQuestDefenseInstance = questField.GetValue(incidentRimcitiesDefenseQuest.Worker);

            Faction oldEnemyFaction = null;
            Settlement oldTarget = null;
            //选择敌军阵营
            var enemyFactions = (from x in Find.FactionManager.AllFactions
                                 where !x.def.hidden
                                      && !x.defeated
                                      && x.HostileTo(Faction.OfPlayer)
                                      && x.HostileTo(alliedFaction)
                                 select x).ToList();
            if (enemyFactions.Count == 0)
            {
                Log.Error("[DMP] Reflection failure for RimCities defense quest. No hostile faction available for this quest.");
                return false;
            }
            enemyFaction = enemyFactions.RandomElement();

            //更改Rimcities该任务内容：敌军阵营
            var rimCitiesQuestDefenseType = AccessTools.TypeByName("Cities.Quest_Defend");
            var enemyFactionField = rimCitiesQuestDefenseType.GetField("enemyFaction", BindingFlags.NonPublic | BindingFlags.Instance);
            if (enemyFactionField == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities defense quest. Field alliedFaction doesn't exist on Cities.Quest_Defend.");
                return false;
            }
            oldEnemyFaction = enemyFactionField.GetValue(rimCitiesQuestDefenseInstance) as Faction;
            enemyFactionField.SetValue(rimCitiesQuestDefenseInstance, enemyFaction);
            Log.Message("[DMP] Rimcities Defend quest: Enemy faction altered: " + enemyFaction.Name);

            //找出任务对应的玩家地图
            var homeMapProperty = rimCitiesQuestDefenseType.GetProperty("HomeMap", BindingFlags.Public | BindingFlags.Instance);
            if (homeMapProperty == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities defense quest. Property HomeMap doesn't exist on Cities.Quest_Defend.");
                return false;
            }
            Map homeMap = homeMapProperty.GetValue(rimCitiesQuestDefenseInstance) as Map;
            if (homeMap == null)
            {
                Log.Warning("[DMP] Reflection warning for RimCities defense quest. Property HomeMap is null. Using player colony map determined by DMP instead.");
                homeMap = Utils.GetPlayerMainColonyMap();
                if (homeMap == null)
                {
                    Log.Warning("[DMP] Reflection warning for RimCities defense quest. Player doesnt have a valid colony map. Incident aborted.");
                    return false;
                }
            }

            //更改Rimcities该任务内容：城市为附近某个永久同盟的大城市
            var settlements = (from settlement in Find.WorldObjects.Settlements
                               where settlement.def.defName.Equals("City_Faction")
                               && settlement.Faction == alliedFaction
                               select settlement)
                .ToList();
            if(settlements.Count == 0)
            {
                Log.Error("[DMP] Reflection failure for RimCities defense quest. No faction city of permanent ally available for this quest.");
                return false;
            }
            target = settlements.OrderBy(settlement => Find.WorldGrid.ApproxDistanceInTiles(settlement.Tile, homeMap.Tile)).FirstOrDefault();

            var cityField = rimCitiesQuestDefenseType.GetField("city", BindingFlags.NonPublic | BindingFlags.Instance);
            oldTarget = cityField.GetValue(rimCitiesQuestDefenseInstance) as Settlement;
            cityField.SetValue(rimCitiesQuestDefenseInstance, target);

            Log.Message("[DMP] Rimcities Defense quest target chosen : " + target.Name);


            //刷新任务的信息。
            var handleField = rimCitiesQuestDefenseType.GetField("handle", BindingFlags.NonPublic | BindingFlags.Instance);
            var handle = handleField.GetValue(rimCitiesQuestDefenseInstance) as Quest;
            handle.description = handle.description
                .Replace(oldEnemyFaction.Name, enemyFaction.Name)
                .Replace(oldTarget.Faction.Name, target.Faction.Name)
                .Replace(oldTarget.Name, target.Name)
                ;
            handle.PartsListForReading.Clear();
            var OnSetupHandleMethod = rimCitiesQuestDefenseType.GetMethod("OnSetupHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            OnSetupHandleMethod.Invoke(rimCitiesQuestDefenseInstance, new Object[] { handle });

            return true;
        }
    }
}
