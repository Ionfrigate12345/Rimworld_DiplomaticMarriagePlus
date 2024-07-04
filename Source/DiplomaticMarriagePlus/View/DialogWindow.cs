using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiplomaticMarriagePlus.Model;
using HarmonyLib;
using RimWorld;
using Verse;
using static System.Collections.Specialized.BitVector32;
using Verse.Noise;
using RimWorld.Planet;
using DiplomaticMarriagePlus.Global;
using System.Runtime.CompilerServices;
using Verse.AI.Group;
using static HarmonyLib.Code;

namespace DiplomaticMarriagePlus
{
    [HarmonyPatch(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor))]
    public class DialogWindow
    {
        [HarmonyPostfix]
        public static void AddOption(ref DiaNode __result, Pawn negotiator, Faction faction)
        {
            var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
            if (permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
            {
                return;
            }

            if(faction != permanentAlliance.WithFaction)
            {
                return;
            }

            //添加通讯台对话选项：请求援军联合攻击当前玩家正在攻击的敌对常规据点（非边缘城市据点）
            var jointAttackOption = AddJointAttackOption(permanentAlliance);
            __result.options.Insert(2, jointAttackOption);
        }

        private static DiaOption AddJointAttackOption(PermanentAlliance permanentAlliance)
        {
            string text = "DMP_PermanentAllianceJointAttackDialogTitle".Translate();

            var mapEnemySettlementUnderAttackList = Find.Maps.Where(
                map => map.Parent is Settlement
                && permanentAlliance.EnemySettlementsToBeTransferredPendingList.Where(s => s.Tile == map.Parent.Tile && s.Name == ((Settlement)map.Parent).Name).ToList().Count == 0 //不能已经在列表里
                && !map.Parent.def.defName.StartsWith("City_") //排除边缘城市据点
                && map.ParentFaction != null
                && map.ParentFaction.HostileTo(Faction.OfPlayer)
                && map.ParentFaction.HostileTo(permanentAlliance.WithFaction)
                && map.mapPawns.PawnsInFaction(Faction.OfPlayer).Count() > 0 //地图上至少要有一个玩家小人
            ).ToList();
            if (mapEnemySettlementUnderAttackList.Count == 0)
            {
                var jointAttackDialogOptionDisabled = new DiaOption(text);
                jointAttackDialogOptionDisabled.Disable("DMP_PermanentAllianceJointAttackDialogTitleDisabledNoAvailableTarget".Translate());
                return jointAttackDialogOptionDisabled;
            }

            var jointAttackMapTarget = mapEnemySettlementUnderAttackList.FirstOrDefault();
            Settlement targetSettlement = jointAttackMapTarget.Parent as Settlement;
            var jointAttackDialogOption = new DiaOption(text);
            jointAttackDialogOption.action = delegate
            {
                if (permanentAlliance.EnemySettlementsToBeTransferredPendingList.Where(s => s.Tile == targetSettlement.Tile && s.Name == targetSettlement.Name).ToList().Count == 0)
                {
                    //生成随机友军小人
                    List<Pawn> reinforcementPawns = Utils.GenerateIncidentPawns(Rand.RangeInclusive(1000, 3000), permanentAlliance.WithFaction, jointAttackMapTarget, PawnGroupKindDefOf.Combat);

                    //联姻夫妇必出，除非当前处于玩家阵营暂住，或者当前已经在某个地图上。
                    if (permanentAlliance.PlayerBetrothed.Faction == permanentAlliance.WithFaction
                        && permanentAlliance.PlayerBetrothed.Map == null
                        && !reinforcementPawns.Contains(permanentAlliance.PlayerBetrothed)
                    )
                    {
                        reinforcementPawns.Add(permanentAlliance.PlayerBetrothed);
                    }
                    if (permanentAlliance.NpcMarriageSeeker.Faction == permanentAlliance.WithFaction
                        && permanentAlliance.NpcMarriageSeeker.Map == null
                        && !reinforcementPawns.Contains(permanentAlliance.NpcMarriageSeeker))
                    {
                        reinforcementPawns.Add(permanentAlliance.NpcMarriageSeeker);
                    }

                    //设定为攻击敌基地
                    LordMaker.MakeNewLord(permanentAlliance.WithFaction, new LordJob_AssaultColony(), jointAttackMapTarget, reinforcementPawns);

                    //找到该地图上随机的玩家小人并空投到其附近
                    var randomPlayerPawn = jointAttackMapTarget.mapPawns.PawnsInFaction(Faction.OfPlayer).RandomElement();
                    DropPodUtility.DropThingsNear(randomPlayerPawn.Position, jointAttackMapTarget, reinforcementPawns, 100, false, false, false, true, true, permanentAlliance.WithFaction);

                    //保存该据点引用，以后如果发现该据点已废弃（战斗胜利），则改为归姻亲同盟阵营。
                    permanentAlliance.EnemySettlementsToBeTransferredPendingList.Add(new SettlementToBeTransferedPendingElement(targetSettlement.Tile, targetSettlement.Name, targetSettlement));
                    jointAttackDialogOption.disabled = true;

                    Messages.Message("DMP_PermanentAllianceJointAttackDialogOnTheWay".Translate(targetSettlement.Name), MessageTypeDefOf.PositiveEvent, false);
                }
            };

            return jointAttackDialogOption;
        }
    }
}
