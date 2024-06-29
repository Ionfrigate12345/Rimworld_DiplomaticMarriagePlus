using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DiplomaticMarriagePlus.Global;
using DiplomaticMarriagePlus.Model;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace DiplomaticMarriagePlus
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        private static Harmony _harmonyInstance;
        public static Harmony HarmonyInstance { get { return _harmonyInstance; } }

        static HarmonyPatches()
        {
            _harmonyInstance = new Harmony("com.ionfrigate12345.diplomaticmarriageplus");
            _harmonyInstance.PatchAll();
        }
    }

    [HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoDate))]
    internal class ShowTemporaryStayRemaining
    {
        private static void Postfix(ref float curBaseY)
        {
            var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
            var temporaryStay = Find.World.GetComponent<TemporaryStay>();

            //如果有有效的永久同盟，则显示状态
            if(permanentAlliance != null && permanentAlliance.IsValid() == PermanentAlliance.Validity.VALID)
            {
                UIUtils.AddWidget(ref curBaseY,
                    "DMP_PermanentAllianceInfo_WidgetTitle".Translate(),
                    TranslatorFormattedStringExtensions.Translate("DMP_PermanentAllianceInfo_WidgetTitleDesc",
                        NamedArgumentUtility.Named(permanentAlliance.PlayerBetrothed.Name, "{0}"),
                        NamedArgumentUtility.Named(permanentAlliance.NpcMarriageSeeker.Name, "{1}"),
                        NamedArgumentUtility.Named(permanentAlliance.WithFaction.Name, "{2}")
                    )
                );
            }

            //显示目前的夫妇暂住信息
            if (temporaryStay != null && temporaryStay.IsRunning && temporaryStay.IsCurrentlyOnVisit)
            {
                //夫妇已抵达，正在暂住中
                var ticksRemaining = temporaryStay.TickLastTemporaryVisitEnd - GenTicks.TicksAbs;
                var hoursRemaining = CalculateRemainingHours(ticksRemaining);

                UIUtils.AddWidget(ref curBaseY, 
                    "DMP_TemporaryStay_WidgetTitle".Translate(), 
                    TranslatorFormattedStringExtensions.Translate("DMP_TemporaryStay_WidgetTitleDesc",
                        NamedArgumentUtility.Named(permanentAlliance.WithFaction.Name, "{0}"),
                        NamedArgumentUtility.Named(permanentAlliance.PlayerBetrothed.Name, "{1}"),
                        NamedArgumentUtility.Named(permanentAlliance.NpcMarriageSeeker.Name, "{2}"),
                        NamedArgumentUtility.Named(Math.Round(hoursRemaining / 24.0f, 1), "{3}")
                    )
                );
            }
        }

        private static float CalculateRemainingHours(float ticksRemaining)
        {
            return (float)(ticksRemaining <= 0 ? 0 : Math.Ceiling(ticksRemaining / GenDate.TicksPerHour));
        }
    }
}