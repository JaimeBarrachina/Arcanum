using HarmonyLib;
using RimWorld;
using Verse;

namespace PhantomHand
{
    [StaticConstructorOnStartup]
    public static class PhantomHandMod
    {
        static PhantomHandMod()
        {
            new Harmony("darky.vpe.phantomhand").PatchAll();
            StatPart_PhantomHand.InjectIntoStats();
            DetachPickUpAndHaul();
        }

        // Pick Up And Haul patches its comp onto the abstract BasePawn (patches run before inheritance), so hands inherit it
        // and, with its default "allow mechanoids", haul into their inventory. Its unload step is only triggered from think
        // nodes hands don't have, so they idled with full pockets until they vanished. Its work giver skips pawns without the
        // comp, so removing it makes hands haul the vanilla way, one stack at a time.
        private static void DetachPickUpAndHaul()
        {
            System.Type puahComp = GenTypes.GetTypeInAnyAssembly("PickUpAndHaul.CompHauledToInventory");
            if (puahComp == null)
            {
                return;
            }
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.thingClass != null && typeof(Pawn_PhantomHand).IsAssignableFrom(def.thingClass))
                {
                    def.comps?.RemoveAll(c => c.compClass == puahComp);
                }
            }
        }
    }

    [DefOf]
    public static class PhantomHandDefOf
    {
        public static HediffDef PH_Grasped;
        public static HediffDef PH_TelekineticAura;

        static PhantomHandDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PhantomHandDefOf));
        }
    }
}
