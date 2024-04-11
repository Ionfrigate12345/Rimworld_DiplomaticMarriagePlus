using System.Collections.Generic;
using DiplomaticMarriagePlus.Global;
using DiplomaticMarriagePlus.Model.LordJob;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Verse.Noise;

namespace DiplomaticMarriagePlus.Model
{
    internal class RandomVisitAllyCaravanRefugeAttack : WorldComponent, ILoadReferenceable
    {
        private int _tickTriggerNext = int.MaxValue;
        private Faction _hostileFactionTriggerNext;
        private Map _mapTriggerNext;
        private Lord _lordCaravan;
        private bool _onGoingAttackFlag = false;

        public int TickTriggerNext { get { return _tickTriggerNext;  } set { _tickTriggerNext = value;  } }
        public Faction HostileFactionTriggerNext { get { return _hostileFactionTriggerNext; } set { _hostileFactionTriggerNext = value;  } }
        public Map MapTriggerNext { get { return _mapTriggerNext; } set { _mapTriggerNext = value; } }
        public Lord LordCaravan { get { return _lordCaravan; } set { _lordCaravan = value; } }
        public bool OnGoingAttackFlag { get { return _onGoingAttackFlag; } set { _onGoingAttackFlag = value; } }

        public RandomVisitAllyCaravanRefugeAttack(World world) : base(world)
        {

        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if(!OnGoingAttackFlag && GenTicks.TicksAbs > TickTriggerNext)
            {
                TickTriggerNext = int.MaxValue;
                var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();

                //触发入侵
                List<Pawn> incidentPawns;
                IntVec3 stageLoc;
                Utils.SpawnVIPAndIncidentPawns(
                    MapTriggerNext, 
                    HostileFactionTriggerNext, 
                    null, 
                    Utils.GetRandomThreatPointsByPlayerWealth(MapTriggerNext, Rand.Range(100, 200)), 
                    PawnGroupKindDefOf.Combat, 
                    out incidentPawns, 
                    out stageLoc
                    );

                //攻击目标为两个关键小人夫妇。
                List<Thing> caravanPawnsThings = new List<Thing>();
                caravanPawnsThings.Add(permanentAlliance.PlayerBetrothed);
                caravanPawnsThings.Add(permanentAlliance.NpcMarriageSeeker);
                var lordJobRaiders = new LordJob_AssaultThings(HostileFactionTriggerNext, caravanPawnsThings);
                var lordRaiders = LordMaker.MakeNewLord(_hostileFactionTriggerNext, lordJobRaiders, _mapTriggerNext, incidentPawns);
                OnGoingAttackFlag = true;

                //弹出信件
                var textVocabularyPapaOrMama =
                    ("DMP_PermanentAllianceEventRandomVocabulary_"
                    + (permanentAlliance.PlayerFactionLeader.gender == Gender.Male ? "Father" : "Mother")
                    ).Translate();
                var letter = LetterMaker.MakeLetter(
                    label: "DMP_PermanentAllianceEventRandomVisitCaravanEnemyIncomingTitle".Translate().CapitalizeFirst(),
                    text: "DMP_PermanentAllianceEventRandomVisitCaravanEnemyIncoming".Translate(
                        textVocabularyPapaOrMama,
                        permanentAlliance.PlayerBetrothed.Label,
                        permanentAlliance.NpcMarriageSeeker.Label,
                        permanentAlliance.WithFaction.Name
                        ).CapitalizeFirst(),
                    def: LetterDefOf.ThreatBig,
                    relatedFaction: permanentAlliance.WithFaction
                );
                Find.LetterStack.ReceiveLetter(@let: letter);
                Find.TickManager.Pause();
            }

            if (OnGoingAttackFlag && GenTicks.TicksAbs % GenDate.TicksPerHour == 0)
            {
                var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();

                //如果永久联盟已失效（通常是夫妇或玩家派系领袖有人在战斗中死亡），则判定为失败。
                if (permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
                {
                    (LordCaravan.LordJob as LordJobCaravanRandomVisit).SetIsConditionMet(true);//允许商队离开地图
                    OnGoingAttackFlag = false;
                    ClearAllData();
                    return;
                }

                //敌人已清理干净。
                if (!GenHostility.AnyHostileActiveThreatToPlayer(_mapTriggerNext))
                {
                    (LordCaravan.LordJob as LordJobCaravanRandomVisit).SetIsConditionMet(true);//允许商队离开地图
                    OnGoingAttackFlag = false;

                    //夫妇的商队离开，感谢信，留下礼物
                    var textVocabularyPapaOrMama =
                    ("DMP_PermanentAllianceEventRandomVocabulary_"
                    + (permanentAlliance.PlayerFactionLeader.gender == Gender.Male ? "Father" : "Mother")
                    ).Translate();
                    var letter = LetterMaker.MakeLetter(
                        label: "DMP_PermanentAllianceEventRandomVisitCaravanEnemyDefeatedTitle".Translate().CapitalizeFirst(),
                        text: "DMP_PermanentAllianceEventRandomVisitCaravanEnemyDefeated".Translate(
                            textVocabularyPapaOrMama, 
                            Faction.OfPlayer.Name, 
                            permanentAlliance.WithFaction.Name
                            ).CapitalizeFirst(),
                        def: LetterDefOf.PositiveEvent,
                        relatedFaction: permanentAlliance.WithFaction
                    );
                    Find.LetterStack.ReceiveLetter(@let: letter);

                    ClearAllData();
                }
            }
            
        }

        private void ClearAllData()
        {
            TickTriggerNext = int.MaxValue;
            HostileFactionTriggerNext = null;
            MapTriggerNext = null;
            OnGoingAttackFlag = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref _tickTriggerNext, "DMP_PermanentAlliance_RandomVisitCaravanAttack_TickTriggerNext", int.MaxValue);
            Scribe_References.Look<Faction>(ref _hostileFactionTriggerNext, "DMP_PermanentAlliance_RandomVisitCaravanAttack_HostileFactionTriggerNext");
            Scribe_References.Look<Map>(ref _mapTriggerNext, "DMP_PermanentAlliance_RandomVisitCaravanAttack_MapTriggerNext");
            Scribe_References.Look<Lord>(ref _lordCaravan, "DMP_PermanentAlliance_RandomVisitCaravanAttack_LordCaravan");
            Scribe_Values.Look<bool>(ref _onGoingAttackFlag, "DMP_PermanentAlliance_RandomVisitCaravanAttack_OnGoingAttackFlag");
        }

        public string GetUniqueLoadID()
        {
            return "DMP_RandomVisitCaravanAttack";
        }

    }
}
