using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DiplomaticMarriagePlus.Model
{
    public class AllianceAgainstPA : WorldComponent, ILoadReferenceable
    {
        public enum AllianceStatus
        {
            INACTIVE, 
            ACTIVE_RUNNING, 
            ACTIVE_STOPPING, 
        }

        private AllianceStatus _status;
        public AllianceStatus Status { get { return _status; } set { _status = value; } }

        //如果永久同盟被全球联盟事件针对，该列表记录该联盟的所有派系。
        private List<Faction> _allianceAgainstPAFactionList;
        public List<Faction> AllianceAgainstPAFactionList { get { return _allianceAgainstPAFactionList; } set { _allianceAgainstPAFactionList = value; } }

        public const float GLOBAL_SETTLEMENT_PERCT_THRESHOLD = 0.0f;  /*TODO: change this value after debug*/

        public AllianceAgainstPA(World world) : base(world)
        {
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (_status == AllianceStatus.INACTIVE)
            {
                return;
            }

            var tickCount = GenTicks.TicksAbs;

            //每天扫描一次派系之间的关系
            if (tickCount % GenDate.TicksPerDay == 3000)
            {
                var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
                UpdateFactionRelations(permanentAlliance);
            }
        }

        public string GetUniqueLoadID()
        {
            return "DMP_AllianceAgainstPA";
        }

        public void UpdateFactionRelations(PermanentAlliance permanentAlliance)
        {
            if (_status == AllianceStatus.INACTIVE)
            {
                return;
            }

            if (_allianceAgainstPAFactionList.Count < 2)
            {
                Log.Message("^[DMP] Not enough candidate factions for alliance. Alliance against PA disbanded..");
                _status = AllianceStatus.ACTIVE_STOPPING;
                return;
            }

            if(permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
            {
                Log.Message("^[DMP] Player no longer has a valid PA. Alliance against PA disbanded..");
                _status = AllianceStatus.ACTIVE_STOPPING;
                return;
            }

            if(!IsPAFactionTooPowerful(permanentAlliance))
            {
                Log.Message("^[DMP] PA faction is no longer powerful enough. Alliance against PA disbanded..");
                _status = AllianceStatus.ACTIVE_STOPPING;
                return;
            }

            var diplomacyWorldComponentType = AccessTools.TypeByName("DynamicDiplomacy.DiplomacyWorldComponent");
            var diplomacyWorldComponent = Find.World.components.Where(c => diplomacyWorldComponentType.IsInstanceOfType(c)).FirstOrDefault();
            var allianceCooldownField = diplomacyWorldComponent.GetType().GetField("allianceCooldown");
            if (_status == AllianceStatus.ACTIVE_STOPPING)
            {
                allianceCooldownField.SetValue(diplomacyWorldComponent, 0); //允许动态外交模组触发别的联盟事件。
                _status = AllianceStatus.INACTIVE;
                //TODO: 弹出信件
                //TODO: 是否该恢复外交关系？
                return;
            }

            //锁定联盟派系之间的非敌对关系，锁定和玩家及玩家的永久同盟之间的敌对关系
            foreach (var faction1 in _allianceAgainstPAFactionList)
            {
                foreach (var faction2 in _allianceAgainstPAFactionList)
                {
                    if (faction1 != faction2)
                    {
                        FactionRelation factionRelation12 = faction1.RelationWith(faction2, false);
                        if (factionRelation12.kind == FactionRelationKind.Hostile)
                        {
                            factionRelation12.kind = FactionRelationKind.Neutral;
                        }
                    }
                }
                faction1.RelationWith(Faction.OfPlayer, false).kind = FactionRelationKind.Hostile;
                faction1.RelationWith(permanentAlliance.WithFaction, false).kind = FactionRelationKind.Hostile;
            }
            allianceCooldownField.SetValue(diplomacyWorldComponent, 10000); //禁止动态外交模组触发别的联盟事件。
        }

        public List<Faction> formAlliance(PermanentAlliance permanentAlliance, bool excludeEmpire, bool allowPerm)
        {
            _allianceAgainstPAFactionList = (from x in Find.FactionManager.AllFactionsVisible
                                    where x.def.settlementGenerationWeight > 0f
                                    && !x.def.hidden
                                    && !x.IsPlayer
                                    && !x.defeated
                                    && x != permanentAlliance.WithFaction
                                    && x.leader != null
                                    && !x.leader.IsPrisoner
                                    && !x.leader.Spawned
                                    && (!excludeEmpire || x.def != FactionDefOf.Empire)
                                    && (allowPerm || !x.def.permanentEnemy)
                                    select x).ToList<Faction>();

            if (_allianceAgainstPAFactionList.Count < 2)
            {
                Log.Message("^[DMP] Not enough candidate factions for alliance. Alliance against PA event aborted.");
                _allianceAgainstPAFactionList = new List<Faction>();
            }

            return _allianceAgainstPAFactionList;
        }

        public static bool IsPAFactionTooPowerful(PermanentAlliance permanentAlliance)
        {
            var globalSettlements = Find.WorldObjects.Settlements.Where(settlement =>
                    !settlement.def.defName.Equals("City_Abandoned") //排除边缘城市据点中的废弃据点和鬼城
                    && !settlement.def.defName.Equals("City_Ghost")
                    ).ToList();
            int totalGlobalSettlements = globalSettlements.Count;
            int totalPASettlements = globalSettlements.Where(settlement => settlement.Faction == permanentAlliance.WithFaction).ToList().Count();
            if (totalPASettlements * 1.0f / totalGlobalSettlements < GLOBAL_SETTLEMENT_PERCT_THRESHOLD)
            {
                return false;
            }
            return true;
        }
    }
}
