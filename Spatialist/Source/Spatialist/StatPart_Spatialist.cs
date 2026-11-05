using System.Text;
using RimWorld;
using VEF.Abilities;
using Verse;

namespace Spatialist
{
    // Spatial Pockets is a passive psycast: VPE has no passives, so the bonus lives on the stat itself and
    // simply checks whether the pawn has learned it. The same part adds a worn Spatial Bag's stored capacity.
    public class StatPart_Spatialist : StatPart
    {
        public float pocketsOffset;
        public bool countBags;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!(req.Thing is Pawn pawn))
            {
                return;
            }
            if (HasPockets(pawn))
            {
                val += pocketsOffset;
            }
            if (countBags)
            {
                val += WornBagCapacity(pawn);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn))
            {
                return null;
            }
            var sb = new StringBuilder();
            if (HasPockets(pawn))
            {
                sb.AppendLine("Spatialist.StatPockets".Translate() + ": " + parentStat.ValueToString(pocketsOffset, ToStringNumberSense.Offset));
            }
            float bag = countBags ? WornBagCapacity(pawn) : 0f;
            if (bag > 0f)
            {
                sb.AppendLine("Spatialist.StatBag".Translate() + ": " + parentStat.ValueToString(bag, ToStringNumberSense.Offset));
            }
            return sb.Length > 0 ? sb.ToString().TrimEndNewlines() : null;
        }

        private static bool HasPockets(Pawn pawn) =>
            pawn.GetComp<CompAbilities>()?.HasAbility(SpatialistDefOf.Spatialist_SpatialPockets) == true;

        private static float WornBagCapacity(Pawn pawn)
        {
            var worn = pawn.apparel?.WornApparel;
            if (worn == null)
            {
                return 0f;
            }
            float total = 0f;
            for (int i = 0; i < worn.Count; i++)
            {
                CompSpatialBag bag = worn[i].TryGetComp<CompSpatialBag>();
                if (bag != null)
                {
                    total += bag.capacity;
                }
            }
            return total;
        }
    }
}
