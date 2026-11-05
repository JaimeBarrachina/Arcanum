using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PhantomHand
{
    public static class PhantomSummoner
    {
        // Recasting refreshes the caster's existing hands of that kind and tops the count up; it never stacks past the curve.
        public static void Summon(Pawn caster, PhantomSummonExtension ext)
        {
            if (caster?.Map == null || ext?.kind == null)
            {
                return;
            }
            float sensitivity = caster.GetStatValue(StatDefOf.PsychicSensitivity);
            int count = Mathf.Max(1, Mathf.RoundToInt(ext.count.Evaluate(sensitivity)));
            int skill = SkillFor(ext, sensitivity);
            float speed = ext.speed.Evaluate(sensitivity);

            List<Pawn_PhantomHand> existing = PhantomHandTracker.Instance.HandsOf(caster, ext.kind, fromAura: false);
            foreach (Pawn_PhantomHand hand in existing)
            {
                hand.Refresh(ext.durationTicks, skill, speed);
            }
            for (int i = existing.Count; i < count; i++)
            {
                SpawnHand(caster, ext, skill, speed, fromAura: false);
            }
        }

        public static int SkillFor(PhantomSummonExtension ext, float sensitivity) =>
            Mathf.Clamp(Mathf.RoundToInt(ext.skill.Evaluate(sensitivity)), 0, 20);

        public static Pawn_PhantomHand SpawnHand(Pawn caster, PhantomSummonExtension ext, int skill, float speed, bool fromAura)
        {
            Map map = caster.Map;
            if (!(PawnGenerator.GeneratePawn(ext.kind, caster.Faction) is Pawn_PhantomHand hand))
            {
                Log.ErrorOnce($"[Phantom Hand] {ext.kind} doesn't generate a Pawn_PhantomHand; check its race's thingClass.",
                    ext.kind.shortHash);
                return null;
            }
            hand.Init(caster, ext.durationTicks, skill, speed, fromAura);
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(caster.Position, map, 2);
            GenSpawn.Spawn(hand, cell, map);
            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipFlashEntry, 0.8f);
            return hand;
        }
    }
}
