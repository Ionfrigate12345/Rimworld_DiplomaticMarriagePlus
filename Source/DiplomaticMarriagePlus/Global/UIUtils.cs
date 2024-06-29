using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiplomaticMarriagePlus.Model;
using RimWorld;
using UnityEngine;
using Verse;

namespace DiplomaticMarriagePlus.Global
{
    internal class UIUtils
    {
        public static void AddWidget(ref float curBaseY, TaggedString title, TaggedString description)
        {
            var rightMargin = 7f;
            var zlRect = new Rect(UI.screenWidth - Alert.Width, curBaseY - 24f, Alert.Width, 24f);
            Text.Font = GameFont.Small;

            if (Mouse.IsOver(zlRect))
            {
                Widgets.DrawHighlight(zlRect);
            }

            GUI.BeginGroup(zlRect);
            Text.Anchor = TextAnchor.UpperRight;
            var rect = zlRect.AtZero();
            rect.xMax -= rightMargin;

            Widgets.Label(rect, title);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.EndGroup();

            TooltipHandler.TipRegion(zlRect, new TipSignal(description));

            curBaseY -= zlRect.height;
        }
    }
}
