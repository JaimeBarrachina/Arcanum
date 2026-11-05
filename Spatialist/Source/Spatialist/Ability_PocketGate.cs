using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace Spatialist
{
    // Closed: target a cell to open the gate there. Open: click to close it, free and instant.
    public class Ability_PocketGate : Ability
    {
        private PocketRecord Record => WorldComponent_PocketDimensions.Instance.RecordFor(pawn);

        private bool InOwnPocket(PocketRecord record) =>
            record != null && pawn.Map != null && pawn.Map == record.map;

        public override void DoAction()
        {
            if (Event.current.button == 0)
            {
                PocketRecord record = Record;
                if (record != null && record.GateOpen)
                {
                    WorldComponent_PocketDimensions.Instance.CloseGate(record);
                    return;
                }
                if (InOwnPocket(record))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Spatialist.EvacuateConfirm".Translate(pawn.LabelShort),
                        () => WorldComponent_PocketDimensions.Instance.Evacuate(record)));
                    return;
                }
            }
            base.DoAction();
        }

        public override bool IsEnabledForPawn(out string reason)
        {
            PocketRecord record = Record;
            if (record != null && record.GateOpen || InOwnPocket(record))
            {
                reason = "";
                return true;
            }
            if (pawn.Map == null)
            {
                reason = "Spatialist.NeedMap".Translate();
                return false;
            }
            if (pawn.Map.IsPocketMap)
            {
                reason = "Spatialist.CantOpenInPocket".Translate();
                return false;
            }
            return base.IsEnabledForPawn(out reason);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages))
            {
                return false;
            }
            Map map = pawn.Map;
            IntVec3 cell = target.Cell;
            if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map) || cell.GetFirstBuilding(map) != null)
            {
                if (showMessages)
                {
                    Messages.Message("Spatialist.GateBlocked".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            return true;
        }

        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            Map map = pawn.Map;
            if (targets.Length == 0 || map == null || map.IsPocketMap)
            {
                return;
            }
            WorldComponent_PocketDimensions.Instance.OpenGate(pawn, targets[0].Cell, map);
        }
    }
}
