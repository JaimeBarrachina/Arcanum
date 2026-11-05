using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using VanillaPsycastsExpanded;
using Verse;

namespace PhantomHand
{
    // While the aura lasts, VPE holds the caster's neural heat at or above MinHeat (IMinHeatGiver), and a random working
    // hand is summoned every interval, up to a cap. Ends if the caster is downed, dies or leaves the map.
    public class Hediff_TelekineticAura : HediffWithComps, IMinHeatGiver
    {
        private Map startMap;

        public AuraExtension Ext => def.GetModExtension<AuraExtension>();

        public bool IsActive => pawn != null && !pawn.Dead && pawn.health.hediffSet.hediffs.Contains(this);

        public int MinHeat => Ext?.minHeat ?? 0;

        public override bool ShouldRemove =>
            base.ShouldRemove || pawn.Downed || !pawn.Spawned || (startMap != null && pawn.Map != startMap);

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            startMap = pawn.Map;
            pawn.Psycasts()?.AddMinHeatGiver(this);
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            AuraExtension ext = Ext;
            if (ext == null || !pawn.Spawned || !pawn.IsHashIntervalTick(ext.intervalTicks, delta))
            {
                return;
            }
            float sensitivity = pawn.GetStatValue(StatDefOf.PsychicSensitivity);
            int cap = Mathf.RoundToInt(ext.maxHands?.Evaluate(sensitivity) ?? 3f);
            if (PhantomHandTracker.Instance.HandsOf(pawn, fromAura: true).Count >= cap)
            {
                return;
            }
            List<PhantomSummonExtension> useful = ext.sources
                .Select(a => a.GetModExtension<PhantomSummonExtension>())
                .Where(s => s?.kind != null && HasWorkFor(s.kind, pawn.Map))
                .ToList();
            if (useful.TryRandomElement(out PhantomSummonExtension pick))
            {
                PhantomSummoner.SpawnHand(pawn, pick, PhantomSummoner.SkillFor(pick, sensitivity),
                    pick.speed.Evaluate(sensitivity), fromAura: true);
            }
        }

        // Cheap checks, so the aura doesn't summon cutters on a map with nothing to cut.
        private static bool HasWorkFor(PawnKindDef kind, Map map)
        {
            List<WorkTypeDef> types = kind.race.race.mechEnabledWorkTypes;
            if (types.NullOrEmpty())
            {
                return false;
            }
            foreach (WorkTypeDef type in types)
            {
                if (type == WorkTypeDefOf.Hauling && map.listerHaulables.ThingsPotentiallyNeedingHauling().Count > 0)
                {
                    return true;
                }
                if (type == WorkTypeDefOf.PlantCutting
                    && (map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.CutPlant)
                        || map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.HarvestPlant)))
                {
                    return true;
                }
                if (type == WorkTypeDefOf.Mining
                    && (map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.Mine)
                        || map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.MineVein)))
                {
                    return true;
                }
                if (type == WorkTypeDefOf.Construction
                    && (map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Count > 0
                        || map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Count > 0
                        || map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer).Count > 0))
                {
                    return true;
                }
            }
            return false;
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            foreach (Pawn_PhantomHand hand in PhantomHandTracker.Instance?.HandsOf(pawn, fromAura: true)
                ?? new List<Pawn_PhantomHand>())
            {
                hand.Vanish();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref startMap, "startMap");
        }
    }
}
