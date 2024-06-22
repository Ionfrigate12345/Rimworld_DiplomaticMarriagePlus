using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            ACTIVE_STOPPING_PA_TOO_WEAK,
            ACTIVE_STOPPING_PA_ENDED,
            ACTIVE_STOPPING_PA_NO_ENOUGH_FACTIONS,
        }

        private AllianceStatus _status;
        public AllianceStatus Status { get { return _status; } set { _status = value; } }

        //如果永久同盟被全球联盟事件针对，该列表记录该联盟的所有派系。
        private List<Faction> _allianceAgainstPAFactionList;
        public List<Faction> AllianceAgainstPAFactionList { get { return _allianceAgainstPAFactionList; } set { _allianceAgainstPAFactionList = value; } }

        public const float GLOBAL_SETTLEMENT_PERCT_THRESHOLD = 0.33f;

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

            //每X小时扫描一次派系之间的关系
            if (tickCount % (GenDate.TicksPerHour * 3) == 2000)
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
                _status = AllianceStatus.ACTIVE_STOPPING_PA_NO_ENOUGH_FACTIONS;
            }

            if (permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
            {
                Log.Message("^[DMP] Player no longer has a valid PA. Alliance against PA disbanded..");
                _status = AllianceStatus.ACTIVE_STOPPING_PA_ENDED;
            }

            if (!IsPAFactionTooPowerful(permanentAlliance))
            {
                Log.Message("^[DMP] PA faction is no longer powerful enough. Alliance against PA disbanded..");
                _status = AllianceStatus.ACTIVE_STOPPING_PA_TOO_WEAK;
            }

            var diplomacyWorldComponentType = AccessTools.TypeByName("DynamicDiplomacy.DiplomacyWorldComponent");
            var diplomacyWorldComponent = Find.World.components.Where(c => diplomacyWorldComponentType.IsInstanceOfType(c)).FirstOrDefault();
            var allianceCooldownField = diplomacyWorldComponent.GetType().GetField("allianceCooldown", BindingFlags.Static | BindingFlags.Public);
            if (_status != AllianceStatus.INACTIVE && _status != AllianceStatus.ACTIVE_RUNNING) //任何一种即将停止事件的状态
            {
                //动态外交模组的联盟事件CD时间恢复到0，从而允许其触发下一个联盟事件。
                //但如果结束理由是因为玩家永久同盟的终止，则维持10个动态外交事件的CD，即转化为动态外交的常规联盟事件，变成NPC派系之间的联盟战争，和玩家无关
                allianceCooldownField.SetValue(diplomacyWorldComponent, _status == AllianceStatus.ACTIVE_STOPPING_PA_ENDED ? 10 : 0);

                //TODO: 是否该恢复外交关系？
                var text = "DMP_DynamicDiplomacyAllianceAgainstPAEnded_" + _status.ToString().Replace("ACTIVE_STOPPING_", "");
                var letter = LetterMaker.MakeLetter(
                        label: "DMP_DynamicDiplomacyAllianceAgainstPAEndedTitle".Translate().CapitalizeFirst(),
                        text: text.Translate(permanentAlliance.WithFaction).CapitalizeFirst(),
                        def: LetterDefOf.PositiveEvent
                        );
                Find.LetterStack.ReceiveLetter(@let: letter);

                //所有永久敌对派系恢复和其它派系的敌对）
                foreach (Faction faction1 in _allianceAgainstPAFactionList)
                {
                    if (faction1.def.permanentEnemy)
                    {
                        foreach (Faction faction2 in _allianceAgainstPAFactionList)
                        {
                            if (faction1 != faction2)
                            {
                                faction1.TryAffectGoodwillWith(other: faction2, goodwillChange: -200, canSendMessage: false, canSendHostilityLetter: false);
                                FactionRelation factionRelation = faction1.RelationWith(faction2, false);
                                factionRelation.kind = FactionRelationKind.Hostile;

                                faction2.TryAffectGoodwillWith(other: faction1, goodwillChange: -200, canSendMessage: false, canSendHostilityLetter: false);
                                FactionRelation factionRelation2 = faction2.RelationWith(faction1, false);
                                factionRelation2.kind = FactionRelationKind.Hostile;
                            }
                        }
                    }
                }

                _status = AllianceStatus.INACTIVE;
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
                            faction1.TryAffectGoodwillWith(other: faction2, goodwillChange: 200, canSendMessage: false, canSendHostilityLetter: false);
                            factionRelation12.kind = FactionRelationKind.Ally;
                        }
                    }
                }
                faction1.TryAffectGoodwillWith(other: Faction.OfPlayer, goodwillChange: -200, canSendMessage: true, canSendHostilityLetter: true);
                faction1.RelationWith(Faction.OfPlayer, false).kind = FactionRelationKind.Hostile;
                faction1.TryAffectGoodwillWith(other: permanentAlliance.WithFaction, goodwillChange: -200, canSendMessage: false, canSendHostilityLetter: false);
                faction1.RelationWith(permanentAlliance.WithFaction, false).kind = FactionRelationKind.Hostile;
            }
            allianceCooldownField.SetValue(null, Int32.MaxValue); //无时间限制，禁止动态外交模组触发别的联盟事件。
        }

        public List<Faction> GenerateAllianceFactionList(PermanentAlliance permanentAlliance, bool excludeEmpire, bool allowPerm)
        {
            var allianceAgainstPAFactions = (from x in Find.FactionManager.AllFactionsVisible
                                             where x.def.settlementGenerationWeight > 0f
                                             && !x.def.hidden
                                             && !x.defeated
                                             && x != Faction.OfPlayer
                                             && x != permanentAlliance.WithFaction
                                             select x).ToList();
            if (excludeEmpire)
            {
                allianceAgainstPAFactions = allianceAgainstPAFactions.Where(x => x.def != FactionDefOf.Empire).ToList();
            }
            if (!allowPerm)
            {
                allianceAgainstPAFactions = allianceAgainstPAFactions.Where(x => !x.def.permanentEnemy).ToList();
            }
            _allianceAgainstPAFactionList = allianceAgainstPAFactions.ToList();

            if (_allianceAgainstPAFactionList.Count < 2)
            {
                Log.Message("^[DMP] Not enough candidate factions for alliance. Alliance against PA event aborted.");
                _allianceAgainstPAFactionList.Clear();
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
            if ((totalPASettlements * 1.0f / totalGlobalSettlements) < GLOBAL_SETTLEMENT_PERCT_THRESHOLD)
            {
                return false;
            }
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look<AllianceStatus>(ref _status, "DMP_AllianceAgainstPAStatus", AllianceStatus.INACTIVE);
            Scribe_Collections.Look(ref _allianceAgainstPAFactionList, "DMP_AllianceAgainstPAFactionList", LookMode.Reference);
        }
    }
}
