using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PhantomHand
{
    // A temporary telekinetic hand. Its race is a mechanoid with no overseer comp: that makes it IsColonyMech, so
    // vanilla work givers accept it and mechEnabledWorkTypes limits what it does, with no mechanitor involved.
    public class Pawn_PhantomHand : Pawn
    {
        // A hand carrying a pawn when it runs out gets this long to reach the bed before it drops them.
        private const int CarryGraceTicks = 600;

        public Pawn caster;
        public int ticksLeft = 2500;
        public int skillLevel = 8;
        public float speedFactor = 1f;
        public bool fromAura;
        private int graceLeft = CarryGraceTicks;
        private bool vanishing;

        public PhantomHandProps Props => def.GetModExtension<PhantomHandProps>() ?? PhantomHandProps.Default;

        public void Init(Pawn caster, int durationTicks, int skillLevel, float speedFactor, bool fromAura)
        {
            this.caster = caster;
            this.fromAura = fromAura;
            ticksLeft = durationTicks;
            Refresh(durationTicks, skillLevel, speedFactor);
        }

        public void Refresh(int durationTicks, int skillLevel, float speedFactor)
        {
            ticksLeft = Mathf.Max(ticksLeft, durationTicks);
            this.skillLevel = skillLevel;
            this.speedFactor = speedFactor;
            graceLeft = CarryGraceTicks;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            // Vanilla only creates work settings for mechs with an overseer.
            if (workSettings == null && !RaceProps.mechEnabledWorkTypes.NullOrEmpty())
            {
                workSettings = new Pawn_WorkSettings(this);
                workSettings.EnableAndInitialize();
            }
            PhantomHandTracker.Instance?.Register(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            PhantomHandTracker.Instance?.Deregister(this);
            base.DeSpawn(mode);
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Spawned || vanishing)
            {
                return;
            }
            if (this.IsHashIntervalTick(30, delta) && !CasterHolds())
            {
                Vanish();
                return;
            }
            // Inventory-hauling mods can leave an idle hand with full pockets. Set things down; hauling then stores them.
            if (this.IsHashIntervalTick(250, delta) && inventory != null && inventory.innerContainer.Count > 0
                && (CurJob == null || CurJob.def.isIdle || CurJob.def == JobDefOf.GotoWander))
            {
                inventory.DropAllNearPawn(Position);
            }
            ticksLeft -= delta;
            if (ticksLeft > 0)
            {
                return;
            }
            if (carryTracker?.CarriedThing is Pawn && graceLeft > 0)
            {
                graceLeft -= delta;
                return;
            }
            Vanish();
        }

        // The hand lasts only while its caster is alive, conscious and on the same map.
        private bool CasterHolds() =>
            caster != null && !caster.Dead && !caster.Downed && caster.MapHeld == Map;

        // Drop whatever it holds, then disappear without a corpse. Pawn.Destroy doesn't drop carried things.
        public void Vanish()
        {
            if (vanishing || Destroyed)
            {
                return;
            }
            vanishing = true;
            if (Spawned)
            {
                Map map = Map;
                IntVec3 pos = Position;
                carryTracker?.TryDropCarriedThing(pos, ThingPlaceMode.Near, out _);
                inventory?.DropAllNearPawn(pos);
                FleckMaker.Static(DrawPos, map, FleckDefOf.PsycastSkipInnerExit, 0.6f);
            }
            Destroy(DestroyMode.Vanish);
            if (!Discarded)
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(this);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            yield return new Command_Action
            {
                defaultLabel = "PH.Dismiss".Translate(),
                defaultDesc = "PH.DismissDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                action = Vanish
            };
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            string mine = "PH.HandInspect".Translate(Mathf.Max(ticksLeft, 0).ToStringTicksToPeriod(), skillLevel,
                caster?.LabelShort ?? "?");
            return text.NullOrEmpty() ? mine : text + "\n" + mine;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 2500);
            Scribe_Values.Look(ref skillLevel, "skillLevel", 8);
            Scribe_Values.Look(ref speedFactor, "speedFactor", 1f);
            Scribe_Values.Look(ref fromAura, "fromAura");
            Scribe_Values.Look(ref graceLeft, "graceLeft", CarryGraceTicks);
        }
    }
}
