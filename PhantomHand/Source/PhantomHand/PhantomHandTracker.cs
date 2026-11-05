using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PhantomHand
{
    // Every spawned hand, for counting and cleanup. Not saved: hands register again in SpawnSetup on load.
    public class PhantomHandTracker : GameComponent
    {
        private readonly List<Pawn_PhantomHand> hands = new List<Pawn_PhantomHand>();
        private readonly List<Pawn_PhantomHand> despawned = new List<Pawn_PhantomHand>();

        public static PhantomHandTracker Instance => Current.Game?.GetComponent<PhantomHandTracker>();

        public PhantomHandTracker(Game game)
        {
        }

        public void Register(Pawn_PhantomHand hand)
        {
            despawned.Remove(hand);
            if (!hands.Contains(hand))
            {
                hands.Add(hand);
            }
        }

        // A hand leaving its map (caravan, gravship, pod, portal) vanishes on the next check instead of travelling.
        public void Deregister(Pawn_PhantomHand hand)
        {
            if (hands.Remove(hand) && !despawned.Contains(hand))
            {
                despawned.Add(hand);
            }
        }

        public List<Pawn_PhantomHand> HandsOf(Pawn caster, PawnKindDef kind = null, bool? fromAura = null) =>
            hands.Where(h => h.caster == caster && (kind == null || h.kindDef == kind)
                && (fromAura == null || h.fromAura == fromAura.Value)).ToList();

        public override void GameComponentTick()
        {
            if (despawned.Count == 0 || Find.TickManager.TicksGame % 60 != 0)
            {
                return;
            }
            for (int i = despawned.Count - 1; i >= 0; i--)
            {
                Pawn_PhantomHand hand = despawned[i];
                despawned.RemoveAt(i);
                // Killed hands are already handled by their death action.
                if (!hand.Destroyed && !hand.Dead && !hand.Spawned)
                {
                    hand.Vanish();
                }
            }
        }
    }
}
