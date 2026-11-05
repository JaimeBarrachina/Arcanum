using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Spatialist
{
    public class Building_PocketGate : MapPortal
    {
        private static readonly CachedTexture CloseTex = new CachedTexture("UI/Designators/Cancel");

        public Pawn owner;

        public override string LabelNoCount =>
            owner != null ? "Spatialist.GateLabel".Translate(owner.LabelShort).Resolve() : base.LabelNoCount;

        public void LinkTo(Map pocket, PocketMapExit pocketExit)
        {
            pocketMap = pocket;
            exit = pocketExit;
            if (pocketExit != null)
            {
                pocketExit.entrance = this;
            }
        }

        // The pocket is created by WorldComponent_PocketDimensions, never lazily by vanilla on first entry.
        public override Map GetOtherMap() => PocketMap;

        protected override Map GeneratePocketMapInt() => PocketMap;

        public override bool IsEnterable(out string reason)
        {
            if (PocketMap == null || exit == null)
            {
                reason = "Spatialist.PocketMissing".Translate();
                return false;
            }
            return base.IsEnterable(out reason);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (Faction == Faction.OfPlayer)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Spatialist.CloseGate".Translate(),
                    defaultDesc = "Spatialist.CloseGateDesc".Translate(),
                    icon = CloseTex.Texture,
                    action = Close
                };
            }
        }

        public void Close()
        {
            WorldComponent_PocketDimensions pockets = WorldComponent_PocketDimensions.Instance;
            PocketRecord record = pockets.RecordFor(owner);
            if (record != null && record.gate == this)
            {
                pockets.CloseGate(record);
            }
            else
            {
                WorldComponent_PocketDimensions.Vanish(this);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "owner");
        }
    }
}
