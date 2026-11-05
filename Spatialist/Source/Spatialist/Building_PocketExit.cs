using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Spatialist
{
    public class Building_PocketExit : PocketMapExit
    {
        private bool GateOpen => entrance != null && entrance.Spawned;

        public override bool IsEnterable(out string reason)
        {
            if (!GateOpen)
            {
                reason = "Spatialist.GateClosed".Translate();
                return false;
            }
            return base.IsEnterable(out reason);
        }

        public override Map GetOtherMap() => GateOpen ? entrance.Map : null;

        public override IntVec3 GetDestinationLocation() => GateOpen ? entrance.Position : IntVec3.Invalid;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            string viewSurface = "CommandViewSurface".Translate();
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                // Vanilla's "view surface" jumps to the entrance, which doesn't exist while the gate is closed.
                if (!GateOpen && gizmo is Command command && command.defaultLabel == viewSurface)
                {
                    continue;
                }
                yield return gizmo;
            }
        }
    }
}
