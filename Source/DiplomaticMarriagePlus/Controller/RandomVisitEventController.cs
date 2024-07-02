using System.Collections.Generic;
using System.Linq;
using DiplomaticMarriagePlus.Global;
using DiplomaticMarriagePlus.Model;
using DiplomaticMarriagePlus.Model.LordJob;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace DiplomaticMarriagePlus.Controller
{
    internal class RandomVisitEventController : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            PermanentAlliance permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
            if(permanentAlliance == null || permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
            {
                Log.Message("[DMP] Random visit event aborted: No permanent alliance");
                //只有永久同盟生效时才可能启动该事件
                return false;
            }

            if (permanentAlliance.NpcMarriageSeeker.Map != null || permanentAlliance.PlayerBetrothed.Map != null)
            {
                Log.Message("[DMP] Random visit event aborted: At least one of the couple is on player's colony map");
                //只有二人都不在小地图时才能触发。
                return false;
            }

            TemporaryStay temporaryStay = Find.World.GetComponent<TemporaryStay>();
            if(temporaryStay.IsCurrentlyOnVisit)
            {
                Log.Message("[DMP] Random visit event aborted: The couples are currently on visit of player colony.");
                //小人在回到玩家殖民地暂住期间无法触发。
                return false;
            }

            Map map = Utils.GetPlayerMainColonyMap();
            if(map == null) 
            {
                Log.Message("[DMP] Random visit event aborted: The player doesnt have a valid non-SOS2 colony");
                //玩家没有非SOS2主基地
                return false;
            }

            if (GenHostility.AnyHostileActiveThreatToPlayer(map))
            {
                Log.Message("[DMP] Random visit event aborted: The player colony has hostile raiders");
                //地图上有敌人时无法触发
                return false;
            }

            Faction WithFaction = permanentAlliance.WithFaction;
            Pawn playerBetrothed = permanentAlliance.PlayerBetrothed;
            Pawn npcMarriageSeeker = permanentAlliance.NpcMarriageSeeker;

            //生成NPC商队，并且联姻的两个小人也在其中。
            List<Pawn> couple = new List<Pawn>();
            couple.Add(permanentAlliance.PlayerBetrothed);
            couple.Add(permanentAlliance.NpcMarriageSeeker);
            List<Pawn> incidentPawns;
            IntVec3 stageLoc;
            Utils.SpawnVIPAndIncidentPawns(map, WithFaction, couple, Utils.GetRandomThreatPointsByPlayerWealth(map, 20), PawnGroupKindDefOf.Trader, out incidentPawns, out stageLoc);
            List<Pawn> allCaravanPawns = incidentPawns.Concat(couple).ToList();

            IntVec3 caravanTargetLoc;

            //寻找地图中央附近的封闭区域
            if (!CellFinder.TryRandomClosewalkCellNear(map.Center, map, 20, out caravanTargetLoc))
            {
                //如果失败，就寻找地图中央附近任何可以落脚的地方
                caravanTargetLoc = CellFinder.StandableCellNear(map.Center, map, 10);
            }

            //抵达地点，等待战斗，结束后停留一段时间然后离开
            var lordJobCaravan = new LordJobCaravanRandomVisit(
                caravanTargetLoc, 
                GenDate.HoursPerDay * Rand.Range(6, 12) //在战斗结束后再等这么长时间才离开
                );
            var lordCaravan = LordMaker.MakeNewLord(WithFaction, lordJobCaravan, map, allCaravanPawns);

            //寻找和玩家及联姻结盟派系同时敌对的第三方派系。
            Faction randomHostileFaction;
            List<Faction> enemyFactions = (from x in Find.FactionManager.AllFactions
                                           where !x.IsPlayer && x.GetUniqueLoadID() != WithFaction.GetUniqueLoadID()
                                                && !x.defeated
                                                && !x.Hidden
                                                && x.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Hostile
                                                && x.RelationKindWith(WithFaction) == FactionRelationKind.Hostile
                                           select x).ToList();
            if (!enemyFactions.TryRandomElement(out randomHostileFaction))
            {
                //随机抽取第三方非隐藏派系，如果找不到就选机械族。
                randomHostileFaction = Faction.OfMechanoids;
            }

            //弹出信件
            var textVocabularyPapaOrMama = 
                ("DMP_PermanentAllianceEventRandomVocabulary_"
                + (permanentAlliance.PlayerFactionLeader.gender == Gender.Male ? "Father" : "Mother")
                ).Translate();
            var letter = LetterMaker.MakeLetter(
                label: "DMP_PermanentAllianceEventRandomVisitCaravanArriveTitle".Translate().CapitalizeFirst(),
                text: "DMP_PermanentAllianceEventRandomVisitCaravanArrive".Translate(
                    textVocabularyPapaOrMama, 
                    playerBetrothed.Label, 
                    npcMarriageSeeker.Label,
                    WithFaction.Name,
                    randomHostileFaction.Name
                    ).CapitalizeFirst(),
                def: LetterDefOf.ThreatBig,
                relatedFaction: WithFaction
            );
            Find.LetterStack.ReceiveLetter(@let: letter);

            //和玩家以及该NPC派系同时敌对的第三方随机派系军队在X小时后出现在小地图上，触发战斗。
            var randomVisitCaravanAttack = Find.World.GetComponent<RandomVisitAllyCaravanRefugeAttack>();
            randomVisitCaravanAttack.TickTriggerNext = GenTicks.TicksAbs + (GenDate.TicksPerHour * Rand.Range(3, 6));
            randomVisitCaravanAttack.HostileFactionTriggerNext = randomHostileFaction;
            randomVisitCaravanAttack.MapTriggerNext = map;
            randomVisitCaravanAttack.LordCaravan = lordCaravan;
            randomVisitCaravanAttack.IsEventStartedFlag = true;

            return false;
        }
    }
}
