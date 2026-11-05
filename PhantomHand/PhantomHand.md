# VPE - Phantom Hand

A Vanilla Psycasts Expanded path that summons short-lived telekinetic hands. The hands work, rescue or fight on their own: they're very fast, they ignore terrain movement cost, and they vanish after a short time. Inspired by the summons in A RimWorld of Magic: the minions that haul and clean, and the spirit wolves that fight.

Status: **all 9 psycasts built on 2026-11-03 (Miner Hands added the same day). It compiles and the XML resolves. Playtest round 1 (Busy Hands) ran on 2026-11-03; see the playtest log. Miner Hands is untested.** Final art (icons, hand sprite, path background) was done the same day; the Workshop preview and mod icon are still to do. Published on the Steam Workshop 2026-11-05 as **3834210546** (https://steamcommunity.com/sharedfiles/filedetails/?id=3834210546). Target: RimWorld 1.6. Tab: `Arcanum` (shared with Spatialist; was `Arcane` until 2026-11-05).

General paths, tooling and research notes live in [../Private/Mods.md](../Private/Mods.md) (local only, not in the repo; sections "Temporary / autonomous worker pawns" and "Vanilla 1.6 summoned-worker research").

## Layout

| Path | What |
|---|---|
| `About/About.xml` | packageId `darky.vpe.phantomhand`; depends on Harmony, VEF, VPE, **Biotech** |
| `1.6/Defs/PsycasterPathDefs/PhantomHand_Path.xml` | the path (`PH_PhantomHandPath`, tab Arcanum) |
| `1.6/Defs/AbilityDefs/PhantomHand_Abilities.xml` | the 9 psycasts, each summon with its `PhantomSummonExtension` curves |
| `1.6/Defs/ThingDefs_Races/PhantomHand_Races.xml` | `PH_HandBase` plus 6 hand races and kinds |
| `1.6/Defs/ThinkTreeDefs/PhantomHand_ThinkTree.xml` | `PH_HandThinkTree` |
| `1.6/Defs/HediffDefs/PhantomHand_Hediffs.xml` | `PH_Grasped`, `PH_TelekineticAura` (with `AuraExtension`) |
| `1.6/Defs/Combat/PhantomHand_Shove.xml` | shove tool capacity, maneuver and damage (VEF push-back) |
| `Languages/English/Keyed/PhantomHand.xml` | UI strings |
| `Textures/PhantomHand/` | final art (see Art below) |
| `Art/` | Codex jobs, raw output, references, splits and finals. **Leave it out of a Workshop upload** (16 MB). |
| `Source/PhantomHand/` | C# (`dotnet build Source/PhantomHand/PhantomHand.csproj -c Release` writes to `1.6/Assemblies`) |

The folder is junctioned into `RimWorld\Mods\PhantomHand`.

## Psycast tree

| Lvl | Slot | Psycast | Needs | Hand kind | What it does |
|---|---|---|---|---|---|
| 1 | 0 | Busy Hands | none | `PH_BusyHand` | Hauling hands that follow the colony's hauling priorities. 2-6 hands, lasting 2500 ticks. |
| 2 | -29 | Timber Hands | Busy Hands | `PH_TimberHand` | PlantCutting. 2-6 hands, lasting 2500 ticks. |
| 3 | -51 | Phantom Construction Crew | Timber Hands | `PH_BuilderHand` | Construction (building, delivering materials, repair). 2-6 hands, lasting 2500 ticks. |
| 3 | 0 | Miner Hands (added 2026-11-03) | Timber Hands | `PH_MinerHand` | Mining (designated rock and veins; vanilla bars mechs from deep drills). 2-6 hands, lasting 2500 ticks. |
| 4 | -29 | Telekinetic Aura | Construction Crew **or** Miner Hands | (from the 4 work kinds) | Toggle that locks 30 heat. Every 1200 ticks it summons a hauler, cutter, builder or miner for work that's waiting, up to 2-6 hands. |
| 2 | +29 | Rescue Hands | Busy Hands | `PH_RescueHand` | Carries downed allies to beds anywhere on the map, even with enemies near. 1-4 hands, lasting 1250 ticks. |
| 3 | +51 | Grasping Hands | Rescue Hands | none | Applies `PH_Grasped` to hostile pawns within radius 3.9: Moving capped at 0.2, Manipulation ×0.6, aiming delay ×1.5. Lasts 600 ticks × sensitivity. |
| 4 | +29 | Thousand Palms | Grasping Hands | `PH_PalmHand` | Fighting hands, each with a projectile shield (radius 2, 60 HP), fast blows and a shove. 3-8 hands, lasting 1500 ticks. |
| 5 | 0 | The Hundred-Handed | Aura **or** Palms | `PH_HundredHand` | Does every task: it fights and rescues during a threat, and hauls, cuts, builds and mines otherwise. 14-40 hands, lasting 2000 ticks; 60000-tick cooldown. |

**The capstone is any-one by decision (2026-11-03).** The user preferred whatever was simplest, so VPE's native any-one prerequisite stands and there is no patch.

**Card layout:** rows 2 and 4 hold pairs at ±29 (work on the left, combat on the right). Row 3 holds three: Construction -51, Miner 0, Grasping +51 (`order` 1/2/3). The root and capstone sit at 0. Lines: Timber to Construction and Miner, both to Aura, Rescue to Grasping to Palms. Nothing crosses or runs under an icon.

**Scaling:** every summon curve takes the caster's psychic sensitivity, and the curve's end points are the caps. Skill goes from 4 to 15 (combat 5-16, Hundred 6-17). The speed factor goes from 0.85 to 1.3 on a base MoveSpeed of 8. Recasting refreshes the duration, skill and speed of that caster's existing hands of that kind and tops up the count. It never stacks beyond the curve.

## Code map

- **`Pawn_PhantomHand : Pawn`** saves `caster`, `ticksLeft`, `skillLevel`, `speedFactor`, `fromAura` and `graceLeft`.
  - At spawn it creates `workSettings`, because vanilla only does that for mechs with an overseer.
  - Every 30 ticks it vanishes if the caster is dead, downed or on another map.
  - At expiry it vanishes. A hand carrying a pawn first gets a 600-tick grace.
  - `Vanish()` drops what the hand carries, plays a fleck, then calls `Destroy(Vanish)` and discards the pawn through the GC.
  - It has a Dismiss gizmo and an inspect line.
- **`PhantomHandTracker : GameComponent`** is the unsaved registry of spawned hands, rebuilt from `SpawnSetup`. Hands that despawn without being destroyed or killed (caravan, gravship, pod, portal) vanish within 60 ticks.
- **`PhantomSummoner`** does the curve math, refresh-and-top-up, and spawns hands near the caster.
- **`StatPart_PhantomHand`** is injected at startup into every StatDef that has skill needs, plus MoveSpeed. For hands it removes vanilla's `noSkillOffset`/`noSkillFactor` and applies the stat's own `SkillNeed_BaseBonus`/`SkillNeed_Direct` at the hand's level. MoveSpeed gets the speed factor.
- **`ThinkNode_ConditionalHandRole`** (Rescue/Fight/Work, from `PhantomHandProps` on the race; `switchOnThreat` uses a 120-tick `ThreatCache`), plus `JobGiver_PhantomRescue` (map-wide, ignores nearby enemies, humanlike same-faction or guest) and `JobGiver_WanderNearCaster`.
- **Abilities:** `Ability_SummonHands`, `Ability_GraspingHands` (hostile-only area, uses VEF `ApplyHediff`), `Ability_TelekineticAura` (toggle; using it again is free; it refuses when there isn't 30 heat of headroom).
- **`Hediff_TelekineticAura`** implements VPE `IMinHeatGiver` for the heat lock. It summons from the `AuraExtension.sources` abilities, skipping kinds that have no waiting work. It ends when the caster is downed, despawned or off its starting map, and its hands vanish with it.
- **Harmony:**
  - `Pawn_PathFollower.GetPawnCellBaseCostOverride` returns 0 for hands, so terrain costs nothing; things, snow and doors still count.
  - `PawnUtility.ShouldSendNotificationAbout` returns false for hands.
  - `QualityUtility.GenerateQualityCreatedByPawn(Pawn, SkillDef, bool)` uses the hand's level.
  - `GenConstruct.CanConstruct(Thing, Pawn, bool, bool, JobDef)` applies the hand's level. The races set `mechFixedSkillLevel` 20 so vanilla's own gate always passes.

## Chassis notes (why it works, all from the decompile)

- A mechanoid race **without** `CompProperties_OverseerSubject` and **not** parented to `BaseMechanoid` is `IsColonyMech`. So `JobGiver_Work.PawnCanUseWorkGiver` accepts it, and `GetDisabledWorkTypes` turns off everything outside `mechEnabledWorkTypes`. It has no energy need, no feral state, no bandwidth, no Mechs tab entry, and it doesn't appear in the colonist bar or work tab.
- `MarketValue` 0 (pawns count toward wealth), `combatPower` 5-10 (colony mechs add to threat points). `forceDeathOnDowned`, `forceNoDeathNotification`, and a `DeathActionProperties_Vanish` death action, so there are no corpses or letters.
- **`isFighter` and `allowInMechClusters` are false.** Otherwise mech clusters, sleeping-mech complexes and ancient shrines would pick hand kinds (`MechClusterGenerator.MechKindSuitableForCluster`).
- Body: vanilla `Mech_Light`, so the health tab shows mech part names. A custom body is a possible later polish.
- `PH_TelekineticAura` is the defName of both an AbilityDef and a HediffDef. That's legal, because each def type has its own database.

## Playtest checklist

1. The mod loads with no errors. The Arcanum tab shows the Phantom Hand card in two columns, and the icons and background appear.
2. Busy Hands: the hands haul by priority, glide through mud and shallow water at full speed, show the inspect line, and vanish on time. One carrying an item drops it. No letters or messages.
3. Colony wealth doesn't change. Raid points barely move (check with dev mode's storyteller points).
4. Save and load with hands out: they keep their timers, keep working, and still vanish.
5. Caster downed, killed, or leaving with a caravan: the hands vanish. Forming a caravan with a hand in it: the hand vanishes.
6. Timber and Construction Crew: they cut designated plants, and they build with quality from their skill. A low-skill crew refuses buildings that need high skill.
7. Rescue Hands: they rescue a downed colonist with an enemy within 25 cells. A hand expiring mid-carry finishes within the grace or drops the colonist gently.
8. Grasping Hands: only hostiles get the grip, and it wears off.
9. Thousand Palms: shields catch bullets, enemies get shoved, and hands attack.
10. Telekinetic Aura: the heat floor holds at 30, and hands appear every ~20 s only for waiting work, up to the cap. Using it again ends it and its hands vanish.
11. Hundred-Handed: during a raid the hands fight and rescue, and afterwards they switch to work. 40 hands don't tank the TPS. The cooldown is 1 day.
12. No mech cluster or ancient shrine ever contains a hand.

## Playtest log

- **2026-11-03, round 1 (Busy Hands in a closed storeroom):** the hands spawned, reorganised items, idled when done, and despawned on time. **Bug:** on despawn, every hand dropped an item that had valid storage. Cause: Pick Up And Haul (`mehni.pickupandhaul`, active) patches `CompHauledToInventory` onto `ThingDef[thingClass="Pawn"]`, which is the abstract `BasePawn`, so the hands inherited it. Its default "allow mechanoids" let the hands use its haul-to-inventory work giver. Its unload step is only triggered from a HaulToCell postfix, `JobGiver_Idle` and `JobGiver_DropUnusedInventory`, and our think tree uses none of those. **Fix:** `PhantomHandMod.DetachPickUpAndHaul` removes that comp from hand races at startup (PUAH's work giver skips pawns without it), plus a safety net in `Pawn_PhantomHand.TickInterval`: an idle hand with anything in its inventory drops it every 250 ticks, and its hauling stores it. Retest pending.

## Known limitations / ideas

- Route *planning* still avoids mud and water. Only the per-step cost is flattened.
- Rescue handles humanlikes only (not colony animals). With no free bed, nobody is rescued.
- Hands use the vanilla light-mech body and sounds.

## Art (done 2026-11-03)

All of it was generated with Codex image generation (`../Tools/codex_image.py`, run from this folder with `--repo .`). The jobs, raw output and references are in `Art/`.

| File | Size | Source | Notes |
|---|---|---|---|
| `Textures/PhantomHand/Abilities/BusyHands`, `TimberHands`, `ConstructionCrew`, `TelekineticAura` | 128 | `Art/raw/icons_a_v1.png` | Passed the family check against VPE icons first time. |
| `Textures/PhantomHand/Abilities/RescueHands`, `GraspingHands`, `ThousandPalms`, `HundredHanded` | 128 | `Art/raw/icons_b_v2.png` (sheet A as the reference) | v1 drifted to thin outlines and flat lemon hands; v2 copies sheet A's ivory and honey-gold look. |
| `Textures/PhantomHand/Abilities/MinerHands` | 128 | `Art/raw/icon_miner_v1.png` (one icon; sheets A and B v2 as references), split and padded to square | Passed the family check against its 8 siblings first time. |
| `Textures/PhantomHand/Things/Hand_south`, `_east`, `_north` | 256 | `Art/raw/sprite_v1.png` (drawn on black), keyed with `../Tools/alpha_from_black.py`, cut by grid at one shared scale | Near-white on purpose: each hand kind is tinted by `graphicData color`. |
| `Textures/PhantomHand/UI/PathBackground.png` (background and altBackground) | 950x1515 | `Art/raw/background_v2.png` via `../Tools/fit_cover.py` | v1 had the right emblem but a thin frame and no black panel border. v2 copies VPE's frame from PathBG_Archon, Chronopath and Skipmaster. |
| `About/Preview.png` | 1280x720 | `Art/raw/preview_v1.png` (made by the user from `Art/jobs/preview_v1.md` after Codex image generation kept failing with network errors on 2026-11-05), `fit_cover.py`, 256-colour Floyd-Steinberg dither (507 KB) | Full colour in `Art/final/Preview_1280_fullcolour.png`. Shows a hand sowing seeds, though the mod has no planting hands; the user accepted it as artistic licence (2026-11-05), so leave it. |
| `About/ModIcon.png` | 128 | copy of the Busy Hands icon | |

Pipeline notes:
- **Cut icon sheets by grid** (scratchpad `grid_split.py`: crop each 2x2 cell, then run Necro's splitter on that cell). The splitter's `--merge-distance` either splits multi-part icons or merges neighbouring cells.
- **Don't use `--fill-holes`.** It turned the inside of the Aura icon's ring into a white disc.
- **Pad the splitter output to square.** It trims to content, and gizmos would stretch a 128x97 icon.
- **Shader:** additive `MoteGlow` washed the near-white hands out to plain white on pale floors. They use `TransparentPostLight` instead: alpha-blended, and unlit, so they still glow at night. The role tints were made stronger at the same time.

House style for future Phantom Hand art: **spectral gold.** Ivory hands (#f3e4b8) with honey-gold shade planes (#d6a646), objects in muted ochre and warm grey, thick near-black outlines, flat fills. The hand is chunky and cartoonish with rounded fingers, no arm, and a wrist that dissolves into 2-3 wispy ghost tails. The card is bronze-amber; Chronopath already uses olive-gold. Pass `Art/raw/icons_a_v1.png` as the first `--ref` for any new icon.
