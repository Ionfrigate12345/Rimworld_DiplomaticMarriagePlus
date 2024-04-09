using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DiplomaticMarriagePlus.ViewController
{
    using System;
    using DiplomaticMarriagePlus.Model;
    using DiplomaticMarriagePlus.Global;
    using DiplomaticMarriagePlus.Model.LordJob;
    using Verse.AI.Group;
    using Verse.Noise;

    public class ChoiceLetter_DMPProposalViewController : ChoiceLetter
    {
        public Pawn PlayerBetrothed { get; set; }
        public Pawn NpcMarriageSeeker { get; set; }
        public Pawn PlayerFactionLeader { get; set; }

        private PermanentAlliance permanentAlliance;

        public override bool CanShowInLetterStack => base.CanShowInLetterStack && PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists.Contains(value: this.PlayerBetrothed);

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (this.ArchivedOnly)
                {
                    yield return this.Option_Close;
                }
                else
                {
                    DiaOption accept = new DiaOption(text: "RansomDemand_Accept".Translate())
                    {
                        action = () =>
                        {
                            //TODO: 如果玩家方的小人是玩家派系的领袖该怎么处理？是否应该反过来让NPC阵营的配偶来投靠玩家？

                            //如果玩家方的小人是派系领袖的子女，则结成永久同盟。
                            permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
                            permanentAlliance.PlayerFactionLeader = PlayerFactionLeader;
                            permanentAlliance.PlayerBetrothed = PlayerBetrothed;
                            permanentAlliance.NpcMarriageSeeker = NpcMarriageSeeker;
                            permanentAlliance.WithFaction = NpcMarriageSeeker.Faction;

                            //好感度增加至100
                            NpcMarriageSeeker.Faction.TryAffectGoodwillWith(other: Faction.OfPlayer, goodwillChange: 100 - NpcMarriageSeeker.Faction.GoodwillWith(Faction.OfPlayer), canSendMessage: true, canSendHostilityLetter: true);

                            //如果本方小人在远行队，则从远行队中删除（注：已无必要，因为事件触发时要求被求婚者和玩家派系领袖都在同一小地图上）
                            /*if (PlayerBetrothed.GetCaravan() is Caravan caravan)
                            {
                                CaravanInventoryUtility.MoveAllInventoryToSomeoneElse(from: PlayerBetrothed, candidates: caravan.PawnsListForReading);
                                HealIfPossible(p: PlayerBetrothed);
                                caravan.RemovePawn(p: PlayerBetrothed);
                            }*/

                            //把NPC小人生成到地图（玩家派系领袖此刻所处的小地图）,再随机生成些事件小人当求婚者的随从。
                            List<Pawn> vipPawns = new List<Pawn>();
                            vipPawns.Add(NpcMarriageSeeker);
                            List<Pawn> incidentPawns;
                            IntVec3 spawnLoc;
                            Utils.SpawnVIPAndIncidentPawns(PlayerFactionLeader.Map, NpcMarriageSeeker.Faction, vipPawns, 5000, PawnGroupKindDefOf.Combat, out incidentPawns, out spawnLoc);

                            //本方小人加入NPC派系，并和对方立刻订婚
                            PlayerBetrothed.SetFaction(newFaction: NpcMarriageSeeker.Faction);
                            PlayerBetrothed.relations.AddDirectRelation(PawnRelationDefOf.Fiance, NpcMarriageSeeker);

                            //求婚者带着NPC的军队等候被求婚者，汇合后举行婚礼，随后离开地图。
                            vipPawns.Add(PlayerBetrothed);

                            //汇合点：任意附近的围城点
                            var stageLoc = RCellFinder.FindSiegePositionFrom(spawnLoc, PlayerBetrothed.Map);
                            var lordJobEscortPlayerBetrothed = new LordJobDefendMarriageLeave(stageLoc, PlayerBetrothed, NpcMarriageSeeker, 7);
                            var lord1 = LordMaker.MakeNewLord(NpcMarriageSeeker.Faction, lordJobEscortPlayerBetrothed, PlayerFactionLeader.Map, incidentPawns.Concat(vipPawns).ToList());

                            //更改状态为结婚
                            MarriageCeremonyUtility.Married(PlayerBetrothed, NpcMarriageSeeker);

                            //求婚者改变文化
                            NpcMarriageSeeker.ideo.SetIdeo(PlayerFactionLeader.Ideo);
                        }
                    };
                    DiaNode dialogueNodeAccept = new DiaNode(text: "DMP_DiplomaticMarriagePlusProposalAccept".Translate(NpcMarriageSeeker.Faction.Name, PlayerBetrothed.Label, NpcMarriageSeeker.Label).CapitalizeFirst().AdjustedFor(this.NpcMarriageSeeker));
                    dialogueNodeAccept.options.Add(item: this.Option_Close);
                    accept.link = dialogueNodeAccept;

                    DiaOption reject = new DiaOption(text: "RansomDemand_Reject".Translate())
                    {
                        action = () =>
                        {
                            //关系-5
                            this.NpcMarriageSeeker.Faction.TryAffectGoodwillWith(other: Faction.OfPlayer, goodwillChange: -5, canSendMessage: true, canSendHostilityLetter: true);
                            Find.LetterStack.RemoveLetter(this);
                        }
                    };
                    DiaNode dialogueNodeReject = new DiaNode(text: "DMP_DiplomaticMarriagePlusProposalReject".Translate(this.NpcMarriageSeeker.Faction).CapitalizeFirst().AdjustedFor(this.NpcMarriageSeeker));
                    dialogueNodeReject.options.Add(item: this.Option_Close);
                    reject.link = dialogueNodeReject;

                    yield return accept;
                    yield return reject;
                    yield return this.Option_Postpone;
                }
            }
        }

        private static void HealIfPossible(Pawn p)
        {
            List<Hediff> tmpHediffs = new List<Hediff>();
            tmpHediffs.AddRange(collection: p.health.hediffSet.hediffs);
            foreach (Hediff hediffTemp in tmpHediffs)
            {
                if (hediffTemp is Hediff_Injury hediffInjury && !hediffInjury.IsPermanent())
                {
                    p.health.RemoveHediff(hediff: hediffInjury);
                }
                else
                {
                    ImmunityRecord immunityRecord = p.health.immunity.GetImmunityRecord(def: hediffTemp.def);
                    if (immunityRecord != null)
                        immunityRecord.immunity = 1f;
                }
            }
            tmpHediffs.Clear();
        }
    }
}
