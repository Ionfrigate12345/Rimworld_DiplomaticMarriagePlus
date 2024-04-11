using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiplomaticMarriagePlus.Global;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Noise;

namespace DiplomaticMarriagePlus.Model
{
    internal class TemporaryStay : WorldComponent
    {
        private int _tickLastNostalgia = 0;
        private int _tickLastTemporaryVisitStart = 0;
        private int _tickLastTemporaryVisitEnd = 0;
        private bool _isReadyForNextVisit = false;

        public int TickLastNostalgia { get { return _tickLastNostalgia; } set { _tickLastNostalgia = value; } }
        public int TickLastTemporaryVisitStart { get { return _tickLastTemporaryVisitStart; } set { _tickLastTemporaryVisitStart = value; } }
        public int TickLastTemporaryVisitEnd { get { return _tickLastTemporaryVisitEnd; } set { _tickLastTemporaryVisitEnd = value; } }


        public TemporaryStay(World world) : base(world)
        {

        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if(!IsInitialized())
            {
                //在未初始化状态（通常因为无永久联盟，或有了永久联盟后尚未启动），该对象不做任何事。必须人为启动初始化才开始运作。
                return;
            }

            if (GenTicks.TicksAbs % (GenDate.TicksPerHour * 3) == 0)
            {
                if(_isReadyForNextVisit && IsCurrentlyTimeForVisit())
                {
                    //地图上生成小人，开始访问
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
                        ResetAll();
                        return;
                    }
                    _isReadyForNextVisit = false;
                    Faction WithFaction = permanentAlliance.WithFaction;
                    Pawn playerBetrothed = permanentAlliance.PlayerBetrothed;
                    Pawn npcMarriageSeeker = permanentAlliance.NpcMarriageSeeker;

                    List<Pawn> couple = new List<Pawn> { playerBetrothed, npcMarriageSeeker };
                    List<Pawn> incidentPawns;
                    IntVec3 stageLog;
                    Utils.SpawnVIPAndIncidentPawns(map, WithFaction, couple, 0, PawnGroupKindDefOf.Combat, out incidentPawns, out stageLog);

                    playerBetrothed.SetFaction(Faction.OfPlayer);
                    npcMarriageSeeker.SetFaction(Faction.OfPlayer);

                    //TODO: 弹出小人抵达信件
                }
                else if (!IsCurrentlyTimeForVisit())
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
                        //TODO: 弹出告别信件
                    }
                }
            }
            if (GenTicks.TicksAbs % (GenDate.TicksPerDay * 1) == GenDate.HoursPerDay * 10)
            {
                //每天有一定几率得思乡病。
                if(TryGetNostalgia()) {
                    PlanNextVisit(Rand.Range(3, 7), Rand.Range(7, 15));
                }
            }
        }

        //根据上一次访问时间，有一定几率得思乡病。
        //可用来强制思乡病，以此初始化第一次访问。
        public bool TryGetNostalgia(bool forceNostalgiaIfNonExisting = false)
        {
            if (_tickLastNostalgia > 0)
            {
                //已有思乡病
                return false;
            }

            if(forceNostalgiaIfNonExisting)
            {
                _tickLastNostalgia = GenTicks.TicksAbs;
                return true;
            }

            if (IsCurrentlyTimeForVisit()) 
            {
                //目前正在访问中
                return false;
            }

            //在上一次访问结束后一段时间过后，便有可能得思乡病。几率随着时间延长而逐渐变高，直到判定成功。
            float daysSinceLastVisitEnd = (GenTicks.TicksAbs - _tickLastTemporaryVisitEnd) * 1.0f / GenDate.TicksPerDay;

            var minimumDaysForNostalgia = 15;//TODO:设置里调整
            if (daysSinceLastVisitEnd > minimumDaysForNostalgia) 
            {
                var chanceInitial = 10; //TODO:设置里调整 
                var chanceIncreasePerDay = 3; //TODO:设置里调整 （每天提高的概率百分比）
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

            //TODO: 弹出信件，提前通知来访。
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

        public void OnArrival()
        {
            _tickLastNostalgia = 0;
        }

        public void ResetAll()
        {
            _tickLastNostalgia = _tickLastTemporaryVisitStart = _tickLastTemporaryVisitEnd = 0;
            _isReadyForNextVisit = false;
        }

        public bool IsInitialized()
        {
            return !(_tickLastNostalgia == 0 && _tickLastTemporaryVisitStart == 0 && _tickLastTemporaryVisitEnd == 0 && _isReadyForNextVisit == false);
        }

        public void InitializeByForcingFirstNostalgia()
        {
            TryGetNostalgia(true);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref _tickLastNostalgia, "DMP_PermanentAlliance_TemporaryStay_TickLastNostalgia", 0);
            Scribe_Values.Look<int>(ref _tickLastTemporaryVisitStart, "DMP_PermanentAlliance_TemporaryStay_TickLastTemporaryVisitStart", 0);
            Scribe_Values.Look<int>(ref _tickLastTemporaryVisitEnd, "DMP_PermanentAlliance_TemporaryStay_TickLastTemporaryVisitEnd", 0);
            Scribe_Values.Look<bool>(ref _isReadyForNextVisit, "DMP_PermanentAlliance_TemporaryStay_IsReadyForNextVisit", false);
        }
    }
}
