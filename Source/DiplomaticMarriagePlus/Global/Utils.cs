﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
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
            int skillSocialLevel = skillSocial.GetLevel();
            //TODO: Maybe consider passion as well?
            return 5 + skillSocialLevel;
        }

        //找出玩家派系领袖。如果玩家派系没有领袖（通常都是这种情况），则返回玩家文化领袖。如果都找不到则返回NULL。
        public static Pawn GetPlayerFactionLeader()
        {
            Pawn playerFactionLeader = Faction.OfPlayer.leader;
            if (playerFactionLeader == null)
            {
                foreach (Precept_Role role in Faction.OfPlayer.ideos.PrimaryIdeo.RolesListForReading)
                {
                    if (role.def.leaderRole)
                    {
                        playerFactionLeader = role.ChosenPawnSingle();
                        return playerFactionLeader;
                    }
                }
                    
                return null;
            }
            else
            {
                return playerFactionLeader;
            }

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

        public static void SpawnOnePawn(Map map, Pawn pawn)
        {
            var stageLoc = RCellFinder.FindSiegePositionFrom(map.Center, map);
            IntVec3 loc = CellFinder.RandomClosewalkCellNear(stageLoc, map, 6);
            loc = CellFinder.RandomClosewalkCellNear(stageLoc, map, 6);
            var spawnRotation = Rot4.FromAngleFlat((map.Center - stageLoc).AngleFlat);
            GenSpawn.Spawn(pawn, loc, map, spawnRotation);
        }

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
            stageLoc = CellFinder.RandomEdgeCell(map);
            //stageLoc = RCellFinder.FindSiegePositionFrom(map.Center, map);

            //把VIP小人放到地图上
            var spawnRotation = Rot4.FromAngleFlat((map.Center - stageLoc).AngleFlat);
            if(vipPawns != null)
            {
                foreach (Pawn vipPawn in vipPawns)
                {
                    GenSpawn.Spawn(vipPawn, stageLoc, map, spawnRotation);
                }
            }

            //再随机生成些事件小人当VIP的随从。
            incidentPawns = Utils.GenerateIncidentPawns(incidentPawnsTotalThreat, faction, map, pawnGroupKindDefOf);
            foreach (Pawn incidentPawn in incidentPawns)
            {
                IntVec3 loc = CellFinder.RandomClosewalkCellNear(stageLoc, map, 6);
                GenSpawn.Spawn(incidentPawn, loc, map, spawnRotation);
            }
        }

        //根据玩家目前的总财富值，用叙事者的算法给出一个随机但相对合理的威胁点数。
        public static int GetRandomThreatPointsByPlayerWealth(
            Map map,
            //该数值决定了威胁点数为wiki基准估算的多少倍，数值越大也越多。1代表wiki基准值的1%，200代表200%
            int factorPercentage 
            )
        {
            float threatAvg = StorytellerUtility.DefaultThreatPointsNow(map);

            //实际威胁值在基础值附近随机浮动，最多不能超过10000
            float threat = Rand.RangeInclusive(75, 150) * threatAvg * (factorPercentage / 100.0f);
            return Math.Min((int)threat, 10000);
        }
    }
}