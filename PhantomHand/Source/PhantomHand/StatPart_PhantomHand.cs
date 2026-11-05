using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PhantomHand
{
    // Mechs have no skill tracker, so vanilla falls back to noSkillOffset / noSkillFactor for every skill-based stat.
    // For hands, swap those for the stat's own skill needs evaluated at the hand's summoned skill level, so a level-8 hand
    // builds, cuts and fights like a level-8 colonist. MoveSpeed gets the summon's speed factor.
    public class StatPart_PhantomHand : StatPart
    {
        private static readonly AccessTools.FieldRef<SkillNeed_BaseBonus, float> BaseValue =
            AccessTools.FieldRefAccess<SkillNeed_BaseBonus, float>("baseValue");
        private static readonly AccessTools.FieldRef<SkillNeed_BaseBonus, float> BonusPerLevel =
            AccessTools.FieldRefAccess<SkillNeed_BaseBonus, float>("bonusPerLevel");

        public static void InjectIntoStats()
        {
            foreach (StatDef stat in DefDatabase<StatDef>.AllDefsListForReading)
            {
                if (stat.skillNeedFactors.NullOrEmpty() && stat.skillNeedOffsets.NullOrEmpty() && stat != StatDefOf.MoveSpeed)
                {
                    continue;
                }
                if (stat.parts == null)
                {
                    stat.parts = new List<StatPart>();
                }
                stat.parts.Add(new StatPart_PhantomHand { parentStat = stat });
            }
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!(req.Thing is Pawn_PhantomHand hand))
            {
                return;
            }
            if (parentStat == StatDefOf.MoveSpeed)
            {
                val *= hand.speedFactor;
                return;
            }
            int level = hand.skillLevel;
            if (!parentStat.skillNeedOffsets.NullOrEmpty())
            {
                val -= parentStat.noSkillOffset;
                foreach (SkillNeed need in parentStat.skillNeedOffsets)
                {
                    val += ValueAt(need, level, 0f);
                }
            }
            if (!parentStat.skillNeedFactors.NullOrEmpty())
            {
                if (parentStat.noSkillFactor != 0f)
                {
                    val /= parentStat.noSkillFactor;
                }
                foreach (SkillNeed need in parentStat.skillNeedFactors)
                {
                    val *= ValueAt(need, level, 1f);
                }
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn_PhantomHand hand))
            {
                return null;
            }
            if (parentStat == StatDefOf.MoveSpeed)
            {
                return "PH.StatSpeed".Translate() + ": x" + hand.speedFactor.ToStringPercent();
            }
            return "PH.StatSkill".Translate(hand.skillLevel);
        }

        private static float ValueAt(SkillNeed need, int level, float fallback)
        {
            switch (need)
            {
                case SkillNeed_BaseBonus baseBonus:
                    return BaseValue(baseBonus) + BonusPerLevel(baseBonus) * level;
                case SkillNeed_Direct direct when direct.valuesPerLevel.Count > 0:
                    return direct.valuesPerLevel[System.Math.Min(level, direct.valuesPerLevel.Count - 1)];
                default:
                    return fallback;
            }
        }
    }
}
