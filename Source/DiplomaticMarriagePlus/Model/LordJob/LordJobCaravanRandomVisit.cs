using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace DiplomaticMarriagePlus.Model.LordJob
{
    internal class LordJobCaravanRandomVisit : LordJob_WaitForDurationThenExit
    {
        private bool _isConditionMetExit = false;
        public LordJobCaravanRandomVisit() : base()
        {
        }

        public LordJobCaravanRandomVisit(IntVec3 point, int durationTicks) : base(point, durationTicks)
        {
            
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            LordToil_DefendPoint lordToil_Defend = new LordToil_DefendPoint(point);
            stateGraph.AddToil(lordToil_Defend);
            stateGraph.StartingToil = lordToil_Defend;

            LordToil_ExitMap lordToil_ExitMap = new LordToil_ExitMap();
            stateGraph.AddToil(lordToil_ExitMap);

            Transition transition = new Transition(lordToil_Defend, lordToil_ExitMap);
            transition.AddTrigger(new Trigger_TicksPassedAfterConditionMet(durationTicks, IsConditionMetExit));
            
            var transitionActionGiveGift = new TransitionAction_GiveGift();
            transitionActionGiveGift.gifts = new List<Thing>();
            Thing silver = new Thing();
            silver.def = ThingDefOf.Silver;
            silver.stackCount = Rand.Range(800, 1200);
            Thing gold = new Thing();
            gold.def = ThingDefOf.Gold;
            gold.stackCount = Rand.Range(200, 300);
            transitionActionGiveGift.gifts.Add(silver);
            transitionActionGiveGift.gifts.Add(gold);
            transition.AddPostAction(transitionActionGiveGift);

            transition.AddPostAction(new TransitionAction_EndAllJobs());
            stateGraph.AddTransition(transition);
            return stateGraph;
        }

        public bool IsConditionMetExit()
        {
            return _isConditionMetExit;
        }

        public void SetIsConditionMetExit(bool isConditionMetExit)
        {
            _isConditionMetExit = isConditionMetExit;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _isConditionMetExit, "DMP_PermanentAlliance_LordJobCaravanRandomVisit_IsConditionMetExit");
        }
    }
}
