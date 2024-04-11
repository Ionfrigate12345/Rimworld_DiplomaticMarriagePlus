using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiplomaticMarriagePlus.Global;
using DiplomaticMarriagePlus.Model;
using RimWorld;
using Verse;

namespace DiplomaticMarriagePlus.Controller
{
    internal class TemporaryStayEventController : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            PermanentAlliance permanentAlliance = Find.World.GetComponent<PermanentAlliance>();
            if (permanentAlliance == null || permanentAlliance.IsValid() != PermanentAlliance.Validity.VALID)
            {
                Log.Warning("Random visit event aborted: No permanent alliance");
                //只有永久同盟生效时才可能启动该事件
                return false;
            }

            if (permanentAlliance.NpcMarriageSeeker.Map != null || permanentAlliance.PlayerBetrothed.Map != null)
            {
                Log.Warning("Random visit event aborted: At least one of the couple is on player's colony map");
                //只有二人都不在小地图时才能触发。
                return false;
            }

            Map map = TradeUtility.PlayerHomeMapWithMostLaunchableSilver();

            if(GenHostility.AnyHostileActiveThreatToPlayer(map))
            {
                //如果此刻地图上有敌人则无法触发。
                return false;
            }

            //如果TemporaryStay尚未初始化，则强制产生思乡病，开始第一轮访问计划。
            //往后TemporaryStay会自己运行，在一轮访问完成后自己产生下一次思乡病，无需本事件再次启动，直到永久同盟终结。
            //TODO: 访问期间会有随机的敌对派系袭击。
            TemporaryStay temporaryStay = Find.World.GetComponent<TemporaryStay>();
            if (!temporaryStay.IsInitialized())
            {
                temporaryStay.InitializeByForcingFirstNostalgia();
            }

            return true;

        }
    }
}
