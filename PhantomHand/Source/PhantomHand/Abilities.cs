using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace PhantomHand
{
    // Busy Hands, Timber Hands, Construction Crew, Rescue Hands, Thousand Palms, The Hundred-Handed.
    public class Ability_SummonHands : Ability
    {
        public override bool IsEnabledForPawn(out string reason)
        {
            if (pawn.Map == null)
            {
                reason = "PH.NeedMap".Translate();
                return false;
            }
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            PhantomSummoner.Summon(pawn, def.GetModExtension<PhantomSummonExtension>());
        }
    }

    // Seize every hostile pawn in the targeted area. No hands are spawned; the grip is a hediff.
    public class Ability_GraspingHands : Ability
    {
        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            Map map = pawn.Map;
            if (map == null || targets.Length == 0)
            {
                return;
            }
            IntVec3 center = targets[0].Cell;
            int duration = GetDurationForPawn();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(center, map, GetRadiusForPawn(), true))
            {
                if (thing is Pawn victim && !victim.Dead && victim.HostileTo(pawn))
                {
                    ApplyHediff(victim, PhantomHandDefOf.PH_Grasped, null, duration, 1f);
                    FleckMaker.Static(victim.DrawPos, map, FleckDefOf.PsycastSkipInnerExit, 0.7f);
                }
            }
        }
    }

    // Toggle: cast to start the aura, use again (free) to end it.
    public class Ability_TelekineticAura : Ability
    {
        private Hediff ActiveAura => pawn.health.hediffSet.GetFirstHediffOfDef(PhantomHandDefOf.PH_TelekineticAura);

        public override void DoAction()
        {
            Hediff aura = ActiveAura;
            if (aura != null && Event.current.button == 0)
            {
                pawn.health.RemoveHediff(aura);
                return;
            }
            base.DoAction();
        }

        public override bool IsEnabledForPawn(out string reason)
        {
            if (ActiveAura != null)
            {
                reason = "";
                return true;
            }
            if (pawn.Map == null)
            {
                reason = "PH.NeedMap".Translate();
                return false;
            }
            int lockHeat = PhantomHandDefOf.PH_TelekineticAura.GetModExtension<AuraExtension>()?.minHeat ?? 0;
            Pawn_PsychicEntropyTracker entropy = pawn.psychicEntropy;
            if (entropy != null && entropy.MaxEntropy - entropy.EntropyValue < lockHeat)
            {
                reason = "PH.AuraNoHeatRoom".Translate(lockHeat);
                return false;
            }
            return base.IsEnabledForPawn(out reason);
        }

        public override string GetDescriptionForPawn()
        {
            string text = base.GetDescriptionForPawn();
            return ActiveAura != null ? text + "\n\n" + "PH.AuraActive".Translate().Colorize(ColoredText.TipSectionTitleColor) : text;
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (ActiveAura == null)
            {
                pawn.health.AddHediff(PhantomHandDefOf.PH_TelekineticAura);
            }
        }
    }
}
