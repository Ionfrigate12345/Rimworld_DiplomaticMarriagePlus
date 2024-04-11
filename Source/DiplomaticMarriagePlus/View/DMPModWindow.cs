using DiplomaticMarriagePlus.Model;
using UnityEngine;
using Verse;

namespace DiplomaticMarriagePlus.View
{
    internal class DMPModWindow : Mod
    {
        public readonly DMPModSettings settings;
        public static DMPModWindow Instance { get; private set; }

        public DMPModWindow(ModContentPack content) : base(content)
        {
            settings = GetSettings<DMPModSettings>();
            Instance = this;
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            Listing_Standard options = new Listing_Standard();
            options.Begin(rect: rect);
            options.Label($"{"DMP_Setting_GoodwillDailyIncreaseBaseValue".Translate()}: {settings.goodwillDailyIncreaseBaseValue}");
            settings.goodwillDailyIncreaseBaseValue = (int)options.Slider(settings.goodwillDailyIncreaseBaseValue, 0.0f, 20.0f);
            options.Label($"{"DMP_Setting_GoodwillDailyIncreaseSocialSkillFactor".Translate()}: {settings.goodwillDailyIncreaseSocialSkillFactor.ToStringByStyle(style: ToStringStyle.FloatOne)}");
            settings.goodwillDailyIncreaseSocialSkillFactor = options.Slider(settings.goodwillDailyIncreaseSocialSkillFactor, 0.0f, 2.0f);
            options.Label($"{"DMP_Setting_ThreatMultiplier".Translate()}: {settings.threatMultiplier.ToStringByStyle(style: ToStringStyle.FloatTwo)}");
            settings.threatMultiplier = options.Slider(settings.threatMultiplier, 0.5f, 5.0f);
            options.Label($"{"DMP_Setting_FactionConversionChancePerSocialSkill".Translate()}: {settings.factionConversionChancePerSocialSkill.ToStringByStyle(style: ToStringStyle.FloatThree)}");
            settings.factionConversionChancePerSocialSkill = options.Slider(settings.factionConversionChancePerSocialSkill, 0f, 0.2f);
            options.CheckboxLabeled("DMP_Setting_WarningVIPOnTheMap".Translate(), ref settings.warningVIPOnTheMap, "DMP_Settings_WarningVIPOnTheMapDetails".Translate());
            options.GapLine(15f);
            options.End();
            base.DoSettingsWindowContents(rect);
        }

        public override string SettingsCategory()
        {
            return "DMP_Setting_ModName".Translate();
        }
    }
}
