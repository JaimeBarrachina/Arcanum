using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PhantomHand
{
    public enum HandRole
    {
        Rescue,
        Fight,
        Work
    }

    // Gates a branch of PH_HandThinkTree on the hand's PhantomHandProps.
    public class ThinkNode_ConditionalHandRole : ThinkNode_Conditional
    {
        public HandRole role;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            ThinkNode_ConditionalHandRole copy = (ThinkNode_ConditionalHandRole)base.DeepCopy(resolve);
            copy.role = role;
            return copy;
        }

        protected override bool Satisfied(Pawn pawn)
        {
            if (!(pawn is Pawn_PhantomHand hand))
            {
                return false;
            }
            PhantomHandProps props = hand.Props;
            bool threat = props.switchOnThreat && ThreatCache.AnyThreat(pawn.Map);
            switch (role)
            {
                case HandRole.Rescue:
                    return props.rescue && (!props.switchOnThreat || threat);
                case HandRole.Fight:
                    return props.fight && (!props.switchOnThreat || threat);
                case HandRole.Work:
                    return props.work && (!props.switchOnThreat || !threat);
                default:
                    return false;
            }
        }
    }

    // GenHostility.AnyHostileActiveThreatToPlayer walks every hostile; a swarm of 40 hands shouldn't ask 40 times a tick.
    public static class ThreatCache
    {
        private static readonly Dictionary<int, (int tick, bool threat)> cache = new Dictionary<int, (int, bool)>();

        public static bool AnyThreat(Map map)
        {
            if (map == null)
            {
                return false;
            }
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(map.uniqueID, out var entry) && now - entry.tick < 120)
            {
                return entry.threat;
            }
            bool threat = GenHostility.AnyHostileActiveThreatToPlayer(map);
            cache[map.uniqueID] = (now, threat);
            return threat;
        }
    }

    // Vanilla JobGiver_RescueNearby refuses whenever an enemy is within 25 cells, which is exactly when a combat rescuer
    // is wanted. This one searches the whole map and ignores nearby enemies.
    public class JobGiver_PhantomRescue : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn patient = (Pawn)GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Pawn), PathEndMode.OnCell, TraverseParms.For(pawn),
                9999f, t => NeedsRescue(pawn, (Pawn)t));
            if (patient == null)
            {
                return null;
            }
            Building_Bed bed = RestUtility.FindBedFor(patient, pawn, checkSocialProperness: false,
                ignoreOtherReservations: false, patient.GuestStatus);
            if (bed == null || !patient.CanReserve(bed))
            {
                return null;
            }
            Job job = JobMaker.MakeJob(JobDefOf.Rescue, patient, bed);
            job.count = 1;
            return job;
        }

        private static bool NeedsRescue(Pawn rescuer, Pawn patient)
        {
            if (patient == rescuer || patient is Pawn_PhantomHand || !patient.Downed || patient.InBed()
                || !patient.RaceProps.Humanlike)
            {
                return false;
            }
            bool ours = patient.Faction == rescuer.Faction
                || (patient.HostFaction == rescuer.Faction && !patient.IsPrisoner);
            return ours && HealthAIUtility.WantsToBeRescued(patient)
                && rescuer.CanReserve(patient, 1, -1, null, false);
        }
    }

    // Idle hands hover around their caster rather than wandering the whole colony.
    public class JobGiver_WanderNearCaster : JobGiver_Wander
    {
        public JobGiver_WanderNearCaster()
        {
            wanderRadius = 5f;
            ticksBetweenWandersRange = new IntRange(90, 250);
        }

        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            Pawn caster = (pawn as Pawn_PhantomHand)?.caster;
            return caster != null && caster.Spawned && caster.Map == pawn.Map ? caster.Position : pawn.Position;
        }
    }
}
