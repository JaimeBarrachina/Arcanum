using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace Spatialist
{
    public class WorldComponent_PocketDimensions : WorldComponent
    {
        // The map is made at its final size; Expand Pocket removes rings of wall around the open middle.
        public const int PocketSize = 45;
        public const int StartOpenSize = 15;
        public const int ExpandStep = 10;
        private const int ValidateInterval = 60;

        private List<PocketRecord> records = new List<PocketRecord>();

        public static WorldComponent_PocketDimensions Instance => Find.World.GetComponent<WorldComponent_PocketDimensions>();

        public WorldComponent_PocketDimensions(World world) : base(world)
        {
        }

        public static bool IsPocketDimension(Map map) => map?.generatorDef == SpatialistDefOf.Spatialist_PocketMap;

        public PocketRecord RecordFor(Pawn pawn) => pawn == null ? null : records.FirstOrDefault(r => r.owner == pawn);

        public void OpenGate(Pawn caster, IntVec3 cell, Map map)
        {
            PocketRecord record = RecordFor(caster);
            if (record == null)
            {
                record = new PocketRecord { owner = caster };
                records.Add(record);
            }
            if (record.GateOpen)
            {
                CloseGate(record);
            }

            var gate = (Building_PocketGate)ThingMaker.MakeThing(SpatialistDefOf.Spatialist_PocketGate);
            gate.owner = caster;
            gate.SetFaction(caster.Faction);
            GenSpawn.Spawn(gate, cell, map, WipeMode.VanishOrMoveAside);
            record.gate = gate;

            if (!record.MapAlive)
            {
                record.map = GeneratePocket(gate, map);
                record.exit = gate.exit as Building_PocketExit;
                record.OpenSize = StartOpenSize;
            }
            else if (record.exit == null || !record.exit.Spawned)
            {
                record.exit = RespawnExit(gate, record.map);
            }
            gate.LinkTo(record.map, record.exit);
            ((PocketMapParent)record.map.Parent).sourceMap = map;
            RepairRoof(record);

            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipInnerExit, 2f);
            SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(cell, map));
        }

        public void CloseGate(PocketRecord record, Map ejectMap = null, IntVec3? ejectCell = null)
        {
            Building_PocketGate gate = record.gate;
            record.gate = null;
            if (record.exit != null)
            {
                record.exit.entrance = null;
            }
            if (gate == null || !gate.Spawned)
            {
                return;
            }
            Map gateMap = gate.Map;
            IntVec3 gatePos = gate.Position;
            if (gate.LoadInProgress)
            {
                gate.CancelLoad();
            }
            if (record.MapAlive)
            {
                EjectPawns(record.map, ejectMap ?? gateMap, ejectCell ?? gatePos);
            }
            FleckMaker.Static(gatePos, gateMap, FleckDefOf.PsycastSkipFlashEntry, 2f);
            SoundDefOf.Psycast_Skip_Entry.PlayOneShot(new TargetInfo(gatePos, gateMap));
            Vanish(gate);
        }

        // Emergency exit for anyone stuck in a pocket whose gate can't be opened.
        public void Evacuate(PocketRecord record)
        {
            if (!record.MapAlive || !TryFindOutsideSpot(record, out Map target, out IntVec3 near))
            {
                Messages.Message("Spatialist.NowhereToGo".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            EjectPawns(record.map, target, near);
        }

        // Shared check for the psycasts that use an existing pocket from outside it (Stash, Displace, Sanctuary, Expand).
        public bool TryGetPocket(Pawn pawn, out PocketRecord record, out string reason)
        {
            record = RecordFor(pawn);
            if (record == null || !record.MapAlive)
            {
                reason = "Spatialist.NoPocketYet".Translate();
                return false;
            }
            reason = "";
            return true;
        }

        // Where Stash and Displace send items: from outside, into the pocket by the exit. From inside the caster's
        // own pocket, out next to the open gate. False when cast inside the pocket with the gate closed.
        public static bool TryGetItemDestination(PocketRecord record, Map castFrom, out Map map, out IntVec3 landing)
        {
            if (castFrom == record.map)
            {
                map = record.GateOpen ? record.gate.Map : null;
                landing = record.GateOpen ? record.gate.Position : IntVec3.Invalid;
                return map != null;
            }
            map = record.map;
            landing = record.exit != null && record.exit.Spawned ? record.exit.Position : record.map.Center;
            return true;
        }

        // Skips items to another map, landing around the given cell. Returns how many stacks arrived.
        public int SendItems(IEnumerable<Thing> things, Map toMap, IntVec3 landing)
        {
            int sent = 0;
            foreach (Thing thing in things.ToList())
            {
                if (!thing.Spawned)
                {
                    continue;
                }
                Map from = thing.Map;
                IntVec3 cell = thing.Position;
                thing.DeSpawn();
                if (GenPlace.TryPlaceThing(thing, landing, toMap, ThingPlaceMode.Near))
                {
                    FleckMaker.Static(cell, from, FleckDefOf.PsycastSkipFlashEntry, 0.6f);
                    sent++;
                }
                else
                {
                    // No room at the destination: put it back where it was.
                    GenPlace.TryPlaceThing(thing, cell, from, ThingPlaceMode.Near);
                }
            }
            return sent;
        }

        public void SendPawnsToPocket(PocketRecord record, IEnumerable<Pawn> pawns)
        {
            IntVec3 landing = record.exit != null && record.exit.Spawned ? record.exit.Position : record.map.Center;
            foreach (Pawn pawn in pawns.ToList())
            {
                FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastSkipFlashEntry, 1f);
                MovePawn(pawn, record.map, landing);
            }
        }

        // Opens the next ring of the pocket and lights it with crystals along its inner edge.
        public bool Expand(PocketRecord record)
        {
            if (!record.CanExpand)
            {
                return false;
            }
            Map map = record.map;
            IntVec3 center = map.Center;
            int oldSize = record.OpenSize;
            int newSize = System.Math.Min(oldSize + ExpandStep, map.Size.x);
            foreach (IntVec3 cell in map.AllCells)
            {
                if (GenStep_PocketRoom.IsOpen(cell, center, oldSize) || !GenStep_PocketRoom.IsOpen(cell, center, newSize))
                {
                    continue;
                }
                foreach (Thing wall in cell.GetThingList(map).Where(t => t.def == SpatialistDefOf.Spatialist_PocketWall).ToList())
                {
                    Vanish(wall);
                }
            }
            record.OpenSize = newSize;
            RepairRoof(record);

            int half = newSize / 2;
            var spots = new[]
            {
                new IntVec3(center.x - half, 0, center.z - half), new IntVec3(center.x + half, 0, center.z - half),
                new IntVec3(center.x - half, 0, center.z + half), new IntVec3(center.x + half, 0, center.z + half),
                new IntVec3(center.x, 0, center.z - half), new IntVec3(center.x, 0, center.z + half),
                new IntVec3(center.x - half, 0, center.z), new IntVec3(center.x + half, 0, center.z),
            };
            foreach (IntVec3 spot in spots)
            {
                if (spot.InBounds(map) && spot.GetFirstBuilding(map) == null)
                {
                    GenSpawn.Spawn(SpatialistDefOf.Spatialist_LightCrystal, spot, map, WipeMode.VanishOrMoveAside);
                }
            }
            return true;
        }

        // Puts back roof lost to the pre-fix collapse bug (and clears its rubble), so old saves heal on the next open or expand.
        private static void RepairRoof(PocketRecord record)
        {
            if (!record.MapAlive)
            {
                return;
            }
            Map map = record.map;
            RoofDef roof = SpatialistDefOf.Spatialist_PocketMap.genSteps
                .Select(s => s.genStep).OfType<GenStep_PocketRoom>().FirstOrDefault()?.roof;
            if (roof == null)
            {
                return;
            }
            IntVec3 center = map.Center;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (!GenStep_PocketRoom.IsOpen(cell, center, record.OpenSize))
                {
                    continue;
                }
                foreach (Thing rubble in cell.GetThingList(map).Where(t => t.def == ThingDefOf.CollapsedRocks).ToList())
                {
                    rubble.Destroy();
                }
                if (!map.roofGrid.Roofed(cell))
                {
                    map.roofGrid.SetRoof(cell, roof);
                }
            }
        }

        public void Notify_GravshipTakeoff(Building_GravEngine engine)
        {
            foreach (PocketRecord record in records)
            {
                if (record.GateOpen && record.gate.Map == engine.Map)
                {
                    // Eject next to the engine so pawns ride along instead of being left behind.
                    CloseGate(record, engine.Map, engine.Position);
                }
            }
        }

        // Validation runs on ticks only: World.FinalizeInit runs before maps load and references resolve,
        // so checking there would see every owner and map as missing and collapse every pocket.
        public override void WorldComponentTick()
        {
            if (Find.TickManager.TicksGame % ValidateInterval == 0)
            {
                ValidateAll();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "records", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                records ??= new List<PocketRecord>();
                records.RemoveAll(r => r == null);
            }
        }

        public static void Vanish(Thing thing)
        {
            Thing.allowDestroyNonDestroyable = true;
            try
            {
                thing.Destroy(DestroyMode.Vanish);
            }
            finally
            {
                Thing.allowDestroyNonDestroyable = false;
            }
        }

        private void ValidateAll()
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                PocketRecord record = records[i];
                if (record.owner == null || record.owner.Dead || record.owner.Destroyed)
                {
                    Collapse(record);
                    continue;
                }
                if (record.gate != null && !record.gate.Spawned)
                {
                    // The gate's map was removed under it (e.g. a quest map closing).
                    if (record.exit != null && record.exit.entrance == record.gate)
                    {
                        record.exit.entrance = null;
                    }
                    record.gate = null;
                }
                if (record.map != null && !record.MapAlive)
                {
                    record.map = null;
                    record.exit = null;
                }
                if (record.MapAlive)
                {
                    FixSourceMap(record);
                }
            }
        }

        // Storyteller and wealth code read sourceMap, and the map it points at can be removed while the gate is closed.
        private static void FixSourceMap(PocketRecord record)
        {
            if (!(record.map.Parent is PocketMapParent parent))
            {
                return;
            }
            if (parent.sourceMap != null && Find.Maps.Contains(parent.sourceMap))
            {
                return;
            }
            parent.sourceMap = record.GateOpen
                ? record.gate.Map
                : Find.AnyPlayerHomeMap ?? Find.Maps.FirstOrDefault(m => !m.IsPocketMap);
        }

        private void Collapse(PocketRecord record)
        {
            records.Remove(record);
            bool hasSpot = TryFindOutsideSpot(record, out Map target, out IntVec3 near);
            if (record.MapAlive)
            {
                if (hasSpot)
                {
                    Spill(record.map, target, near);
                }
                PocketMapUtility.DestroyPocketMap(record.map);
            }
            if (record.gate != null && record.gate.Spawned)
            {
                Vanish(record.gate);
            }
            if (record.owner != null && record.owner.Faction == Faction.OfPlayer)
            {
                Find.LetterStack.ReceiveLetter(
                    "Spatialist.CollapsedLabel".Translate(),
                    "Spatialist.CollapsedText".Translate(record.owner.LabelShort),
                    LetterDefOf.NeutralEvent,
                    hasSpot ? new LookTargets(near, target) : LookTargets.Invalid);
            }
        }

        private static bool TryFindOutsideSpot(PocketRecord record, out Map map, out IntVec3 cell)
        {
            if (record.GateOpen)
            {
                map = record.gate.Map;
                cell = record.gate.Position;
                return true;
            }
            Thing body = (Thing)record.owner?.Corpse ?? record.owner;
            if (body != null && body.MapHeld != null && !body.MapHeld.IsPocketMap)
            {
                map = body.MapHeld;
                cell = body.PositionHeld;
                return true;
            }
            Map source = (record.map?.Parent as PocketMapParent)?.sourceMap;
            if (source != null && Find.Maps.Contains(source) && !source.IsPocketMap)
            {
                map = source;
                cell = DropCellFinder.TradeDropSpot(source);
                return true;
            }
            map = Find.AnyPlayerHomeMap;
            cell = map != null ? DropCellFinder.TradeDropSpot(map) : IntVec3.Invalid;
            return map != null;
        }

        private static Map GeneratePocket(Building_PocketGate gate, Map sourceMap)
        {
            // Same as PocketMapUtility.GeneratePocketMap, plus the tile so the pocket keeps the caster's time of day.
            var parent = (PocketMapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.PocketMap);
            parent.sourceMap = sourceMap;
            parent.mapGenerator = SpatialistDefOf.Spatialist_PocketMap;
            parent.Tile = sourceMap.Tile;
            PocketMapUtility.currentlyGeneratingPortal = gate;
            try
            {
                Map pocket = MapGenerator.GenerateMap(new IntVec3(PocketSize, 1, PocketSize), parent, parent.mapGenerator, isPocketMap: true);
                Find.World.pocketMaps.Add(parent);
                return pocket;
            }
            finally
            {
                PocketMapUtility.currentlyGeneratingPortal = null;
            }
        }

        private static Building_PocketExit RespawnExit(Building_PocketGate gate, Map pocket)
        {
            ThingDef exitDef = SpatialistDefOf.Spatialist_PocketMap.genSteps
                .Select(s => s.genStep).OfType<GenStep_PocketRoom>().First().exitDef;
            IntVec3 cell = CellFinder.StandableCellNear(pocket.Center, pocket, 5f);
            PocketMapUtility.currentlyGeneratingPortal = gate;
            try
            {
                return (Building_PocketExit)GenSpawn.Spawn(exitDef, cell.IsValid ? cell : pocket.Center, pocket, WipeMode.VanishOrMoveAside);
            }
            finally
            {
                PocketMapUtility.currentlyGeneratingPortal = null;
            }
        }

        private static void EjectPawns(Map from, Map to, IntVec3 near)
        {
            foreach (Pawn pawn in from.mapPawns.AllPawnsSpawned.ToList())
            {
                MovePawn(pawn, to, near);
            }
        }

        // Mirrors JobDriver_EnterPortal's hand-over between maps.
        private static void MovePawn(Pawn pawn, Map to, IntVec3 near)
        {
            IntVec3 cell = CellFinder.StandableCellNear(near, to, 5f);
            if (!cell.IsValid)
            {
                cell = CellFinder.RandomClosewalkCellNear(near, to, 10);
            }
            bool drafted = pawn.IsPlayerControlled && pawn.Drafted;
            pawn.DeSpawnOrDeselect();
            GenSpawn.Spawn(pawn, cell, to, Rot4.Random);
            if (drafted)
            {
                pawn.drafter.Drafted = true;
            }
            if (pawn.carryTracker?.CarriedThing != null && !pawn.Drafted)
            {
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            }
            pawn.mindState?.priorityWork?.ClearPrioritizedWorkAndJobQueue();
            pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.ExitedMap);
        }

        private static void Spill(Map from, Map to, IntVec3 near)
        {
            EjectPawns(from, to, near);
            foreach (Thing thing in from.listerThings.AllThings.ToList())
            {
                if (!thing.Spawned)
                {
                    continue;
                }
                Thing moving = null;
                if (thing.def.category == ThingCategory.Item)
                {
                    thing.DeSpawn();
                    moving = thing;
                }
                else if (thing.def.category == ThingCategory.Building && thing.def.Minifiable)
                {
                    moving = thing.MakeMinified();
                }
                if (moving != null && !GenPlace.TryPlaceThing(moving, near, to, ThingPlaceMode.Near))
                {
                    moving.Destroy();
                }
            }
        }
    }
}
