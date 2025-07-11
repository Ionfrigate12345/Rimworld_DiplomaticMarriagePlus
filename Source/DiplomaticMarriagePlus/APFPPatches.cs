using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DiplomaticMarriagePlus.Global;
using DiplomaticMarriagePlus.Model;
using DiplomaticMarriagePlus.View;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DiplomaticMarriagePlus
{
    [StaticConstructorOnStartup]
    public static class APFPPatches
    {
        //A Petition For Provisions（以下简称APFP）补丁
        static APFPPatches()
        {
            if (!ModsConfig.IsActive("mlie.apetitionforprovisions"))
            {
                return;
            }

            var harmony = new Harmony("DiplomaticMarriagePlus.APFPPatches");

            MethodInfo targetMethodDialogWindowRequestItemOption = AccessTools.Method(
                "ItemRequests.DialogWindow:RequestItemOption",
                new Type[] { typeof(Map), typeof(Faction), typeof(Pawn) }
            );

            harmony.Patch(targetMethodDialogWindowRequestItemOption, postfix: new HarmonyMethod(typeof(APFPPatches).GetMethod(nameof(RequestItemOptionPostfix))));

            MethodInfo targetMethodFulfillItemRequestWindowSpawnItem = AccessTools.Method(
                "ItemRequests.FulfillItemRequestWindow:SpawnItem",
                new Type[] { AccessTools.TypeByName("ItemRequests.RequestItem") }
            );

            harmony.Patch(targetMethodFulfillItemRequestWindowSpawnItem, postfix: new HarmonyMethod(typeof(APFPPatches).GetMethod(nameof(SpawnItemPostfix))));
        }

        //除了永久同盟外，禁止其它派系提供APFP的交易系统（屏蔽通讯台该选项）。此外永久同盟也有CD时间
        public static void RequestItemOptionPostfix(ref DiaOption __result, Map map, Faction faction, Pawn negotiator)
        {
            var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();

            //检查该派系是否为永久同盟派系，如果不是就禁止对话选项
            if (permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID || faction != permanentAlliance.WithFaction)
            {
                __result.Disable("DMP_PermanentAllianceAPFPButtonDisabledNotPA".Translate());
                return;
            }

            if (Utils.IsSpaceMap(map))
            {
                __result.Disable("DMP_PermanentAllianceAPFPButtonDisabledSOS2SpaceMap".Translate());
                return;
            }

            //检查永久同盟上次交易时间是否冷却完毕。并根据同盟派系的总殖民地占全球比例和模组设置来计算当前冷却时间。
            int totalGlobalSettlementCount = Find.WorldObjects.Settlements.Where(settlement =>
                    !settlement.def.defName.Equals("City_Abandoned") //排除边缘城市据点中的废弃据点和鬼城
                    && !settlement.def.defName.Equals("City_Ghost")
                    ).ToList().Count;
            int totalPAFactionSettlementCount = Find.WorldObjects.Settlements.Where(s => s.Faction == permanentAlliance.WithFaction).ToList().Count;
            int apfpCoolDownReductionHours = (int)(DMPModWindow.Instance.settings.apfpCooldownReductionHoursPerGlobalSettlementPercentage * totalPAFactionSettlementCount * 100.0f / totalGlobalSettlementCount);
            int apfpCooldownIncreaseTicks = GenDate.TicksPerYear - apfpCoolDownReductionHours * GenDate.TicksPerHour;
            if(apfpCooldownIncreaseTicks < 0)
            {
                apfpCooldownIncreaseTicks = 0;
            }
            var remainingTicks = PermanentAlliance.LastAPFPTradeTicks + 240000 + apfpCooldownIncreaseTicks - GenTicks.TicksAbs;
            if (PermanentAlliance.LastAPFPTradeTicks > 0 //本存档第一次使用时不会算为禁止。
                && remainingTicks > 0)
            {
                __result.Disable("DMP_PermanentAllianceAPFPButtonDisabledInCD".Translate((int)(remainingTicks / GenDate.TicksPerDay)));
                return;
            }
            Log.Message("[DMP] APFP trade granted.");
            __result.disabled = false;
        }

        //每次检测到APFP的交易成功，记录下交易的Tick时间，用来判定冷却开始。
        public static void SpawnItemPostfix(IExposable requested)
        {
            PermanentAlliance.LastAPFPTradeTicks = GenTicks.TicksAbs;
        }
    }
}
