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
        public float goodwillDailyIncreaseSocialSkillFactor = 1.0f;
        public float threatMultiplier = 1.0f;

        public override void ExposeData()
        {
            Scribe_Values.Look(value: ref goodwillDailyIncreaseSocialSkillFactor, label: "DMP_Settings_GoodwillDailyIncreaseSocialSkillFactor", defaultValue: 1.0f);
            Scribe_Values.Look(value: ref threatMultiplier, label: "DMP_Settings_ThreatMultiplier", defaultValue: 1.0f);
            base.ExposeData();
        }
    }
}
