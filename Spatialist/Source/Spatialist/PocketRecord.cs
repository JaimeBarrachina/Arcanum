using Verse;

namespace Spatialist
{
    // One caster's pocket. The map and its exit are permanent; the gate is made on open and destroyed on close.
    public class PocketRecord : IExposable
    {
        public Pawn owner;
        public Map map;
        public Building_PocketExit exit;
        public Building_PocketGate gate;
        private int openSize;

        public bool GateOpen => gate != null && gate.Spawned;

        public bool MapAlive => map != null && Find.Maps.Contains(map);

        // Side of the open square in the middle of the map. Pockets made before Expand Pocket existed were
        // generated at 15x15 with no walls, so for them the whole map counts as open.
        public int OpenSize
        {
            get => openSize > 0 ? openSize : map?.Size.x ?? 0;
            set => openSize = value;
        }

        public bool CanExpand => MapAlive && OpenSize < map.Size.x;

        public void ExposeData()
        {
            Scribe_References.Look(ref owner, "owner");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref exit, "exit");
            Scribe_References.Look(ref gate, "gate");
            Scribe_Values.Look(ref openSize, "openSize");
        }
    }
}
