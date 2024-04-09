﻿using System;
using System.Linq;
using DiplomaticMarriagePlus.Global;
using RimWorld;
using RimWorld.Planet;
using Verse;
namespace DiplomaticMarriagePlus.Model
{
    public class PermanentAlliance : WorldComponent, ILoadReferenceable
    {
        private Faction _withFaction;
        private Pawn _playerFactionLeader;
        private Pawn _playerBetrothed;
        private Pawn _npcMarriageSeeker;

        private static int tickCount = GenTicks.TicksAbs;

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
            if (PlayerBetrothed.GetFather() != PlayerFactionLeader && PlayerBetrothed.GetMother() != PlayerFactionLeader)
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

            //各种和永久联盟相关的随机小事件，都为每隔一段时间后有一定几率触发
            if (tickCount % (GenDate.TicksPerDay * 1) == 0)
            {
                //增加和NPC阵营的友好度，也有一定几率增加社交技能
                GoodwillIncrease();
                SocialSkillIncrease();
            }
            if (tickCount % (GenDate.TicksPerDay * 3) == 0)
            {
                //判定是否转化联姻NPC派系
                PlayerIdeologySpread();
            }
        }

        public void ForceUpdatePermanentAllianceStatus()
        {
            //检查永久同盟是否依然有效
            Validity validity = IsValid();

            Log.Message("DMP: Permanent Alliance Validity Check. Result code:" + validity);

            if (validity == Validity.INVALID_EMPTY)
            {
                return;
            }
            else if (validity == Validity.VALID)
            {
                //强制夫妇都属于NPC阵营，防止有些mod把两个关键小人弄到第三方阵营。（TODO: 做客期间除外）
                if (PlayerBetrothed.Faction != WithFaction)
                {
                    PlayerBetrothed.SetFaction(newFaction: WithFaction);
                }
                if (NpcMarriageSeeker.Faction != WithFaction)
                {
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
                        //如果此时小人在地图外，把小人生成到玩家可交易货币最多的地图（通常也是主殖民地）
                        Map map = TradeUtility.PlayerHomeMapWithMostLaunchableSilver();
                        Utils.SpawnOnePawn(map, PlayerBetrothed);
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

                Log.Message("DMP: Permanent Alliance with " + WithFaction.Name + " is no longer valid. Reason code: " + validity.ToString());

                //弹出信件信息，通知永久同盟终结。
                String text = "PermanentAllianceEventAllianceEnded_Reason_" + validity.ToString();
                var letter = LetterMaker.MakeLetter(
                        label: "PermanentAllianceEventAllianceEndedTitle".Translate().CapitalizeFirst(),
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
        //TODO: 下一任领袖如果是联姻的儿媳/女婿，如何让永久联盟更加巩固？（派出商队的频率翻倍？好感度锁定100？）
        //TODO: 如果盟友是帝国阵营该怎么办？（需要考虑VE Empire之类的mod）
        private void PermanentAllySuccessionWar()
        {

        }

        //双方小人在联姻的NPC阵营内部随机传播玩家的文化，如果二人社交技能足够高，有一定概率直接转化整个阵营。
        //只有在二人不在殖民地地图上时才能触发
        private void PlayerIdeologySpread()
        {
            if (IsValid() == Validity.VALID 
                && PlayerBetrothed.Map == null && NpcMarriageSeeker.Map == null 
                && WithFaction.ideos.PrimaryIdeo.id != PlayerFactionLeader.Ideo.id)
            {
                int playerBetrothedSocialSkill = PlayerBetrothed.skills.GetSkill(SkillDefOf.Social).GetLevel();
                int npcMarriageSeekerSocialSkill = NpcMarriageSeeker.skills.GetSkill(SkillDefOf.Social).GetLevel();

                if ((playerBetrothedSocialSkill + npcMarriageSeekerSocialSkill) * 15 <= Rand.Range(1, 10000))
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
                        label: "PermanentAllianceEventFactionConversionTitle".Translate().CapitalizeFirst(), 
                        text: "PermanentAllianceEventFactionConversion".Translate(WithFaction.Name, PlayerBetrothed.Label, NpcMarriageSeeker.Label).CapitalizeFirst(),
                        def: LetterDefOf.PositiveEvent,
                        relatedFaction: WithFaction
                        );
                    Find.LetterStack.ReceiveLetter(@let: letter);
                }
            }
            return;
        }

        //TODO:双方小人离婚事件随机判定，通常判定成功概率为0。但如果二人互相好感过低，我方和NPC阵营关系过低（即使依然大于0），且我方小人社交技能太低，则有判定成功的风险。
        private bool DivorceCheck()
        {
            //TODO:考虑平衡性因素暂时不实现
            return false;
        }

        //TODO:双方小人遇险求助任务。
        private void HelpQuest()
        {

        }

        //TODO: 在婚后不管是MOD制造的事件还是原版事件，如果两个VIP小人的任何一个进入地图时都会弹出警报，以免玩家忽略了保护他们。警报最多每天一次。
        private void VIPOnTheMapWarning()
        {

        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look<Pawn>(ref _playerFactionLeader, "DMP_PermanentAlliance_PlayerFactionLeader", false);
            Scribe_References.Look<Pawn>(ref _playerBetrothed, "DMP_PermanentAlliance_PlayerBetrothed", false);
            Scribe_References.Look<Pawn>(ref _npcMarriageSeeker, "DMP_PermanentAlliance_NpcMarriageSeeker", false);
            Scribe_References.Look<Faction>(ref _withFaction, "DMP_PermanentAlliance_WithFaction", false);
        }

        public void Invalidate()
        {
            WithFaction = null;
            PlayerFactionLeader = null;
            PlayerBetrothed = null;
            NpcMarriageSeeker = null;
        }
    }
}