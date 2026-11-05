using Verse;

namespace Spatialist
{
    public class CompProperties_SpatialBag : CompProperties
    {
        public float baseCapacity = 100f;

        public CompProperties_SpatialBag()
        {
            compClass = typeof(CompSpatialBag);
        }
    }

    // A bag's capacity is fixed when it's made (base capacity x its maker's psychic sensitivity) and travels with it.
    public class CompSpatialBag : ThingComp
    {
        public float capacity;

        public CompProperties_SpatialBag Props => (CompProperties_SpatialBag)props;

        public override void PostPostMake()
        {
            base.PostPostMake();
            capacity = Props.baseCapacity;
        }

        public override string TransformLabel(string label) => "Spatialist.BagLabel".Translate(label, capacity.ToString("F0"));

        public override string CompInspectStringExtra() => "Spatialist.BagInspect".Translate(capacity.ToString("F0"));

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref capacity, "capacity");
        }
    }
}
