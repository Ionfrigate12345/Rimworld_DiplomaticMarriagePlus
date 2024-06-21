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
            var allowAllianceField = incidentWorker_NPCConquestType.GetField("allowAlliance", BindingFlags.Static);
            bool allowAlliance = (bool)allowAllianceField.GetValue(null);
            if (!allowAlliance)
            {
                Log.Message("^[DMP] Dynamic Diplomacy mod config doesnt allow alliance. Alliance against PA event aborted.");
                return false;
            }

            //读取动态外交的mod数据：当前联盟冷却时间。
            var diplomacyWorldComponentType = AccessTools.TypeByName("DynamicDiplomacy.DiplomacyWorldComponent");
            var diplomacyWorldComponent = Find.World.components.Where(c => diplomacyWorldComponentType.IsInstanceOfType(c)).FirstOrDefault();
            var allianceCooldownField = diplomacyWorldComponent.GetType().GetField("allianceCooldown");
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
            if(!AllianceAgainstPA.IsPAFactionTooPowerful(permanentAlliance))
            {
                Log.Message("^[DMP] PA not powerful enough. Alliance against PA event aborted.");
                return false;
            }

            //读取动态外交的mod设置：是否允许帝国，是否允许永久敌对派系参加
            var IncidentWorker_NPCDiploChangeType = AccessTools.TypeByName("DynamicDiplomacy.IncidentWorker_NPCDiploChange");
            var allowPermField = IncidentWorker_NPCDiploChangeType.GetField("allowPerm", BindingFlags.Static);
            bool allowPerm = (bool)allowPermField.GetValue(null);
            var excludeEmpireField = IncidentWorker_NPCDiploChangeType.GetField("excludeEmpire", BindingFlags.Static);
            bool excludeEmpire = (bool)excludeEmpireField.GetValue(null);

            //组建联盟
            allianceAgainstPA.formAlliance(permanentAlliance, allowPerm, excludeEmpire);
            allianceAgainstPA.Status = AllianceAgainstPA.AllianceStatus.ACTIVE_RUNNING;
            allianceAgainstPA.UpdateFactionRelations(permanentAlliance);

            //TODO:弹出信件

            return true;
        }
    }
}
