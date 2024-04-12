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
            options.CheckboxLabeled("DMP_Setting_WarningVIPOnTheMap".Translate(), ref settings.warningVIPOnTheMap, "DMP_Setting_WarningVIPOnTheMapDetails".Translate());
            options.GapLine(15f);
            options.Label($"{"DMP_Setting_TemporaryStayMinimumDaysForNostalgia".Translate()}: {settings.temporaryStayMinimumDaysForNostalgia}");
            settings.temporaryStayMinimumDaysForNostalgia = (int)options.Slider(settings.temporaryStayMinimumDaysForNostalgia, 0, 60);
            options.Label($"{"DMP_Setting_TemporaryStayDailyChanceInitial".Translate()}: {settings.temporaryStayDailyChanceInitial}");
            settings.temporaryStayDailyChanceInitial = (int)options.Slider(settings.temporaryStayDailyChanceInitial, 0, 100);
            options.Label($"{"DMP_Setting_TemporaryStayDailyChanceIncrease".Translate()}: {settings.temporaryStayDailyChanceIncrease.ToStringByStyle(style: ToStringStyle.FloatOne)}");
            settings.temporaryStayDailyChanceIncrease = options.Slider(settings.temporaryStayDailyChanceIncrease, 0.0f, 10.0f);
            options.Label($"{"DMP_Setting_TemporaryStayStartAfterDaysMinimum".Translate()}: {settings.temporaryStayStartAfterDaysMinimum}");
            settings.temporaryStayStartAfterDaysMinimum = (int)options.Slider(settings.temporaryStayStartAfterDaysMinimum, 0, 15);
            options.Label($"{"DMP_Setting_TemporaryStayStartAfterDaysMaximum".Translate()}: {settings.temporaryStayStartAfterDaysMaximum}");
            settings.temporaryStayStartAfterDaysMaximum = (int)options.Slider(settings.temporaryStayStartAfterDaysMaximum, 0, 15);
            options.Label($"{"DMP_Setting_TemporaryStayDurationMinimum".Translate()}: {settings.temporaryStayDurationMinimum}");
            settings.temporaryStayDurationMinimum = (int)options.Slider(settings.temporaryStayDurationMinimum, 1, 15);
            options.Label($"{"DMP_Setting_TemporaryStayDurationMaximum".Translate()}: {settings.temporaryStayDurationMaximum}");
            settings.temporaryStayDurationMaximum = (int)options.Slider(settings.temporaryStayDurationMaximum, 1, 15);
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
