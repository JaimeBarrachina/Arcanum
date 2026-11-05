using System.Collections.Generic;
using Verse;

namespace PhantomHand
{
    // On a hand's race ThingDef: which branches of PH_HandThinkTree it runs. Work types come from race.mechEnabledWorkTypes.
    public class PhantomHandProps : DefModExtension
    {
        public bool rescue;
        public bool fight;
        public bool work;
        // The Hundred-Handed: rescue and fight only while the map has an active threat, work only while it doesn't.
        public bool switchOnThreat;

        public static readonly PhantomHandProps Default = new PhantomHandProps();
    }

    // On a summoning AbilityDef. Every curve takes the caster's psychic sensitivity; the curve's end points are the caps.
    public class PhantomSummonExtension : DefModExtension
    {
        public PawnKindDef kind;
        public int durationTicks = 2500;
        public SimpleCurve count;
        public SimpleCurve skill;
        public SimpleCurve speed;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (kind == null)
            {
                yield return "PhantomSummonExtension has no kind";
            }
            if (count == null || skill == null || speed == null)
            {
                yield return "PhantomSummonExtension needs count, skill and speed curves";
            }
        }
    }

    // On PH_TelekineticAura's HediffDef.
    public class AuraExtension : DefModExtension
    {
        // Summoning psycasts whose PhantomSummonExtension the aura draws hands from.
        public List<VEF.Abilities.AbilityDef> sources = new List<VEF.Abilities.AbilityDef>();
        public int intervalTicks = 1200;
        public SimpleCurve maxHands;
        public int minHeat = 30;
    }
}
