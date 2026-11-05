using HarmonyLib;
using RimWorld;
using Verse;

namespace Spatialist
{
    // Vanilla bans zones within 5 cells of any map edge, with no pocket-map exception: a 15x15 pocket would get a 5x5 stockpile.
    [HarmonyPatch(typeof(GenGrid), nameof(GenGrid.InNoZoneEdgeArea))]
    public static class Patch_GenGrid_InNoZoneEdgeArea
    {
        public static bool Prefix(Map map, ref bool __result)
        {
            if (WorldComponent_PocketDimensions.IsPocketDimension(map))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    // A pocket's roof is folded space, not rock: it never collapses. Without this, Expand Pocket removing a ring of
    // walls left the open middle out of range of any roof holder and brought the whole roof down.
    [HarmonyPatch(typeof(RoofCollapseCellsFinder), nameof(RoofCollapseCellsFinder.ProcessRoofHolderDespawned))]
    public static class Patch_RoofCollapse_ProcessRoofHolderDespawned
    {
        public static bool Prefix(Map map) => !WorldComponent_PocketDimensions.IsPocketDimension(map);
    }

    [HarmonyPatch(typeof(RoofCollapseCellsFinder), "CheckCollapseFlyingRoofAtAndAdjInternal")]
    public static class Patch_RoofCollapse_CheckCollapseFlyingRoof
    {
        public static bool Prefix(Map map, ref bool __result)
        {
            if (WorldComponent_PocketDimensions.IsPocketDimension(map))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    // Close open gates before the ship is captured, so nothing is left linked to a map about to be abandoned.
    [HarmonyPatch(typeof(WorldComponent_GravshipController), nameof(WorldComponent_GravshipController.InitiateTakeoff))]
    public static class Patch_GravshipController_InitiateTakeoff
    {
        public static void Prefix(Building_GravEngine engine)
        {
            WorldComponent_PocketDimensions.Instance?.Notify_GravshipTakeoff(engine);
        }
    }
}
