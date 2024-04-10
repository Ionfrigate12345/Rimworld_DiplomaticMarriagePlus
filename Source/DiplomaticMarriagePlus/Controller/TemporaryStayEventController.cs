using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;

namespace DiplomaticMarriagePlus.Controller
{
    internal class TemporaryStayEventController : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            return false;
        }
    }
}
