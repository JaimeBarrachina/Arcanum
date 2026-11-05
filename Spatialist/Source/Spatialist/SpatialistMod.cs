using HarmonyLib;
using RimWorld;
using Verse;

namespace Spatialist
{
    [StaticConstructorOnStartup]
    public static class SpatialistMod
    {
        static SpatialistMod()
        {
            new Harmony("darky.vpe.spatialist").PatchAll();
        }
    }

    [DefOf]
    public static class SpatialistDefOf
    {
        public static ThingDef Spatialist_PocketGate;
        public static ThingDef Spatialist_PocketWall;
        public static ThingDef Spatialist_LightCrystal;
        public static ThingDef Spatialist_SpatialBag;
        public static MapGeneratorDef Spatialist_PocketMap;
        public static VEF.Abilities.AbilityDef Spatialist_SpatialPockets;

        static SpatialistDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SpatialistDefOf));
        }
    }
}
