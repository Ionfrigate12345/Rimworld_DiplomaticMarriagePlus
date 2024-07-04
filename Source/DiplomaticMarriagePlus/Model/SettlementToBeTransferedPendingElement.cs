using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld.Planet;

namespace DiplomaticMarriagePlus.Model
{
    public class SettlementToBeTransferedPendingElement
    {
        public int Tile { get; set; }
        public string Name { get; set; }

        public Settlement Settlement { get; set; }

        public SettlementToBeTransferedPendingElement(int tile, string name, Settlement settlement)
        {
            this.Tile = tile;
            this.Name = name;
            this.Settlement = settlement;
        }
    }
}
