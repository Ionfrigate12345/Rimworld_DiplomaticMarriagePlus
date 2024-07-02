using System;
using System.Collections.Generic;
using System.Linq;
using DiplomaticMarriagePlus.Global;
using DiplomaticMarriagePlus.View;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Verse.Noise;
namespace DiplomaticMarriagePlus.Model
{
    public class PermanentAlliance : WorldComponent, ILoadReferenceable
    {
        private Faction _withFaction;
        private Pawn _playerFactionLeader;
        private Pawn _playerBetrothed;
        private Pawn _npcMarriageSeeker;

        private static int tickCount = GenTicks.TicksAbs;
        private static int lastWarningVIPOnTheMap = 0;

        //上一次检测到使用A Petition For Provisions的贸易时间
        public static int LastAPFPTradeTicks = 0;

        public Faction WithFaction
        {
            get { return _withFaction; }
            set { _withFaction = value; }
        }

        public Pawn PlayerFactionLeader
        {
            get { return _playerFactionLeader; }
            set { _playerFactionLeader = value; }
        }

        public Pawn PlayerBetrothed
        {
            get { return _playerBetrothed; }
            set { _playerBetrothed = value; }
        }

        public Pawn NpcMarriageSeeker
        {
            get { return _npcMarriageSeeker; }
            set { _npcMarriageSeeker = value; }
        }

        public PermanentAlliance(World world) : base(world)
        {
        }

        public enum Validity { 
            VALID, //有效
            INVALID_EMPTY, //无效：双方人物和阵营信息不全
            INVALID_REASON_BOTH_DEATH, //无效：结婚双方均已死亡
            INVALID_REASON_PLAYER_BETROTHED_DEATH, //无效：我方小人死亡
            INVALID_REASON_NPC_MARRIAGE_SEEKER_DEATH, //无效：NPC方小人死亡
            INVALID_REASON_DIVORCE, //无效：离婚
            INVALID_REASON_GOODWILL_TOO_LOW, //无效：和NPC阵营友好度太低
            INVALID_REASON_FACTION_DEFEATED, //无效：NPC阵营已被摧毁
            INVALID_REASON_PLAYER_LEADER_NOT_PARENT_OF_BETHOTHED //无效：我方阵营领袖不再是联姻小人的父母。
        }

        //检查目前永久同盟是否还有效，如果无效则返回理由。
        public Validity IsValid ()
        {
            if(WithFaction == null || PlayerFactionLeader == null || PlayerBetrothed == null || NpcMarriageSeeker == null) 
            {
                return Validity.INVALID_EMPTY;
            }
            if (PlayerBetrothed.Dead && NpcMarriageSeeker.Dead)
            {
                return Validity.INVALID_REASON_BOTH_DEATH;
            }
            else if (PlayerBetrothed.Dead)
            {
                return Validity.INVALID_REASON_PLAYER_BETROTHED_DEATH;
            }
            else if (NpcMarriageSeeker.Dead)
            {
                return Validity.INVALID_REASON_NPC_MARRIAGE_SEEKER_DEATH;
            }

            //TODO：平衡性考虑，暂时不实现离婚判定功能
            /*if (!PlayerBetrothed.GetSpouses(false).Contains(NpcMarriageSeeker))
            {
                return Validity.INVALID_REASON_DIVORCE;
            }*/

            if (WithFaction.GoodwillWith(Faction.OfPlayer) < 0)
            {
                return Validity.INVALID_REASON_GOODWILL_TOO_LOW;
            }

            if(WithFaction.defeated)
            {
                return Validity.INVALID_REASON_FACTION_DEFEATED;
            }

            //更新玩家派系领袖
            PlayerFactionLeader = Utils.GetPlayerFactionLeader();
            if (PlayerFactionLeader == null 
                || PlayerFactionLeader.Dead
                || (PlayerBetrothed.GetFather() != PlayerFactionLeader && PlayerBetrothed.GetMother() != PlayerFactionLeader)
                )
            {
                return Validity.INVALID_REASON_PLAYER_LEADER_NOT_PARENT_OF_BETHOTHED;
            }

            return Validity.VALID;
        }

        public string GetUniqueLoadID()
        {
            return "DMP_PermanentAlliance";
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            tickCount = GenTicks.TicksAbs;

            //为了保证运行效率，有些更新每隔一定tick后才运行一次。
            if (tickCount % GenDate.TicksPerHour == 0)
            {
                ///每隔1小时更新一次永久同盟状态并试图触发各种事件和判定。
                ForceUpdatePermanentAllianceStatus();
            }
            if (tickCount % GenDate.TicksPerHour == 5) //同样每小时一次，但选个不同的tick触发
            {
                //在边缘城市地图的某些任务期间，如果永久同盟的队伍出现，则联姻小人必定出现（除非已经在别的小地图）
                TryForceVIPOnRimcitiesQuestMaps();
            }
            if (tickCount % 120 == 2) 
            {
                //检查关键小人是否在地图上，弹出警报，每天限一次。
                VIPOnTheMapWarning();
            }

            //各种和永久联盟相关的随机小事件，都为每隔一段时间后有一定几率触发
            if (tickCount % (GenDate.TicksPerDay * 1) == 0)
            {
                //增加和NPC阵营的友好度，也有一定几率增加社交技能
                GoodwillIncrease();
                SocialSkillIncrease();
            }
            if (tickCount % (GenDate.TicksPerDay * 1) == GenDate.TicksPerHour * 6)//同样每天一次，但选个不同的tick触发
            {
                //判定是否转化联姻NPC派系。和前一个事件错开6小时。
                PlayerIdeologySpread();
            }
        }

        public void ForceUpdatePermanentAllianceStatus()
        {
            //检查永久同盟是否依然有效
            Validity validity = IsValid();

            //Log.Message("[DMP] Permanent Alliance Validity Check. Result code:" + validity);

            if (validity == Validity.INVALID_EMPTY)
            {
                return;
            }
            else if (validity == Validity.VALID)
            {
                //强制夫妇都属于NPC阵营（除了回归玩家阵营暂住期间），这是为了防止两个关键小人被玩家通过其它mod或方式招募，或弄到第三方阵营。

                var temporaryStay = Find.World.GetComponent<TemporaryStay>();

                if (PlayerBetrothed.Faction != WithFaction
                    &&
                    (temporaryStay.IsRunning == false || temporaryStay.IsCurrentlyOnVisit == false)
                    )
                {
                    String text = "DMP_PermanentAllianceVIPUnexpectedFactionWarning";
                    var letter = LetterMaker.MakeLetter(
                            label: "DMP_PermanentAllianceVIPUnexpectedFactionWarningTitle".Translate().CapitalizeFirst(),
                            text: text.Translate(PlayerBetrothed.Label, WithFaction.Name, PlayerBetrothed.Faction.Name).CapitalizeFirst(),
                            def: LetterDefOf.NegativeEvent,
                            relatedFaction: WithFaction
                            );
                    Find.LetterStack.ReceiveLetter(@let: letter);
                    PlayerBetrothed.SetFaction(newFaction: WithFaction);
                }
                if (NpcMarriageSeeker.Faction != WithFaction
                    &&
                    (temporaryStay.IsRunning == false || temporaryStay.IsCurrentlyOnVisit == false)
                    )
                {
                    String text = "DMP_PermanentAllianceVIPUnexpectedFactionWarning";
                    var letter = LetterMaker.MakeLetter(
                            label: "DMP_PermanentAllianceVIPUnexpectedFactionWarningTitle".Translate().CapitalizeFirst(),
                            text: text.Translate(NpcMarriageSeeker.Label, WithFaction.Name, NpcMarriageSeeker.Faction.Name).CapitalizeFirst(),
                            def: LetterDefOf.NegativeEvent,
                            relatedFaction: WithFaction
                            );
                    Find.LetterStack.ReceiveLetter(@let: letter);
                    NpcMarriageSeeker.SetFaction(newFaction: WithFaction);
                }
            }
            else
            {

                if (validity == Validity.INVALID_REASON_DIVORCE
                    ||
                    validity == Validity.INVALID_REASON_NPC_MARRIAGE_SEEKER_DEATH
                    ||
                    validity == Validity.INVALID_REASON_GOODWILL_TOO_LOW)
                {
                    //如果永久同盟终结是因为离婚，NPC阵营那一方的配偶死亡，或好感度太低，可以召回我方小人。
                    PlayerBetrothed.SetFaction(Faction.OfPlayer);
                    if (PlayerBetrothed.Map == null)
                    {
                        Map map = Utils.GetPlayerMainColonyMap();
                        //如果此时小人在地图外，把小人生成到玩家主基地
                        Utils.SpawnOnePawn(map, PlayerBetrothed, IntVec3.Invalid);
                    }
                }
                else if (validity == Validity.INVALID_REASON_FACTION_DEFEATED)
                {
                    //两个小人都会死亡
                    PlayerBetrothed.health.SetDead();
                    NpcMarriageSeeker.health.SetDead();
                }
                else if (validity == Validity.INVALID_REASON_PLAYER_LEADER_NOT_PARENT_OF_BETHOTHED) {
                    //两个联姻小人都会留在NPC派系，NPC派系会继续做常规盟友，但永久同盟终结。（这种情况什么都不用做）

                    //TODO:此处有个类似CK的继承人争夺高级功能可以考虑：
                    //联姻的小人如果发现玩家派系领袖不再是自己父母，会借助NPC派系的势力，回来要求恢复自己父亲/母亲做领袖，或者如果二者都已死亡，自己做下一任领袖？（VE Ideology有些模因会自动改领袖，后果无法预料）
                    //只要二人在玩家殖民地，是否就能继续保持永久同盟？
                }

                Log.Message("[DMP] Permanent Alliance with " + WithFaction.Name + " is no longer valid. Reason code: " + validity.ToString());

                //弹出信件通知永久同盟终结。
                String text = "DMP_PermanentAllianceEventAllianceEnded_Reason_" + validity.ToString();
                var letter = LetterMaker.MakeLetter(
                        label: "DMP_PermanentAllianceEventAllianceEndedTitle".Translate().CapitalizeFirst(),
                        text: text.Translate(WithFaction.Name, PlayerBetrothed.Label, NpcMarriageSeeker.Label, PlayerFactionLeader.Label).CapitalizeFirst(),
                        def: LetterDefOf.NegativeEvent,
                        relatedFaction: WithFaction
                        );
                Find.LetterStack.ReceiveLetter(@let: letter);

                //移除永久同盟
                Invalidate();

                return;
            }
        }

        //我方小人根据社交技能，会借助婚姻不断增强配偶所在NPC阵营对我方的好感度，通常这个数值足以抵消自然衰减且长期维持在100。
        private void GoodwillIncrease()
        {
            if (IsValid() == Validity.VALID)
            {
                int goodWillToIncrease = Utils.DMPGoodwillIncreasePerDay(PlayerBetrothed);
                WithFaction.TryAffectGoodwillWith(other: Faction.OfPlayer, goodwillChange: goodWillToIncrease, canSendMessage: true, canSendHostilityLetter: true);
            }
            return;
        }

        //双方小人在联姻的NPC阵营内部随机传播玩家的文化，如果二人社交技能足够高，有一定概率直接转化整个阵营。
        //只有在二人不在殖民地地图上时才能触发
        private void PlayerIdeologySpread()
        {
            if (IsValid() == Validity.VALID 
                && PlayerBetrothed.Map == null && NpcMarriageSeeker.Map == null 
                && WithFaction.ideos.PrimaryIdeo.id != PlayerFactionLeader.Ideo.id)
            {
                int playerBetrothedSocialSkill = PlayerBetrothed.skills.GetSkill(SkillDefOf.Social).GetUnclampedLevel();
                int npcMarriageSeekerSocialSkill = NpcMarriageSeeker.skills.GetSkill(SkillDefOf.Social).GetUnclampedLevel();

                var allyConversionChance = (playerBetrothedSocialSkill + npcMarriageSeekerSocialSkill) * DMPModWindow.Instance.settings.factionConversionChancePerSocialSkill * 100;
                if (Rand.Range(0, 10000) <= allyConversionChance)
                {
                    WithFaction.ideos.SetPrimary(PlayerFactionLeader.Ideo);
                    WithFaction.leader.ideo.SetIdeo(PlayerFactionLeader.Ideo);

                    var allPawnsOfFaction = (from x in Find.WorldPawns.AllPawnsAlive
                                             where x.Faction == WithFaction 
                                             && x.thingIDNumber != NpcMarriageSeeker.thingIDNumber
                                             && x.thingIDNumber != WithFaction.leader.thingIDNumber
                                             select x).ToList();
                    foreach (Pawn pawnOfFaction in allPawnsOfFaction)
                    {
                        pawnOfFaction.ideo.SetIdeo(PlayerFactionLeader.Ideo);
                    }

                    var letter = LetterMaker.MakeLetter(
                        label: "DMP_PermanentAllianceEventFactionConversionTitle".Translate().CapitalizeFirst(), 
                        text: "DMP_PermanentAllianceEventFactionConversion".Translate(WithFaction.Name, PlayerBetrothed.Label, NpcMarriageSeeker.Label).CapitalizeFirst(),
                        def: LetterDefOf.PositiveEvent,
                        relatedFaction: WithFaction
                        );
                    Find.LetterStack.ReceiveLetter(@let: letter);
                }
            }
            return;
        }

        private void TryForceVIPOnRimcitiesQuestMaps()
        {
            //检测有边缘城市任务的边缘城市地图，并强制联姻小人在永久同盟给的任务时出现

            if (!ModsConfig.IsActive("cabbage.rimcities"))
            {
                //边缘城市没有开启时无法触发。
                return;
            }

            PermanentAlliance permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
            if (permanentAlliance == null || permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
            {
                //只有永久同盟生效时才会检测
                return;
            }

            if(permanentAlliance.PlayerBetrothed.Map != null && permanentAlliance.NpcMarriageSeeker.Map != null)
            {
                return;
            }

            var rimcitiesCityType = AccessTools.TypeByName("Cities.City");
            if(rimcitiesCityType == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities force VIP:  Cities.City.");
                return;
            }
            var rimcitiesCityFindQuestsMethod = rimcitiesCityType.GetMethod("FindQuests");
            if (rimcitiesCityFindQuestsMethod == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities force VIP:  (Cities.city) FindQuests.");
                return;
            }
            var rimcitiesQuestType = AccessTools.TypeByName("Cities.Quest");
            if (rimcitiesQuestType == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities force VIP:  Cities.Quest.");
                return;
            }
            var rimcitiesQuestAssaultType = AccessTools.TypeByName("Cities.Quest_Assault");
            if (rimcitiesQuestAssaultType == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities force VIP:  Cities.Quest_Assault.");
                return;
            }
            var rimcitiesQuestDefendType = AccessTools.TypeByName("Cities.Quest_Defend");
            if (rimcitiesQuestDefendType == null)
            {
                Log.Error("[DMP] Reflection failure for RimCities force VIP:  Cities.Quest_Defend.");
                return;
            }

            var rimcitiesSettlements = (from settlement in Find.WorldObjects.Settlements
                                        where settlement.def.defName.Equals("City_Faction")
                                        && settlement.HasMap
                                        select settlement).ToList();
            foreach(var rimcitiesSettlement in rimcitiesSettlements)
            {
                List<Pawn> permanentAllyPawnsOnMap = rimcitiesSettlement.Map.mapPawns.SpawnedPawnsInFaction(permanentAlliance.WithFaction)
                    .Where(p => !p.IsSlave && !p.IsPrisoner && !p.Dead && !p.Downed)
                    .ToList();

                if (permanentAllyPawnsOnMap.Count > 0)
                {
                    Pawn randomExistingPawn = permanentAllyPawnsOnMap.RandomElement();
                    IntVec3 spawnLoc = randomExistingPawn.Position;
                    if (permanentAlliance.PlayerBetrothed.Map == null)
                    {
                        Utils.SpawnOnePawn(rimcitiesSettlement.Map, permanentAlliance.PlayerBetrothed, spawnLoc);
                        randomExistingPawn.GetLord().AddPawn(permanentAlliance.PlayerBetrothed);
                    }
                    if (permanentAlliance.NpcMarriageSeeker.Map == null)
                    {
                        Utils.SpawnOnePawn(rimcitiesSettlement.Map, permanentAlliance.NpcMarriageSeeker, spawnLoc);
                        randomExistingPawn.GetLord().AddPawn(permanentAlliance.NpcMarriageSeeker);
                    }
                    return;
                }
            }
        }

        //在婚后不管是MOD制造的事件还是原版事件，如果两个VIP小人的任何一个进入地图时都会弹出警报，以免玩家忽略了保护他们。
        private void VIPOnTheMapWarning()
        {
            if (IsValid() != Validity.VALID)
            {
                return;
            }
            if (
                DMPModWindow.Instance.settings.warningVIPOnTheMap
                && GenTicks.TicksAbs > lastWarningVIPOnTheMap + GenDate.TicksPerDay //警报最多每天一次。
                && (
                    (PlayerBetrothed.Map != null && PlayerBetrothed.Faction != Faction.OfPlayer) 
                    || 
                    (NpcMarriageSeeker.Map != null && NpcMarriageSeeker.Faction != Faction.OfPlayer)
                    ) //2个关键小人至少有一个出现在小地图上，且不是玩家派系成员
                )
            {
                Pawn lookTargetVIP = null;
                if(PlayerBetrothed.Map != null)
                {
                    lookTargetVIP = PlayerBetrothed;
                }
                else if(NpcMarriageSeeker.Map != null)
                {
                    lookTargetVIP = NpcMarriageSeeker;
                }
                Log.Warning("Test Warning lookTargetVIP" + lookTargetVIP.Name);
                lastWarningVIPOnTheMap = GenTicks.TicksAbs;
                var letter = LetterMaker.MakeLetter(
                        label: "DMP_PermanentAllianceWarningVIPOnTheMapTitle".Translate().CapitalizeFirst(),
                        text: "DMP_PermanentAllianceWarningVIPOnTheMap".Translate(WithFaction.Name, PlayerBetrothed.Label, NpcMarriageSeeker.Label).CapitalizeFirst(),
                        def: LetterDefOf.PositiveEvent,
                        relatedFaction: WithFaction,
                        lookTargets: lookTargetVIP
                        );
                Find.LetterStack.ReceiveLetter(@let: letter);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look<Pawn>(ref _playerFactionLeader, "DMP_PermanentAlliance_PlayerFactionLeader", false);
            Scribe_References.Look<Pawn>(ref _playerBetrothed, "DMP_PermanentAlliance_PlayerBetrothed", false);
            Scribe_References.Look<Pawn>(ref _npcMarriageSeeker, "DMP_PermanentAlliance_NpcMarriageSeeker", false);
            Scribe_References.Look<Faction>(ref _withFaction, "DMP_PermanentAlliance_WithFaction", false);
            Scribe_Values.Look<int>(ref lastWarningVIPOnTheMap, "DMP_PermanentAlliance_LastWarningVIPOnTheMap", 0);
            Scribe_Values.Look<int>(ref LastAPFPTradeTicks, "DMP_PermanentAlliance_LastAPFPTradeTicks", 0);
        }

        public void Invalidate()
        {
            WithFaction = null;
            PlayerFactionLeader = null;
            PlayerBetrothed = null;
            NpcMarriageSeeker = null;

            //终止小人思乡病和回来定居的事件
            TemporaryStay temporaryStay = Find.World.GetComponent<TemporaryStay>();
            temporaryStay.IsRunning = false;
        }

        /**---------------------------暂不实现的功能---------------------------------*/
        //我方小人每天都有机会社交技能+1，以后改善关系效率越来越高，技能最高到20。
        private void SocialSkillIncrease()
        {
            //TODO:考虑平衡性因素暂时不实现
            return;
        }

        //TODO:联姻期间即使在地图外，这对小人是否可能随机怀孕生子？
        private void PregnancyAndBirth()
        {
            return;
        }

        //TODO: （高级功能）NPC阵营领袖死亡，爆发继承人战争。玩家可以帮助来自NPC阵营的儿媳/女婿赢得战争成为NPC阵营下一任领袖。
        //下一任领袖如果是联姻的儿媳/女婿，如何让永久联盟更加巩固？（派出商队的频率翻倍？好感度锁定100？）
        //如果盟友是帝国阵营该怎么办？（需要考虑VE Empire之类的mod）
        private void PermanentAllySuccessionWar()
        {

        }

        //TODO:双方小人离婚事件随机判定，通常判定成功概率为0。但如果二人互相好感过低，我方和NPC阵营关系过低（即使依然大于0），且我方小人社交技能太低，则有判定成功的风险。
        private bool DivorceCheck()
        {
            //TODO:考虑平衡性因素暂时不实现
            return false;
        }
    }
}
