using RimWorld;
using Verse.AI.Group;
using Verse;

namespace DiplomaticMarriagePlus.Model.LordJob
{
    public class LordJobDefendMarriageLeave : LordJob_DefendPoint
    {
        private IntVec3 _point;
        private float? _wanderRadius;

        private Pawn playerBetrothed;
        private Pawn npcMarriageSeeker;

        public override bool IsCaravanSendable => true;
        public override bool AddFleeToil => false;

        public LordJobDefendMarriageLeave()
        {
        }

        public LordJobDefendMarriageLeave(IntVec3 point, Pawn playerBetrothed, Pawn npcMarriageSeeker, float? wanderRadius = null)
        {
            _point = point;
            this.playerBetrothed = playerBetrothed;
            this.npcMarriageSeeker = npcMarriageSeeker;
            _wanderRadius = wanderRadius;
        }

        // 创建状态机
        public override StateGraph CreateGraph()
        {
            //状态1：往指定地点集合。
            var stateGraph = new StateGraph();

            var lordToilDefendPoint = new LordToil_DefendPoint(_point, wanderRadius: _wanderRadius);
            stateGraph.AddToil(lordToilDefendPoint);

            var lordToilMarriageCeremony = new LordToil_MarriageCeremony(playerBetrothed, npcMarriageSeeker, _point);
            stateGraph.AddToil(lordToilMarriageCeremony);

            var lordToilLeaveMap = new LordToil_ExitMap();
            stateGraph.AddToil(lordToilLeaveMap);

            //添加流程：X小时后举行婚礼。
            var transition1 = new Transition(lordToilDefendPoint, lordToilMarriageCeremony);
            var triggerXHoursAfter1 = new Trigger_TicksPassed(GenDate.TicksPerHour * 3);
            transition1.AddTrigger(triggerXHoursAfter1);
            stateGraph.AddTransition(transition1);

            //添加流程：X小时后离开地图。
            var transition2 = new Transition(lordToilMarriageCeremony, lordToilLeaveMap);
            var triggerXHoursAfter2 = new Trigger_TicksPassed(GenDate.TicksPerHour * 5);
            transition2.AddTrigger(triggerXHoursAfter2);
            stateGraph.AddTransition(transition2);

            return stateGraph;
        }

        /// <summary>
        /// 序列化
        /// </summary>
        public override void ExposeData()
        {
            Scribe_Values.Look(ref _point, "DMP_PermanentAlliance_LordJobDefendMarriageLeave_point");
            Scribe_Values.Look(ref _wanderRadius, "DMP_PermanentAlliance_LordJobDefendMarriageLeave_wanderRadius");
            Scribe_References.Look<Pawn>(ref playerBetrothed, "DMP_PermanentAlliance_PlayerBetrothed", false);
            Scribe_References.Look<Pawn>(ref npcMarriageSeeker, "DMP_PermanentAlliance_NpcMarriageSeeker", false);
        }
    }
}
