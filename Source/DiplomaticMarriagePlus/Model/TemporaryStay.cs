using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiplomaticMarriagePlus.Global;
using DiplomaticMarriagePlus.View;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Noise;

namespace DiplomaticMarriagePlus.Model
{
    internal class TemporaryStay : WorldComponent
    {
        private bool _isRunning = false;

        private int _tickLastNostalgia = 0;
        private int _tickLastTemporaryVisitStart = 0;
        private int _tickLastTemporaryVisitEnd = 0;

        private bool _isReadyForNextVisit = false;
        private bool _isCurrentlyOnVisit = false;

        public bool IsRunning { get { return _isRunning; } set { _isRunning = value; } }
        public int TickLastNostalgia { get { return _tickLastNostalgia; } set { _tickLastNostalgia = value; } }
        public int TickLastTemporaryVisitStart { get { return _tickLastTemporaryVisitStart; } set { _tickLastTemporaryVisitStart = value; } }
        public int TickLastTemporaryVisitEnd { get { return _tickLastTemporaryVisitEnd; } set { _tickLastTemporaryVisitEnd = value; } }
        public bool IsCurrentlyOnVisit { get { return _isCurrentlyOnVisit; } set { _isCurrentlyOnVisit = value; } }


        public TemporaryStay(World world) : base(world)
        {

        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if(!_isRunning) { return; }

            if (GenTicks.TicksAbs % (GenDate.TicksPerHour * 3) == 0)
            {
                if(_isReadyForNextVisit && IsCurrentlyTimeForVisit())
                {
                    Map map = TradeUtility.PlayerHomeMapWithMostLaunchableSilver();
                    if (GenHostility.AnyHostileActiveThreatToPlayer(map))
                    {
                        //如果此刻地图上有敌人则无法触发。
                        return;
                    }
                    var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
                    if(permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
                    {
                        //如果永久联盟已终结，则重置所有属性,不再运行。
                        _isRunning = false;
                        return;
                    }

                    //地图上生成小人，开始访问
                    Faction WithFaction = permanentAlliance.WithFaction;
                    Pawn playerBetrothed = permanentAlliance.PlayerBetrothed;
                    Pawn npcMarriageSeeker = permanentAlliance.NpcMarriageSeeker;

                    List<Pawn> couple = new List<Pawn>();
                    couple.Add(playerBetrothed);
                    couple.Add(npcMarriageSeeker);
                    List<Pawn> incidentPawns;
                    IntVec3 stageLog;

                    playerBetrothed.SetFaction(Faction.OfPlayer);
                    npcMarriageSeeker.SetFaction(Faction.OfPlayer);

                    Utils.SpawnVIPAndIncidentPawns(map, Faction.OfPlayer, couple, 0, PawnGroupKindDefOf.Combat, out incidentPawns, out stageLog);

                    //弹出小人抵达信件
                    var textVocabularyPapaOrMama =
                        ("DMP_PermanentAllianceEventRandomVocabulary_"
                        + (permanentAlliance.PlayerFactionLeader.gender == Gender.Male ? "Father" : "Mother")
                        ).Translate();
                    var letter = LetterMaker.MakeLetter(
                        label: "DMP_PermanentAllianceEventTemporaryStayArrivalTitle".Translate().CapitalizeFirst(),
                        text: "DMP_PermanentAllianceEventTemporaryStayArrival".Translate(
                            textVocabularyPapaOrMama
                            ).CapitalizeFirst(),
                        def: LetterDefOf.PositiveEvent,
                        relatedFaction: WithFaction
                    );
                    Find.LetterStack.ReceiveLetter(@let: letter);

                    _isReadyForNextVisit = false;
                    _isCurrentlyOnVisit = true;
                }
                else if (_isCurrentlyOnVisit && !IsCurrentlyTimeForVisit())
                {
                    //访问时间限制已到，改回阵营，迫使两位小人离开。
                    var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
                    Faction WithFaction = permanentAlliance.WithFaction;
                    Pawn playerBetrothed = permanentAlliance.PlayerBetrothed;
                    Pawn npcMarriageSeeker = permanentAlliance.NpcMarriageSeeker;
                    if(WithFaction != null)
                    {
                        if (playerBetrothed != null && !playerBetrothed.Dead)
                        {
                            playerBetrothed.SetFaction(WithFaction);
                        }
                        if (npcMarriageSeeker != null && !npcMarriageSeeker.Dead)
                        {
                            npcMarriageSeeker.SetFaction(WithFaction);
                        }
                        //弹出告别信件
                        var textVocabularyPapaOrMama =
                            ("DMP_PermanentAllianceEventRandomVocabulary_"
                            + (permanentAlliance.PlayerFactionLeader.gender == Gender.Male ? "Father" : "Mother")
                            ).Translate();
                        var letter = LetterMaker.MakeLetter(
                            label: "DMP_PermanentAllianceEventTemporaryStayDepartureTitle".Translate().CapitalizeFirst(),
                            text: "DMP_PermanentAllianceEventTemporaryStayDeparture".Translate(
                                WithFaction.Name,
                                textVocabularyPapaOrMama
                                ).CapitalizeFirst(),
                            def: LetterDefOf.PositiveEvent,
                            relatedFaction: WithFaction
                        );
                        Find.LetterStack.ReceiveLetter(@let: letter);
                    }
                    _isCurrentlyOnVisit = false;
                    _tickLastNostalgia = 0;
                }
            }
            if (GenTicks.TicksAbs % (GenDate.TicksPerDay * 1) == GenDate.HoursPerDay * 10)
            {
                int temporaryStayStartAfterDaysMinimum = DMPModWindow.Instance.settings.temporaryStayStartAfterDaysMinimum;
                int temporaryStayStartAfterDaysMaximum = DMPModWindow.Instance.settings.temporaryStayStartAfterDaysMaximum;
                int temporaryStayDurationMinimum = DMPModWindow.Instance.settings.temporaryStayDurationMinimum;
                int temporaryStayDurationMaximum = DMPModWindow.Instance.settings.temporaryStayDurationMaximum;

                //联姻生效期间，小人如果不在小地图上，则每天有一定几率得思乡病。
                var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
                if (permanentAlliance.IsValid() == PermanentAlliance.Validity.VALID 
                    && permanentAlliance.PlayerBetrothed.Map == null 
                    ) {
                    if (TryGetNostalgia())
                    {
                        PlanNextVisit(Rand.RangeInclusive(temporaryStayStartAfterDaysMinimum, temporaryStayStartAfterDaysMaximum),
                            Rand.RangeInclusive(temporaryStayDurationMinimum, temporaryStayDurationMaximum)
                            );
                    }
                }
            }
        }

        //根据上一次访问时间，有一定几率得思乡病。
        //可用来强制思乡病，以此初始化第一次访问。
        public bool TryGetNostalgia()
        {
            if (_tickLastNostalgia > 0)
            {
                //已有思乡病
                return false;
            }

            if (_isCurrentlyOnVisit) 
            {
                //目前正在访问中
                return false;
            }

            //在上一次访问结束，或永久同盟成立后（如果是第一次触发该事件）一段时间过后，便有可能得思乡病。几率随着时间延长而逐渐变高，直到判定成功。
            float daysSinceLastVisitEnd = (GenTicks.TicksAbs - _tickLastTemporaryVisitEnd) * 1.0f / GenDate.TicksPerDay;

            var minimumDaysForNostalgia = DMPModWindow.Instance.settings.temporaryStayMinimumDaysForNostalgia;
            if (daysSinceLastVisitEnd > minimumDaysForNostalgia)
            {
                var chanceInitial = DMPModWindow.Instance.settings.temporaryStayDailyChanceInitial; 
                var chanceIncreasePerDay = DMPModWindow.Instance.settings.temporaryStayDailyChanceIncrease; 
                if (Rand.Range(0, 100) < chanceInitial + (daysSinceLastVisitEnd - minimumDaysForNostalgia) * chanceIncreasePerDay)
                {
                    _tickLastNostalgia = GenTicks.TicksAbs;
                    return true;
                }
            }
            return false;
        }

        //计算下次访问玩家殖民地的抵达和离开时间。
        //如果尚未初始化，该方法可以用来第一次初始化访问。
        public void PlanNextVisit(int startAfterDays, int durationDays)
        {
            _tickLastTemporaryVisitStart = GenTicks.TicksAbs + GenDate.TicksPerDay * startAfterDays;
            _tickLastTemporaryVisitEnd = _tickLastTemporaryVisitStart + GenDate.TicksPerDay * durationDays;
            _isReadyForNextVisit = true;

            //弹出信件，提前通知来访。
            var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
            Faction WithFaction = permanentAlliance.WithFaction;
            Pawn playerBetrothed = permanentAlliance.PlayerBetrothed;
            Pawn npcMarriageSeeker = permanentAlliance.NpcMarriageSeeker;

            var textVocabularyPapaOrMama =
            ("DMP_PermanentAllianceEventRandomVocabulary_"
            + (permanentAlliance.PlayerFactionLeader.gender == Gender.Male ? "Father" : "Mother")
            ).Translate();
            var letter = LetterMaker.MakeLetter(
                label: "DMP_PermanentAllianceEventTemporaryStayPlanVisitTitle".Translate().CapitalizeFirst(),
                text: "DMP_PermanentAllianceEventTemporaryStayPlanVisit".Translate(
                    WithFaction.Name,
                    textVocabularyPapaOrMama,
                    npcMarriageSeeker.Label,
                    startAfterDays,
                    durationDays
                    ).CapitalizeFirst(),
                def: LetterDefOf.PositiveEvent,
                relatedFaction: WithFaction
            );
            Find.LetterStack.ReceiveLetter(@let: letter);
        }

        //当前是否正在访问玩家殖民地的时间段内。
        public bool IsCurrentlyTimeForVisit()
        {
            if (_tickLastTemporaryVisitStart == 0 ||  _tickLastTemporaryVisitEnd == 0)
            {
                return false;
            }
            return (GenTicks.TicksAbs >= _tickLastTemporaryVisitStart && GenTicks.TicksAbs <= _tickLastTemporaryVisitEnd);
        }

        /*public void OnArrival()
        {
            _tickLastNostalgia = 0;
        }

        public void ResetAll()
        {
            _tickLastNostalgia = _tickLastTemporaryVisitStart = _tickLastTemporaryVisitEnd = 0;
            _isReadyForNextVisit = false;
        }

        public void InitializeByForcingFirstNostalgia()
        {
            TryGetNostalgia(true);
        }*/

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref _isRunning, "DMP_PermanentAlliance_IsRunning", false);
            Scribe_Values.Look<int>(ref _tickLastNostalgia, "DMP_PermanentAlliance_TemporaryStay_TickLastNostalgia", 0);
            Scribe_Values.Look<int>(ref _tickLastTemporaryVisitStart, "DMP_PermanentAlliance_TemporaryStay_TickLastTemporaryVisitStart", 0);
            Scribe_Values.Look<int>(ref _tickLastTemporaryVisitEnd, "DMP_PermanentAlliance_TemporaryStay_TickLastTemporaryVisitEnd", 0);
            Scribe_Values.Look<bool>(ref _isReadyForNextVisit, "DMP_PermanentAlliance_TemporaryStay_IsReadyForNextVisit", false);
            Scribe_Values.Look<bool>(ref _isCurrentlyOnVisit, "DMP_PermanentAlliance_TemporaryStay_IsCurrentlyOnVisit", false);
        }
    }
}
