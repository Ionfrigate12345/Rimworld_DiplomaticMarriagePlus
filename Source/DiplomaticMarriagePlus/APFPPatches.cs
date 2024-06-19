using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DiplomaticMarriagePlus.Model;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DiplomaticMarriagePlus
{
    [StaticConstructorOnStartup]
    public static class APFPPatches
    {
        static APFPPatches()
        {
            // Create a Harmony instance
            var harmony = new Harmony("DiplomaticMarriagePlus.APFPPatches");

            //A Petition For Provisions
            MethodInfo targetMethod = AccessTools.Method(
                "ItemRequests.DialogWindow:RequestItemOption",
                new Type[] { typeof(Map), typeof(Faction), typeof(Pawn) }
            );

            var postfix = new HarmonyMethod(typeof(APFPPatches).GetMethod(nameof(RequestItemOptionPostfix)));
            harmony.Patch(targetMethod, postfix: postfix);
        }

        // A Petition For Provisions补丁：除了永久同盟外，禁止其它派系提供该mod的交易系统。此外永久同盟也有CD时间，取决于殖民地总数（包括边缘城市的城市类基地）
        public static void RequestItemOptionPostfix(ref DiaOption __result, Map map, Faction faction, Pawn negotiator)
        {
            var permanentAlliance = Find.World.GetComponent<PermanentAlliance>();

            if (permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID || faction != permanentAlliance.WithFaction)
            {
                __result = DisableDiaOption(__result, map, faction, negotiator, "[DMP] Disabled for not being permanent ally");//TODO: Language
            }
        }

        private static DiaOption DisableDiaOption(DiaOption diaOptionRet, Map map, Faction faction, Pawn negotiator, String newText)
        {
            diaOptionRet = new DiaOption(newText);
            diaOptionRet.Disable(newText);
            diaOptionRet.action = () =>
            {
                Log.Message("[DMP] A Petition For Provision dialog option disabled. Only permanent ally not under CD will offer provisions.");
            };

            return diaOptionRet;
        }
    }
}
