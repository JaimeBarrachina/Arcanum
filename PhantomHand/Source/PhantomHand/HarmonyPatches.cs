using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PhantomHand
{
    // Hands glide: terrain adds no movement cost. Things on the cell (rubble, furniture), snow and doors still count.
    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.GetPawnCellBaseCostOverride))]
    public static class Patch_PathFollower_BaseCostOverride
    {
        public static bool Prefix(Pawn pawn, ref int? __result)
        {
            if (pawn is Pawn_PhantomHand)
            {
                __result = 0;
                return false;
            }
            return true;
        }
    }

    // No letters or messages about hands arriving, being downed or dying.
    [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.ShouldSendNotificationAbout))]
    public static class Patch_PawnUtility_ShouldSendNotificationAbout
    {
        public static void Postfix(Pawn p, ref bool __result)
        {
            if (p is Pawn_PhantomHand)
            {
                __result = false;
            }
        }
    }

    // Vanilla rates every mech's work at race.mechFixedSkillLevel; use the hand's own summoned level instead.
    [HarmonyPatch(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn),
        new[] { typeof(Pawn), typeof(SkillDef), typeof(bool) })]
    public static class Patch_QualityUtility_GenerateQualityCreatedByPawn
    {
        public static bool Prefix(Pawn pawn, ref QualityCategory __result)
        {
            if (pawn is Pawn_PhantomHand hand)
            {
                __result = QualityUtility.GenerateQualityCreatedByPawn(hand.skillLevel, false);
                return false;
            }
            return true;
        }
    }

    // Hand races set mechFixedSkillLevel 20 so vanilla's skill gate always passes; this applies the hand's real level.
    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.CanConstruct),
        new[] { typeof(Thing), typeof(Pawn), typeof(bool), typeof(bool), typeof(JobDef) })]
    public static class Patch_GenConstruct_CanConstruct
    {
        public static void Postfix(Thing t, Pawn p, bool checkSkills, ref bool __result)
        {
            if (!__result || !checkSkills || !(p is Pawn_PhantomHand hand))
            {
                return;
            }
            if (hand.skillLevel < t.def.constructionSkillPrerequisite)
            {
                JobFailReason.Is("SkillTooLowForConstruction".Translate(SkillDefOf.Construction.LabelCap));
                __result = false;
            }
            else if (hand.skillLevel < t.def.artisticSkillPrerequisite)
            {
                JobFailReason.Is("SkillTooLowForConstruction".Translate(SkillDefOf.Artistic.LabelCap));
                __result = false;
            }
        }
    }
}
