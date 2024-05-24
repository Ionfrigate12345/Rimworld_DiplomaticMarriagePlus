using HarmonyLib;
using Verse;

namespace DiplomaticMarriagePlus
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        private static Harmony _harmonyInstance;
        public static Harmony HarmonyInstance { get { return _harmonyInstance; } }

        static HarmonyPatches()
        {
            _harmonyInstance = new Harmony("com.ionfrigate12345.diplomaticmarriageplus.saveourships2.filterspacemap");
            _harmonyInstance.PatchAll();
        }
    }
}