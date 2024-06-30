using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DiplomaticMarriagePlus.Model;
using DiplomaticMarriagePlus.View;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using static System.Collections.Specialized.BitVector32;
using static DiplomaticMarriagePlus.Model.AllianceAgainstPA;

namespace DiplomaticMarriagePlus.Controller
{
    //特殊版动态外交(Dynamic Diplomacy)的联盟事件。使用动态外交的部分模组设置，但仅限于针对永久同盟派系，且有部分细节不同。
    //该事件组成的联盟同样算为动态外交的联盟，期间动态外交模组无法形成新的联盟。
    internal class DynamicDiplomacy_AllianceAgainstPAEventController : IncidentWorker
    {

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!ModsConfig.IsActive("nilchei.dynamicdiplomacycontinued") && !ModsConfig.IsActive("nilchei.dynamicdiplomacy"))
            {
                return false;
            }
            if (DMPModWindow.Instance.settings.enableAllianceAgainstPAEvent == false)
            {
                return false;
            }
            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!ModsConfig.IsActive("nilchei.dynamicdiplomacycontinued") && !ModsConfig.IsActive("nilchei.dynamicdiplomacy"))
            {
                Log.Message("^[DMP] Dynamic Diplomacy not installed. Alliance against PA event aborted.");
                return false;
            }
            if (DMPModWindow.Instance.settings.enableAllianceAgainstPAEvent == false)
            {
                Log.Message("^[DMP] Dynamic Diplomacy not installed. Alliance against PA event disabled in mod config.");
                return false;
            }

            //读取动态外交的mod设置：是否允许联盟
            var incidentWorker_NPCConquestType = AccessTools.TypeByName("DynamicDiplomacy.IncidentWorker_NPCConquest");
            var allowAllianceField = incidentWorker_NPCConquestType.GetField("allowAlliance", BindingFlags.Static | BindingFlags.Public);
            bool allowAlliance = (bool)allowAllianceField.GetValue(null);
            if (!allowAlliance)
            {
                Log.Message("^[DMP] Dynamic Diplomacy mod config doesnt allow alliance. Alliance against PA event aborted.");
                return false;
            }

            //读取动态外交的mod数据：当前联盟冷却时间。
            var diplomacyWorldComponentType = AccessTools.TypeByName("DynamicDiplomacy.DiplomacyWorldComponent");
            var allianceCooldownField = diplomacyWorldComponentType.GetField("allianceCooldown", BindingFlags.Static | BindingFlags.Public);
            var diplomacyWorldComponent = Find.World.GetComponent(diplomacyWorldComponentType);
            int allianceCooldown = (int)allianceCooldownField.GetValue(diplomacyWorldComponent);
            if (allianceCooldown > 0)
            {
                Log.Message("^[DMP] A Dynamic Diplomacy alliance event already exists or in cooldown. Alliance against PA event aborted.");
                return false;
            }

            //检查当前是否已有本事件
            var allianceAgainstPA = Find.World.GetComponent<AllianceAgainstPA>();
            if (allianceAgainstPA != null && allianceAgainstPA.Status != AllianceAgainstPA.AllianceStatus.INACTIVE)
            {
                Log.Message("^[DMP] Alliance against PA event is already active. Alliance against PA event aborted.");
                return false;
            }

            //检查有无永久同盟
            var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
            if(permanentAlliance == null || permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
            {
                Log.Message("^[DMP] No valid permanent ally. Alliance against PA event aborted.");
                return false;
            }

            //计算永久同盟占全球据点百分比
            if (!AllianceAgainstPA.IsFactionTooPowerful(permanentAlliance.WithFaction, GLOBAL_SETTLEMENT_PERCT_THRESHOLD))
            {
                Log.Message("^[DMP] PA not powerful enough. Alliance against PA event aborted.");
                return false;
            }

            //读取动态外交的mod设置：是否允许帝国，是否允许永久敌对派系参加
            var npcDiploSettingsType = AccessTools.TypeByName("DynamicDiplomacy.NPCDiploSettings");
            var npcDiploSettingsInstanceProperty = npcDiploSettingsType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            var npcDiploSettingsInstance = npcDiploSettingsInstanceProperty.GetValue(null);
            var npcDiploSettingsSettingsField = npcDiploSettingsType.GetField("settings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var npcDiploSettingsSettingsInstance = npcDiploSettingsSettingsField.GetValue(npcDiploSettingsInstance);

            var npcDiploModSettingsType = AccessTools.TypeByName("DynamicDiplomacy.NPCDiploModSettings");
            var allowPermField = npcDiploModSettingsType.GetField("repAllowPerm", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool allowPerm = (bool)allowPermField.GetValue(npcDiploSettingsSettingsInstance);
            var excludeEmpireField = npcDiploModSettingsType.GetField("repExcludeEmpire", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool excludeEmpire = (bool)excludeEmpireField.GetValue(npcDiploSettingsSettingsInstance);

            //组建联盟
            allianceAgainstPA.GenerateAllianceFactionList(permanentAlliance, excludeEmpire, allowPerm);
            allianceAgainstPA.Status = AllianceAgainstPA.AllianceStatus.ACTIVE_RUNNING;
            allianceAgainstPA.UpdateFactionRelations(permanentAlliance);

            //弹出信件
            var text = "DMP_DynamicDiplomacyAllianceAgainstPAStarted";
            var letter = LetterMaker.MakeLetter(
                    label: "DMP_DynamicDiplomacyAllianceAgainstPAStartedTitle".Translate().CapitalizeFirst(),
                    text: text.Translate(permanentAlliance.WithFaction).CapitalizeFirst(),
                    def: LetterDefOf.NegativeEvent
                    );
            Find.LetterStack.ReceiveLetter(@let: letter);

            return true;
        }
    }
}
