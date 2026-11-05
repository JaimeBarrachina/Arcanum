using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace Spatialist
{
    // Base for psycasts that use the caster's existing pocket. Only opening the gate is barred from other pocket maps
    // (Ability_PocketGate); these work from anywhere except, where pointless, from inside the caster's own pocket.
    public abstract class Ability_PocketUser : Ability
    {
        protected static WorldComponent_PocketDimensions Pockets => WorldComponent_PocketDimensions.Instance;

        protected virtual bool NeedsMap => true;

        protected virtual bool WorksInOwnPocket => false;

        protected virtual bool WorksInOtherPockets => true;

        public override bool IsEnabledForPawn(out string reason)
        {
            if (!Pockets.TryGetPocket(pawn, out PocketRecord record, out reason))
            {
                return false;
            }
            Map map = pawn.Map;
            if (NeedsMap && map == null)
            {
                reason = "Spatialist.NeedMap".Translate();
                return false;
            }
            if (map != null && map == record.map && !WorksInOwnPocket)
            {
                reason = "Spatialist.AlreadyInPocket".Translate();
                return false;
            }
            if (map != null && map != record.map && map.IsPocketMap && !WorksInOtherPockets)
            {
                reason = "Spatialist.MustBeOutside".Translate();
                return false;
            }
            return base.IsEnabledForPawn(out reason);
        }

        protected static bool IsStashable(Thing thing) =>
            thing != null && thing.Spawned && thing.def.category == ThingCategory.Item && thing.def.EverHaulable;
    }

    // Stash and Displace: outside, they skip items into the pocket. Inside the caster's own pocket they work in
    // reverse and skip items out next to the open gate.
    public abstract class Ability_ItemSkip : Ability_PocketUser
    {
        protected override bool WorksInOwnPocket => true;

        public override bool IsEnabledForPawn(out string reason)
        {
            if (!base.IsEnabledForPawn(out reason))
            {
                return false;
            }
            PocketRecord record = Pockets.RecordFor(pawn);
            if (!WorldComponent_PocketDimensions.TryGetItemDestination(record, pawn.Map, out _, out _))
            {
                reason = "Spatialist.GateClosedNoWayOut".Translate();
                return false;
            }
            return true;
        }

        protected int Send(IEnumerable<Thing> items)
        {
            if (!Pockets.TryGetPocket(pawn, out PocketRecord record, out _)
                || !WorldComponent_PocketDimensions.TryGetItemDestination(record, pawn.Map, out Map toMap, out IntVec3 landing))
            {
                return 0;
            }
            return Pockets.SendItems(items, toMap, landing);
        }
    }

    // Stash: skip one item stack into the pocket (or out of it).
    public class Ability_Stash : Ability_ItemSkip
    {
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages))
            {
                return false;
            }
            if (!IsStashable(target.Thing))
            {
                if (showMessages)
                {
                    Messages.Message("Spatialist.NotStashable".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return true;
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (targets.Length > 0 && IsStashable(targets[0].Thing))
            {
                Send(new[] { targets[0].Thing });
            }
        }
    }

    // Displace: skip every item stack in a 3x3 area into the pocket (or out of it).
    public class Ability_Displace : Ability_ItemSkip
    {
        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (targets.Length == 0)
            {
                return;
            }
            Map map = pawn.Map;
            var items = new List<Thing>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(targets[0].Cell, GetRadiusForPawn(), true))
            {
                if (cell.InBounds(map))
                {
                    items.AddRange(cell.GetThingList(map).Where(IsStashable));
                }
            }
            int sent = Send(items.Distinct());
            if (sent == 0)
            {
                Messages.Message("Spatialist.NothingToDisplace".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }
    }

    // Sanctuary: skip the caster and nearby allies (colonists, prisoners, colony animals and mechs) into the pocket.
    public class Ability_Sanctuary : Ability_PocketUser
    {
        // Open question: allowing it from other pocket maps would make it an escape from undercaves and dungeons.
        protected override bool WorksInOtherPockets => false;

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (!Pockets.TryGetPocket(pawn, out PocketRecord record, out _))
            {
                return;
            }
            IEnumerable<Pawn> pawns = GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, GetRadiusForPawn(), true)
                .OfType<Pawn>()
                .Where(p => p == pawn || p.Faction == pawn.Faction || p.IsPrisonerOfColony || p.HostFaction == pawn.Faction);
            Pockets.SendPawnsToPocket(record, pawns);
        }
    }

    // Expand Pocket: open the next ring of the pocket (15 -> 25 -> 35 -> 45).
    public class Ability_ExpandPocket : Ability_PocketUser
    {
        // Works on the pocket wherever the caster is, including from inside it or from a caravan.
        protected override bool NeedsMap => false;

        protected override bool WorksInOwnPocket => true;

        public override bool IsEnabledForPawn(out string reason)
        {
            if (!base.IsEnabledForPawn(out reason))
            {
                return false;
            }
            if (!Pockets.RecordFor(pawn).CanExpand)
            {
                reason = "Spatialist.PocketMaxed".Translate();
                return false;
            }
            return true;
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (Pockets.TryGetPocket(pawn, out PocketRecord record, out _) && Pockets.Expand(record))
            {
                Messages.Message("Spatialist.PocketExpanded".Translate(pawn.LabelShort, record.OpenSize), pawn, MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }

    // Spatial Pockets: passive. The effect lives in StatPart_Spatialist; the ability just marks it as learned.
    public class Ability_Passive : Ability
    {
        public override bool ShowGizmoOnPawn() => false;

        public override bool IsEnabledForPawn(out string reason)
        {
            reason = "Spatialist.Passive".Translate();
            return false;
        }
    }

    // Spatial Bag: fold a bag of space whose capacity is fixed by the caster's psychic sensitivity.
    public class Ability_SpatialBag : Ability
    {
        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            Map map = pawn.MapHeld;
            if (map == null)
            {
                return;
            }
            Thing bag = ThingMaker.MakeThing(SpatialistDefOf.Spatialist_SpatialBag);
            CompSpatialBag comp = bag.TryGetComp<CompSpatialBag>();
            if (comp != null)
            {
                comp.capacity = comp.Props.baseCapacity * pawn.GetStatValue(StatDefOf.PsychicSensitivity);
            }
            GenPlace.TryPlaceThing(bag, pawn.PositionHeld, map, ThingPlaceMode.Near);
            FleckMaker.Static(pawn.PositionHeld, map, FleckDefOf.PsycastSkipInnerExit, 1.5f);
        }
    }
}
