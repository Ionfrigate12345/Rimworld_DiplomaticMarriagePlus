using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using RimWorld;
using Verse;
using DiplomaticMarriagePlus.Global;
using DiplomaticMarriagePlus.ViewController;
using DiplomaticMarriagePlus.Model;

namespace DiplomaticMarriagePlus.Controller
{
    internal class ProposalEventController : IncidentWorker
    {
        private Pawn playerFactionLeader;
        private Pawn playerBetrothed;
        private Pawn npcMarriageSeeker;
        private const int TimeoutTicks = GenDate.TicksPerDay * 3;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms: parms)
                && CanFireDMPNow()
            ;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireDMPNow())
            {
                return false;
            }

            int goodwillIncreasePerDay = Utils.DMPGoodwillIncreasePerDay(playerBetrothed);

            var text = new TaggedString("DMP_DiplomaticMarriagePlusProposal".Translate(npcMarriageSeeker.Faction.Name, playerBetrothed.Label, npcMarriageSeeker.Label, goodwillIncreasePerDay).AdjustedFor(p: npcMarriageSeeker));

            PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref text, npcMarriageSeeker);

            ChoiceLetter_DMPProposalViewController choiceLetterDMP = (ChoiceLetter_DMPProposalViewController)LetterMaker.MakeLetter(label: this.def.letterLabel, text: text, def: this.def.letterDef);
            choiceLetterDMP.title = "DMP_DiplomaticMarriagePlusProposalTitle".Translate(playerBetrothed.LabelShort).CapitalizeFirst();
            choiceLetterDMP.radioMode = false;
            choiceLetterDMP.NpcMarriageSeeker = npcMarriageSeeker;
            choiceLetterDMP.PlayerBetrothed = playerBetrothed;
            choiceLetterDMP.PlayerFactionLeader = playerFactionLeader;
            choiceLetterDMP.StartTimeout(duration: TimeoutTicks);
            Find.LetterStack.ReceiveLetter(@let: choiceLetterDMP);

            return true;
        }

        private bool CanFireDMPNow()
        {
            //TODO: 后面版本是否同时允许多个永久同盟？
            PermanentAlliance permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
            if (permanentAlliance != null && permanentAlliance.IsValid() == PermanentAlliance.Validity.VALID)
            {
                Log.Message(text: "[DMP] Maximum permanent alliance reached. Proposal aborted.");
                return false;
            }
            playerFactionLeader = Utils.GetPlayerFactionLeader();
            if (playerFactionLeader == null)
            {
                //玩家派系没有文化领袖
                Log.Message(text: "[DMP] Player faction has no leader or ideology leader. Proposal aborted.");
                return false;
            }
            if (Utils.GetPlayerMainColonyMap() == null)
            {
                //玩家没有小地图
                Log.Message(text: "[DMP] Player faction has no available colony map. Proposal aborted.");
                return false;
            }
            if (!TryFindBetrothed(out playerBetrothed))
            {
                //玩家派系的文化领袖没有成年未婚，且此刻和派系领袖处于同一小地图上的子女
                Log.Message(text: "[DMP] No player betrothed available. Proposal aborted.");
                return false;
            }
            if (!FindOrSpawnMarriageSeeker(out npcMarriageSeeker, playerBetrothed))
            {
                //没有合适的NPC派系求婚者
                Log.Message(text: "[DMP] No npcMarriageSeeker available. The player might not have a suitable allied faction.");
                return false;
            }
            return true;
        }

        private bool TryFindBetrothed(out Pawn betrothed)
        {
            betrothed = null;

            List<Pawn> betrothedCandidates = FindAllBetrothedCandidates();

            if (betrothedCandidates.Count > 0)
            {
                betrothedCandidates.TryRandomElement(out betrothed);
                return true;
            }

            return false;
        }

        private bool FindOrSpawnMarriageSeeker(out Pawn npcMarriageSeeker, Pawn playerBetrothed)
        {
            //候选者的（生物）年龄限制：男方比女方大1-5岁，最低不能低于18岁。
            int candidateMinAge = (playerBetrothed.gender == Gender.Female) ? (playerBetrothed.ageTracker.AgeBiologicalYears + 1) : Math.Max(18, playerBetrothed.ageTracker.AgeBiologicalYears - 5);
            int candidateMaxAge = (playerBetrothed.gender == Gender.Female) ? (playerBetrothed.ageTracker.AgeBiologicalYears + 5) : Math.Max(18, playerBetrothed.ageTracker.AgeBiologicalYears - 1);

            int maxIncestChance = 40; //男女双方近亲率超过该值则视为乱伦而剔除该候选者。

            npcMarriageSeeker = null;

            List<Pawn> marriageSeekerCandidates = FindAllMarriageSeekerCandidates();
            List<Pawn> marriageSeekerCandidatesQualified = new List<Pawn>();

            foreach (Pawn marriageSeekerCandidate in marriageSeekerCandidates)
            {
                //剔除更多不合条件的求婚者
                bool removeCandidate = false;

                if (marriageSeekerCandidate.gender == Gender.None 
                    || marriageSeekerCandidate.gender.Equals(playerBetrothed.gender)
                )
                {
                    removeCandidate = true;
                }

                PawnRelationDef relation;
                if (PregnancyUtility.InbredChanceFromParents(
                    marriageSeekerCandidate.gender == Gender.Female ? marriageSeekerCandidate : playerBetrothed,
                    marriageSeekerCandidate.gender == Gender.Female ? playerBetrothed : marriageSeekerCandidate, 
                    out relation) >= maxIncestChance) 
                {
                    removeCandidate = true;
                }

                if (marriageSeekerCandidate.ageTracker.AgeBiologicalYears > candidateMaxAge || marriageSeekerCandidate.ageTracker.AgeBiologicalYears < candidateMinAge)
                {
                    removeCandidate = true;
                }

                if(!removeCandidate)
                {
                    marriageSeekerCandidatesQualified.Add(marriageSeekerCandidate);
                }
            }

            //从候选者中随机选择求婚者。
            if(marriageSeekerCandidatesQualified.Count > 0)
            {
                marriageSeekerCandidatesQualified.TryRandomElement(out npcMarriageSeeker);
                return true;
            }

            //如果没有符合条件者的求婚者，则随机选一盟友派系，然后生成一个各方面相对合适的求婚者。
            List<Faction> allyFactions = (from x in Find.FactionManager.AllFactions
                                          where !x.def.hidden
                                               && !x.def.permanentEnemy
                                               && !x.IsPlayer
                                               && !x.defeated
                                               && x.leader != null
                                               && x.leader.RaceProps.Humanlike
                                               && !x.leader.IsPrisoner
                                               && x.GoodwillWith(Faction.OfPlayer) >= 75
                                               && x.AllyOrNeutralTo(Faction.OfPlayer)
                                               && Utils.GetFactionTotalSettlementCount(x) > 0 //不是某些mod生成的弱小派系（必须有至少一个正规殖民地）
                                          select x).ToList();
            if(allyFactions.Count > 0)
            {
                Faction allyFactionRandom;
                allyFactions.TryRandomElement(out allyFactionRandom);
                npcMarriageSeeker = Utils.GenerateOnePawn(allyFactionRandom,
                    candidateMinAge,
                    candidateMaxAge,
                    null,
                    (playerBetrothed.gender == Gender.Female) ? Gender.Male : Gender.Female
                    );
                return true;
            }

            //如果没有盟友派系，则该事件无法触发。
            return false;
        }

        private List<Pawn> FindAllBetrothedCandidates()
        {
           return (from x in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonistsAndPrisoners_NoCryptosleep
                                         where !LovePartnerRelationUtility.HasAnyLovePartner(x)
                                         && x.Faction == Faction.OfPlayer
                                         && !x.IsPrisoner
                                         && x.ageTracker.AgeBiologicalYears >= 18
                                         && ( //被求婚者必须是玩家派系文化领袖的孩子
                                            (x.GetFather() != null && x.GetFather().thingIDNumber == playerFactionLeader.thingIDNumber)
                                            || (x.GetMother() != null && x.GetMother().thingIDNumber == playerFactionLeader.thingIDNumber)
                                        //|| x.Faction.leader.thingIDNumber == x.thingIDNumber //TODO: The diplomarriage of player faction leader is abit complicated, maybe let NPC join player's colony?
                                            )
                                         && x.Map != null && x.Map == playerFactionLeader.Map //被求婚者必须和玩家派系领袖此刻处于同一张小地图，且都能正常活动（不在休眠等状态）
                   select x).ToList();
        }

        private List<Pawn> FindAllMarriageSeekerCandidates()
        {
            return (from x in Find.WorldPawns.AllPawnsAlive
                 where x.Faction != null 
                 && !x.Faction.def.hidden 
                 && !x.Faction.def.permanentEnemy 
                 && !x.Faction.IsPlayer
                 && !x.Faction.defeated
                 && x.Faction.leader != null
                 && x.Faction.AllyOrNeutralTo(Faction.OfPlayer)
                 && x.Faction.GoodwillWith(Faction.OfPlayer) >= 75
                 && !x.IsPrisoner
                 && !x.IsSlave
                 && !x.Spawned
                 && x.relations != null 
                 && x.RaceProps.Humanlike
                 && x != x.Faction.leader
                 && Utils.GetFactionTotalSettlementCount(x.Faction) > 0 //不是某些mod生成的弱小派系（必须有至少一个正规殖民地）
                 && !SettlementUtility.IsPlayerAttackingAnySettlementOf(faction: x.Faction)
                 && x.ageTracker.AgeBiologicalYears >= 18
                 && !LovePartnerRelationUtility.HasAnyLovePartner(pawn: x)
            select x).ToList();
        }
    }
}
