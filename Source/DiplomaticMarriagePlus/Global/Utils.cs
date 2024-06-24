using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DiplomaticMarriagePlus.Model;
using DiplomaticMarriagePlus.View;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Noise;

namespace DiplomaticMarriagePlus.Global
{
    internal class Utils
    {
        //永久联盟生效期间，基于玩家阵营结婚者的社交技能，每天给对方阵营提高多少关系。
        public static int DMPGoodwillIncreasePerDay(Pawn playerBetrothed)
        {
            var skillSocial = playerBetrothed.skills.GetSkill(SkillDefOf.Social);
            int skillSocialLevel = skillSocial.GetUnclampedLevel();
            //TODO: 是否也考虑小人的兴趣？
            return (int)(DMPModWindow.Instance.settings.goodwillDailyIncreaseBaseValue 
                + skillSocialLevel * DMPModWindow.Instance.settings.goodwillDailyIncreaseSocialSkillFactor
                );
        }

        //找出玩家派系领袖（文化领袖）。如果找不到则返回NULL。
        public static Pawn GetPlayerFactionLeader()
        {
            foreach (Precept_Role role in Faction.OfPlayer.ideos.PrimaryIdeo.RolesListForReading)
            {
                if (role.def.leaderRole)
                {
                    return role.ChosenPawnSingle();
                }
            }
            return null;
        }

        public static int GetFactionTotalSettlementCount(Faction faction)
        {
            var settlementsForFaction = Find.WorldObjects.Settlements.Where(s => s.Faction == faction).ToList();
            return settlementsForFaction.Count;
        }

        //生成单个小人。Null参数代表该属性随机，或默认值
        public static Pawn GenerateOnePawn(Faction faction, 
            int minAge, 
            int maxAge, 
            ThingDef race = null, 
            Gender? gender = null
        )
        {
            PawnKindDef pawnKindDef = faction.RandomPawnKind();
            pawnKindDef.minGenerationAge = minAge;
            pawnKindDef.maxGenerationAge = maxAge;
            if (race != null)
            {
                pawnKindDef.race = race;
            }
            if(gender != null)
            {
                pawnKindDef.fixedGender = gender;
            }
            return PawnGenerator.GeneratePawn(pawnKindDef, faction);
        }

        //生成多个事件小人
        public static List<Pawn> GenerateIncidentPawns(
            float threatPoints,
            Faction faction,
            IIncidentTarget target,
            PawnGroupKindDef pawnGroupKindDefOf
            )
        {
            var incidentParms = new IncidentParms { points = threatPoints, faction = faction, target = target };
            var pawnGroupMakerParms =
                IncidentParmsUtility.GetDefaultPawnGroupMakerParms(pawnGroupKindDefOf, incidentParms);
            return PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
        }

        public static void SpawnOnePawn(Map map, Pawn pawn, IntVec3 stageLoc)
        {
            if (stageLoc == IntVec3.Invalid && !RCellFinder.TryFindRandomPawnEntryCell(out stageLoc, map, CellFinder.EdgeRoadChance_Neutral))
            {
                stageLoc = RCellFinder.FindSiegePositionFrom(map.Center, map);
            }
            IntVec3 loc = CellFinder.RandomClosewalkCellNear(stageLoc, map, 6);
            var spawnRotation = Rot4.FromAngleFlat((map.Center - stageLoc).AngleFlat);
            GenSpawn.Spawn(pawn, loc, map, spawnRotation);
        }

        //生成小人。vipPawns是必定会出现的小人，而incidentPawns则是根据给定的威胁点数随机生成
        public static void SpawnVIPAndIncidentPawns (
            Map map, 
            Faction faction, 
            List<Pawn> vipPawns, 
            int incidentPawnsTotalThreat, 
            PawnGroupKindDef pawnGroupKindDefOf, 
            out List<Pawn> incidentPawns, 
            out IntVec3 stageLoc
            )
        {
            if (!RCellFinder.TryFindRandomPawnEntryCell(out stageLoc, map, CellFinder.EdgeRoadChance_Neutral))
            {
                stageLoc = CellFinder.RandomEdgeCell(map);
            }
            //stageLoc = RCellFinder.FindSiegePositionFrom(map.Center, map);

            var spawnRotation = Rot4.FromAngleFlat((map.Center - stageLoc).AngleFlat);
            
            //把VIP小人放到地图上
            if (vipPawns != null)
            {
                foreach (Pawn vipPawn in vipPawns)
                {
                    IntVec3 loc = CellFinder.RandomClosewalkCellNear(stageLoc, map, 6);
                    GenSpawn.Spawn(vipPawn, loc, map, spawnRotation);
                }
            }

            if(incidentPawnsTotalThreat > 0)
            {
                //再随机生成些事件小人。
                incidentPawns = Utils.GenerateIncidentPawns(incidentPawnsTotalThreat, faction, map, pawnGroupKindDefOf);
                foreach (Pawn incidentPawn in incidentPawns)
                {
                    IntVec3 loc = CellFinder.RandomClosewalkCellNear(stageLoc, map, 6);
                    GenSpawn.Spawn(incidentPawn, loc, map, spawnRotation);
                }
            }
            else
            {
                incidentPawns = new List<Pawn>();
            }
        }

        //根据玩家目前的总财富值，以叙事者的算法为基准给出一个随机但相对合理的威胁点数。
        public static int GetRandomThreatPointsByPlayerWealth(
            Map map,
            //该数值决定了威胁点数为wiki基准估算的多少倍，数值越大也越多。100=100%。
            int factorPercentage 
            )
        {
            float threatAvg = StorytellerUtility.DefaultThreatPointsNow(map);

            //实际威胁值在基础值附近随机浮动，最多不能超过10000
            float threat = threatAvg 
                * (Rand.RangeInclusive(50, 150) / 100.0f) //叙事者基准值的50%-150%
                * (factorPercentage / 100.0f) //该数值为mod代码自用，让有些事件生成的NPC队伍强度比另一些更高。
                * DMPModWindow.Instance.settings.threatMultiplier //玩家mod设置，范围为0.5 - 5.0。
                ;
            return Math.Min((int)threat, 10000);
        }

        //判断该地图是否为SOS2的太空地图。
        public static bool IsSOS2SpaceMap(Map map)
        {
            var traverse = Traverse.Create(map);
            var isSpaceMethod = traverse.Method("IsSpace");
            if (isSpaceMethod.MethodExists() && (bool)isSpaceMethod.GetValue())
            {
                return true;
            }
            else if (map.Biome.defName.Contains("OuterSpace"))
            {
                return true;
            }
            return false;
        }
        public static bool IsRimNauts2SpaceMap(Map map)
        {
            return map.Biome.defName.StartsWith("RimNauts2_");
        }

        public static bool IsSOS2OrRimNauts2SpaceMap(Map map)
        {
            return IsSOS2SpaceMap(map) || IsRimNauts2SpaceMap(map);
        }

        //获取玩家财富值最高的地图。SOS2和Rimnauts2的太空地图会被排除。
        public static Map GetPlayerMainColonyMapSOS2Excluded()
        {
            var allPlayerHomes = (from x in Find.Maps
                                  where x.IsPlayerHome
                                  select x).ToList();

            var allNonSpaceMaps = new List<Map>();
            foreach (var map in allPlayerHomes)
            {
                if (IsSOS2OrRimNauts2SpaceMap(map) == false)
                {
                    allNonSpaceMaps.Add(map);
                }
            }

            if (!allNonSpaceMaps.Any())
            {
                return null;
            }

            return allNonSpaceMaps.OrderByDescending(map => map.PlayerWealthForStoryteller).First();
        }

        public static Map GetPlayerMainColonyMap()
        {
            var allPlayerHomes = (from x in Find.Maps
                                  where x.IsPlayerHome
                                  select x).ToList();

            return allPlayerHomes.OrderByDescending(map => map.PlayerWealthForStoryteller).First();
        }

        public static bool RunIncident(IncidentDef incidentDef)
        {
            var incidentParms = StorytellerUtility.DefaultParmsNow(incidentDef.category, Find.World);
            if (incidentDef.pointsScaleable)
            {
                var storytellerComp = Find.Storyteller.storytellerComps.First(comp =>
                    comp is StorytellerComp_OnOffCycle || comp is StorytellerComp_RandomMain);
                incidentParms = storytellerComp.GenerateParms(incidentDef.category, incidentParms.target);
            }

            return incidentDef.Worker.TryExecute(incidentParms);
        }
    }
}
