using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace DiplomaticMarriagePlus.Model
{
    public class DMPModSettings : ModSettings
    {
        public int goodwillDailyIncreaseBaseValue = 5;
        public float goodwillDailyIncreaseSocialSkillFactor = 1.0f;
        public float threatMultiplier = 1.0f;
        public float factionConversionChancePerSocialSkill = 0.05f;
        public bool warningVIPOnTheMap = true;

        public int temporaryStayMinimumDaysForNostalgia = 15;
        public int temporaryStayDailyChanceInitial = 10;
        public float temporaryStayDailyChanceIncrease = 3.0f;
        public int temporaryStayStartAfterDaysMinimum = 3;
        public int temporaryStayStartAfterDaysMaximum = 6;
        public int temporaryStayDurationMinimum = 7;
        public int temporaryStayDurationMaximum = 15;

        //永久同盟派系每提高一点全球殖民地总数的百分比，在使用A Petition For Provision的特权时能减少多少CD时间，单位为（游戏内）小时
        public int apfpCooldownReductionHoursPerGlobalSettlementPercentage = 24;

        public override void ExposeData()
        {
            Scribe_Values.Look(value: ref goodwillDailyIncreaseBaseValue, label: "DMP_Settings_GoodwillDailyIncreaseBaseValue", defaultValue: 5);
            Scribe_Values.Look(value: ref goodwillDailyIncreaseSocialSkillFactor, label: "DMP_Settings_GoodwillDailyIncreaseSocialSkillFactor", defaultValue: 1.0f);
            Scribe_Values.Look(value: ref threatMultiplier, label: "DMP_Settings_ThreatMultiplier", defaultValue: 1.0f);
            Scribe_Values.Look(value: ref factionConversionChancePerSocialSkill, label: "DMP_Settings_FactionConversionChancePerSocialSkill", defaultValue: 0.15f);
            Scribe_Values.Look(value: ref warningVIPOnTheMap, label: "DMP_Settings_WarningVIPOnTheMap", defaultValue: true);

            Scribe_Values.Look(value: ref temporaryStayMinimumDaysForNostalgia, label: "DMP_Settings_TemporaryStayMinimumDaysForNostalgia", defaultValue: 15);
            Scribe_Values.Look(value: ref temporaryStayDailyChanceInitial, label: "DMP_Settings_TemporaryStayDailyChanceInitial", defaultValue: 10);
            Scribe_Values.Look(value: ref temporaryStayDailyChanceIncrease, label: "DMP_Settings_TemporaryStayDailyChanceIncrease", defaultValue: 3.0f);
            Scribe_Values.Look(value: ref temporaryStayStartAfterDaysMinimum, label: "DMP_Settings_TemporaryStayStartAfterDaysMinimum", defaultValue: 3);
            Scribe_Values.Look(value: ref temporaryStayStartAfterDaysMaximum, label: "DMP_Settings_TemporaryStayStartAfterDaysMaximum", defaultValue: 6);
            Scribe_Values.Look(value: ref temporaryStayDurationMinimum, label: "DMP_Settings_TemporaryStayDurationMinimum", defaultValue: 7);
            Scribe_Values.Look(value: ref temporaryStayDurationMaximum, label: "DMP_Settings_TemporaryStayDurationMaximum", defaultValue: 15);

            Scribe_Values.Look(value: ref apfpCooldownReductionHoursPerGlobalSettlementPercentage, label: "DMP_Settings_APFPCooldownReductionHoursPerGlobalSettlementPercentage", defaultValue: 24);

            base.ExposeData();
        }
    }
}
