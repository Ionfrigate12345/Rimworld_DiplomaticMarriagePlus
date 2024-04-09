﻿using DiplomaticMarriagePlus.Model;
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
            //options.Gap();
            options.Label($"{"DMP_Setting_GoodwillDailyIncreaseSocialSkillFactor".Translate()}: {settings.goodwillDailyIncreaseSocialSkillFactor.ToStringByStyle(style: ToStringStyle.FloatOne)}");
            settings.goodwillDailyIncreaseSocialSkillFactor = options.Slider(settings.goodwillDailyIncreaseSocialSkillFactor, 0.0f, 2.0f);
            options.GapLine(15f);
            options.End();
            base.DoSettingsWindowContents(rect);
            //this.Mod.GetSettings<DMPModSettings>().Write();
        }

        public override string SettingsCategory()
        {
            return "DMP_Setting_ModName".Translate();
        }
    }
}
