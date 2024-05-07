using System.Collections.Generic;
using System.Linq;
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
        private Lord _lordCaravan, _lordRaider;
        private bool _hasOnGoingAttackFlag = false;
        private bool _isEventStartedFlag = false;

        public int TickTriggerNext { get { return _tickTriggerNext;  } set { _tickTriggerNext = value;  } }
        public Faction HostileFactionTriggerNext { get { return _hostileFactionTriggerNext; } set { _hostileFactionTriggerNext = value;  } }
        public Map MapTriggerNext { get { return _mapTriggerNext; } set { _mapTriggerNext = value; } }
        public Lord LordCaravan { get { return _lordCaravan; } set { _lordCaravan = value; } }
        public Lord LordRaider { get { return _lordRaider; } set { _lordRaider = value; } }
        public bool HasOnGoingAttackFlag { get { return _hasOnGoingAttackFlag; } set { _hasOnGoingAttackFlag = value; } }
        public bool IsEventStartedFlag { get { return _isEventStartedFlag; } set { _isEventStartedFlag = value; } }

        public RandomVisitAllyCaravanRefugeAttack(World world) : base(world)
        {

        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            /*if(!IsEventStartedFlag && !HasOnGoingAttackFlag)
            {
                return;
            }*/

            if(GenTicks.TicksAbs % GenDate.TicksPerHour != 150)
            {
                return;
            }

            if (IsEventStartedFlag && HasOnGoingAttackFlag)
            {
                var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();

                //如果永久联盟已失效（通常是夫妇或玩家派系领袖有人在战斗中死亡），则判定为失败。
                if (permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
                {
                    (LordCaravan.LordJob as LordJobCaravanRandomVisit).SetIsConditionMetExit(true);//允许商队离开地图
                    //HasOnGoingAttackFlag = false;
                    ClearAllData();
                    return;
                }

                //敌人已清理干净。
                if (!GenHostility.AnyHostileActiveThreatToPlayer(_mapTriggerNext))
                {
                    (LordCaravan.LordJob as LordJobCaravanRandomVisit).SetIsConditionMetExit(true);//允许商队离开地图
                    //HasOnGoingAttackFlag = false;

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

            if (IsEventStartedFlag && !HasOnGoingAttackFlag && GenTicks.TicksAbs > TickTriggerNext)
            {
                //触发入侵
                var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();

                List<Pawn> incidentPawns = new List<Pawn>();
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

                /*int wave = 1;
                int rand = Rand.Range(0, 100);
                if (rand < 30)
                {
                    wave = 3;
                }
                else if (rand < 60)
                {
                    wave = 2;
                }
                while(wave > 0)
                {
                    IntVec3 stageLoc;
                    List<Pawn> incidentPawnsWave;
                    wave--;
                    Utils.SpawnVIPAndIncidentPawns(
                    MapTriggerNext,
                    HostileFactionTriggerNext,
                    null,
                    Utils.GetRandomThreatPointsByPlayerWealth(MapTriggerNext, Rand.Range(100, 200)),
                    PawnGroupKindDefOf.Combat,
                    out incidentPawnsWave,
                    out stageLoc
                    );
                    incidentPawns.Concat(incidentPawnsWave).ToList();
                }*/

                //攻击目标为两个关键小人夫妇。
                List<Thing> caravanPawnsThings = new List<Thing>
                {
                    permanentAlliance.PlayerBetrothed,
                    permanentAlliance.NpcMarriageSeeker
                };
                var lordJobRaiders = new LordJob_AssaultThings(HostileFactionTriggerNext, caravanPawnsThings);
                LordRaider = LordMaker.MakeNewLord(_hostileFactionTriggerNext, lordJobRaiders, _mapTriggerNext, incidentPawns);
                HasOnGoingAttackFlag = true;
                TickTriggerNext = int.MaxValue;

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

            

        }

        private void ClearAllData()
        {
            TickTriggerNext = int.MaxValue;
            HostileFactionTriggerNext = null;
            MapTriggerNext = null;
            HasOnGoingAttackFlag = false;
            IsEventStartedFlag = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref _tickTriggerNext, "DMP_PermanentAlliance_RandomVisitCaravanAttack_TickTriggerNext", int.MaxValue);
            Scribe_References.Look<Faction>(ref _hostileFactionTriggerNext, "DMP_PermanentAlliance_RandomVisitCaravanAttack_HostileFactionTriggerNext");
            Scribe_References.Look<Map>(ref _mapTriggerNext, "DMP_PermanentAlliance_RandomVisitCaravanAttack_MapTriggerNext");
            Scribe_References.Look<Lord>(ref _lordCaravan, "DMP_PermanentAlliance_RandomVisitCaravanAttack_LordCaravan");
            Scribe_References.Look<Lord>(ref _lordRaider, "DMP_PermanentAlliance_RandomVisitCaravanAttack_LordRaider");
            Scribe_Values.Look<bool>(ref _hasOnGoingAttackFlag, "DMP_PermanentAlliance_RandomVisitCaravanAttack_OnGoingAttackFlag");
            Scribe_Values.Look<bool>(ref _isEventStartedFlag, "DMP_PermanentAlliance_RandomVisitCaravanAttack_IsEventStartedFlag");
        }

        public string GetUniqueLoadID()
        {
            return "DMP_RandomVisitCaravanAttack";
        }

    }
}
