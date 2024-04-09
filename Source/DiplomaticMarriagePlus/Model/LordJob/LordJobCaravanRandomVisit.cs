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
        private bool _isConditionMet = false;
        public LordJobCaravanRandomVisit() : base()
        {
        }

        public LordJobCaravanRandomVisit(IntVec3 point, int durationTicks) : base(point, durationTicks)
        {
            
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            LordToil_WanderClose lordToil_WanderClose = new LordToil_WanderClose(point);
            stateGraph.AddToil(lordToil_WanderClose);
            stateGraph.StartingToil = lordToil_WanderClose;
            LordToil_ExitMap lordToil_ExitMap = new LordToil_ExitMap();
            stateGraph.AddToil(lordToil_ExitMap);

            Transition transition = new Transition(lordToil_WanderClose, lordToil_ExitMap);
            transition.AddTrigger(new Trigger_TicksPassedAfterConditionMet(durationTicks, IsConditionMet));
            
            var transitionActionGiveGift = new TransitionAction_GiveGift();
            transitionActionGiveGift.gifts = new List<Thing>();
            Thing silver = new Thing();
            silver.def = ThingDefOf.Silver;
            silver.stackCount = Rand.Range(800, 1200);
            Thing gold = new Thing();
            gold.def = ThingDefOf.Gold;
            gold.stackCount = Rand.Range(200, 300);
            Thing hyperweave = new Thing();
            hyperweave.def = ThingDefOf.Hyperweave;
            hyperweave.stackCount = Rand.Range(100, 200);
            transitionActionGiveGift.gifts.Add(silver);
            transitionActionGiveGift.gifts.Add(gold);
            transitionActionGiveGift.gifts.Add(hyperweave);
            transition.AddPostAction(transitionActionGiveGift);

            transition.AddPostAction(new TransitionAction_EndAllJobs());
            stateGraph.AddTransition(transition);
            return stateGraph;
        }

        public bool IsConditionMet()
        {
            return _isConditionMet;
        }

        public void SetIsConditionMet(bool isConditionMet)
        {
            _isConditionMet = isConditionMet;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _isConditionMet, "DMP_PermanentAlliance_LordJobCaravanRandomVisit_IsConditionMet");
        }
    }
}
