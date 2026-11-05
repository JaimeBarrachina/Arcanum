# VPE - Spatialist

A Vanilla Psycasts Expanded path built around a personal pocket dimension: a small map the psycaster can open a gate to anywhere, used as storage that any pawn can walk into.

Status: **Level 1 (Pocket Gate) built 2026-11-01. It compiles and the XML is well-formed, but it hasn't been tested in game.** Target: RimWorld 1.6.

## Layout

| Path | What |
|---|---|
| `About/About.xml` | packageId `darky.vpe.spatialist`; depends on Harmony, VEF, VPE |
| `LoadFolders.xml` | 1.6 loads `/` and `1.6` |
| `1.6/Assemblies/Spatialist.dll` | build output (don't edit) |
| `1.6/Defs/PsycasterPathDefs/Spatialist_Path.xml` | the path |
| `1.6/Defs/AbilityDefs/Spatialist_Abilities.xml` | Pocket Gate psycast |
| `1.6/Defs/ThingDefs/Spatialist_Buildings.xml` | gate and exit |
| `1.6/Defs/MapGeneration/Spatialist_PocketMap.xml` | map generator and room gen step |
| `Languages/English/Keyed/Spatialist.xml` | UI strings |
| `Source/Spatialist/` | C# project (`dotnet build Source/Spatialist/Spatialist.csproj -c Release` writes to `1.6/Assemblies`) |

The folder is junctioned into `RimWorld\Mods\Spatialist`.

## Code map

- `WorldComponent_PocketDimensions`: owns every pocket (`PocketRecord`: owner, map, exit, gate). Open, close, evacuate, collapse, and spilling items and pawns out. A 60-tick check handles dead owners, maps that have vanished, and a stale `sourceMap`.
- `Building_PocketGate : MapPortal`: the gate. **It's destroyed on close and recreated on every open.** The map and exit are the permanent parts, so a gate lost with its map can't orphan anything.
- `Building_PocketExit : PocketMapExit`: refuses passage while the gate is closed and hides "view surface".
- `GenStep_PocketRoom`: floor (`Slate_Smooth`) plus thick rock roof over the whole map, with the exit in the middle.
- `Ability_PocketGate : VEF.Abilities.Ability`: `DoAction` closes the gate if it's open (free, no targeting). If the caster is stuck inside their own pocket with the gate closed, it offers an evacuation. Otherwise it targets a cell and `Cast` opens the gate.
- `Ability_PocketUser` (base: needs a pocket, cast from outside one) with `Ability_Stash`, `Ability_Displace`, `Ability_Sanctuary` and `Ability_ExpandPocket`; also `Ability_Passive` (no gizmo, used by Spatial Pockets) and `Ability_SpatialBag`. All in `Ability_PocketUser.cs`.
- `StatPart_Spatialist`: patched onto `VEF_MassCarryCapacity` (+100 if Spatial Pockets is learned, plus worn bags' stored capacity) and `CarryingCapacity` (+75) by `1.6/Patches/Spatialist_Stats.xml`.
- `CompSpatialBag`: the bag's stored capacity; shows it in the label and inspect pane.
- `PocketRecord.OpenSize`: the open square's side length. 0 in old saves, which is read as "the whole map", so pre-Expand 15x15 pockets count as fully open.
- `HarmonyPatches` (roof): prefixes on `RoofCollapseCellsFinder.ProcessRoofHolderDespawned` and the private `CheckCollapseFlyingRoofAtAndAdjInternal` skip our pocket maps, so roofs there never collapse. `WorldComponent_PocketDimensions.RepairRoof` re-roofs the open area and clears `CollapsedRocks` on every open and expand.
- `HarmonyPatches`: `GenGrid.InNoZoneEdgeArea` (vanilla bans zones within 5 cells of the map edge, even in pocket maps; we lift that for our maps only) and `WorldComponent_GravshipController.InitiateTakeoff` (close gates on the launching map and eject pawns next to the grav engine).

General paths, tooling and research notes live in [../Mods.md](../Mods.md).

## Design decisions

- **Ownership:** one pocket per caster. It's saved with the caster and lasts until the caster dies.
- **Gate:** a psycast toggles a gate next to the caster. Casting again closes it. The pocket map is never deleted while the caster lives.
- **Access:** any pawn can use an open gate. The vanilla "Enter..." dialog handles hauling items in.
- **Starting size:** 15x15, sealed and roofed.
- **Closing with pawns inside:** they're ejected next to the gate.
- **On caster death:** the contents spill out on the caster's last map near the body, then the pocket is destroyed.
- **Gravship:** the gate closes automatically on launch, and the map survives the old map being abandoned.
- **Can't be cast from inside the pocket.**

## Psycast tree

| Level | Psycast | What it does | Status |
|---|---|---|---|
| 1 | Pocket Gate | Open or close the gate. The first cast creates the map; later casts relink the same map. | Built, in playtest |
| 2 | Spatial Pockets (passive, needs Pocket Gate) | +100 kg `VEF_MassCarryCapacity` (inventory and caravan capacity) and +75 `CarryingCapacity` (doubles the base amount hauled per trip). | Built 2026-11-01, untested |
| 2 | Stash (needs Pocket Gate) | Skip a targeted item stack into the pocket without opening the gate. Range 24.9, 2% psyfocus, 6 heat. | Built 2026-11-01, untested |
| 3 | Displace | Stash for every haulable item in a 3x3 area (radius 1.5). Needs Stash. Range 24.9, 6% psyfocus, 18 heat. | Built 2026-11-01, untested |
| 3 | Expand Pocket (needs Pocket Gate) | 15 → 25 → 35 → 45. The map is generated at **45x45** with `Spatialist_PocketWall` everywhere outside the open middle. Each cast removes a 5-cell ring and places 8 light crystals (corners and mid-sides) along the new inner edge. 50% psyfocus, 40 heat. | Built 2026-11-01, untested. Pockets made before this build stay 15x15 forever (a migration would be needed if published saves matter). |
| 4 | Sanctuary (needs Expand Pocket) | Skip the caster and friendly pawns within 4.9 cells (same faction, prisoners, guests) into the pocket. 30% psyfocus, 35 heat. | Built 2026-11-01, untested |
| 5 | Spatial Bag (needs Spatial Pockets) | Creates a wearable bag (backpack slot) giving +100 kg × the caster's psychic sensitivity at creation. Costs 80% psyfocus and **200 heat**. | Built 2026-11-01, untested |

**Card layout (2026-11-03):** a single tree from Pocket Gate, with three branches: items on the left (Stash, then Displace), the pocket in the middle (Expand, then Sanctuary), and carrying on the right (Spatial Pockets, then Spatial Bag). Before this, Spatial Pockets was a second root at level 1, Sanctuary hung straight off Pocket Gate, and lines crossed.
- In VPE, level only sets the row. Learning a psycast needs one of its prerequisites plus a point ([`ITab`] `PrereqsCompleted && points >= 1`), so moving rows doesn't change the balance. The changes that matter: Spatial Pockets now needs Pocket Gate, and Sanctuary now needs Expand Pocket. Pawns who already learned them keep them.
- Placement rules (`PsycastsUIUtility.DoPathAbilities`): each row has 1–3 slots at fixed offsets from the card centre (1 slot: 0; 2 slots: ±29; 3 slots: -51/0/+51). `order` only sorts. `spaceAfter` inserts a blank slot after an icon. A row can't start with a blank, so a lone icon can only sit at 0 or -29. Lines are straight, centre to centre, drawn under the icons.
- The layout was found by brute force (scored on crossings and lines clipping icons); the script is in the session scratchpad, `tree/solve.py`. The result: L1 PG(0); L2 Stash(-51, spaceAfter), Pockets(+51); L3 Displace(-51), Expand(0, spaceAfter); L4 Sanctuary(-29, spaceAfter); L5 Bag(0).

### Decisions for the new skills (2026-11-01)

- **One stat covers both "carry" and "caravan" capacity.** VEF transpiles `MassUtility.Capacity` to return the `VEF_MassCarryCapacity` stat (base: body size × 35). Vanilla's `CarryingCapacity` is a separate stat: how much a pawn hauls per trip. VAE Accessories' backpack (`VAEA_Apparel_Backpack`) is +50 `VEF_MassCarryCapacity`.
- **How passives work:** VPE has no passive psycasts. Instead, a `StatPart` on `VEF_MassCarryCapacity` (and `CarryingCapacity`) checks "has learned Spatial Pockets". The psycast shows in the tree with no gizmo. Nothing is saved, it shows up in the stat breakdown, and it's removed if the ability is lost. The same `StatPart` also adds the worn Spatial Bag's stored capacity.
- **Spatial Bag:**
  - Its capacity is fixed when it's made, from the *caster's* sensitivity, and stored on the item (a comp). The label shows it, e.g. "spatial bag (148 kg)".
  - **Bags stay when their maker dies.**
  - It goes in the backpack slot, so it can't stack with a regular backpack.
  - **200 heat is intentional:** the trade is overloading for a permanent item. VPE blocks casts that would overflow heat only while the vanilla neural heat limiter is on (`Pawn_PsychicEntropyTracker.WouldOverflowEntropy` returns false when `limitEntropyAmount` is off). The description must say: switch off the heat limiter to cast it.
- **Stash and Displace** need the pocket to exist (Pocket Gate cast at least once). Items land around the exit inside the pocket.
- **Where each psycast works (2026-11-02):** only *opening the gate* is barred from other pocket maps. Stash and Displace work from any map, including undercaves, ancient stockpiles, dungeons and other casters' pockets. **Inside the caster's own pocket they work in reverse (2026-11-02):** items skip out next to the open gate. They're greyed out there while the gate is closed. See `Ability_ItemSkip` and `WorldComponent_PocketDimensions.TryGetItemDestination`. Expand Pocket works from anywhere, including inside the pocket and from a caravan. Sanctuary is still barred from other pocket maps, because allowing it would make it an escape from undercaves and dungeons; that's still undecided. Set per psycast with `NeedsMap`, `WorksInOwnPocket` and `WorksInOtherPockets` on `Ability_PocketUser`.
- **New art needed:** Spatial Pockets icon, Displace icon, Spatial Bag icon, and the Spatial Bag apparel sprite (modelled on a backpack).

## Technical design

Everything is built on the vanilla 1.6 pocket-map API (notes in Mods.md, "Vanilla 1.6 pocket-map API").

- **Gate:** `MapPortal` subclass.
  - Override `GeneratePocketMapInt()` to create the map, or reuse the caster's existing map. On reuse, set the protected `pocketMap`, set `exit`, and set `exit.entrance = this`.
  - Override `Destroy`/`DeSpawn` so removing the gate ejects pawns and never orphans or destroys the map.
- **Exit:** `PocketMapExit` subclass, spawned by our own GenStep. `portal.exitDef` is ignored by vanilla. While the gate is closed, `IsEnterable` returns false with "gate closed".
- **Map:**
  - Our own `MapGeneratorDef` with `pocketMapProperties`: underground-style biome, a fixed temperature, `destroyOnParentMapAbandoned = false`.
  - Gen steps: floor, walls, roof, exit, fog.
  - Each time the gate opens, set `PocketMapParent.sourceMap` and `Tile` to the current map. This handles time of day, threat points and wealth. Isekai Dungeons sets `Tile` too, because surface biomes break without it.
- **Ownership record:** saved with the caster, either in the ability's `ExposeData` or a hediff. It holds the pocket map, the exit and the last map. Alpha Genes stores it on the ability comp.
- **sourceMap safety:** if the map `sourceMap` points at is removed, re-point it to a home map. Otherwise storyteller threat points throw a NullReferenceException.
- **Death:** Harmony postfix on `Pawn.Kill` (as Alpha Genes does). Eject pawns and items onto the caster's map near the body, then call `PocketMapUtility.DestroyPocketMap`.
- **Gravship:** close the gate when a launch starts. Find the right hook in `WorldComponent_GravshipController` / `GravshipUtility`.
- **Psycast:** `VEF.Abilities.Ability` subclass that overrides `Cast(params GlobalTargetInfo[])`, modelled on VPE's `Skipmaster.Ability_Skipdoor`. The ability def uses `ParentName="VPE_PsycastBase"` with an `AbilityExtension_Psycast` mod extension (`path`, `level`, `order`, `psyfocusCost`).
- **Path:** `VanillaPsycastsExpanded.PsycasterPathDef`, in its own **Arcanum** tab (`<tab>Arcanum</tab>`; was `Misc`, then `Arcane` from 2026-11-02 to 2026-11-05).

### Dependencies

- Royalty
- Harmony (`brrainz.harmony`)
- Vanilla Expanded Framework (`OskarPotocki.VanillaFactionsExpanded.Core`, `VEF.dll`)
- Vanilla Psycasts Expanded (`VanillaExpanded.VPsycastsE`, `VanillaPsycastsExpanded.dll`)

### Templates to read

- VPE Skipmaster abilities and Skipdoor: `294100\2842502659\1.6\Defs\AbilityDefs\Skipmaster.xml`, `...\Base.xml`, `...\ThingDefs\Buildings.xml`, `...\PsycasterPathDefs\Paths.xml`
- Add-on layout with a DLL: VPE - PhaseShift `294100\3529964480\1.6\CoreMod\`
- Portal and map generation: Isekai Dungeons `294100\3813394602\Source\IsekaiDungeons\Dungeons\Building_DungeonGate.cs` (lines 164-270), `Defs\MapGeneration\DungeonMapGenerator.xml`
- Vanilla small pocket map: `RimWorld\Data\Odyssey\Defs\MapGeneration\AncientStockpileMapGenerator.xml`

## Known pitfalls

From vanilla code and other mods' bug reports:
- Null-guard anything that touches `Map`/`MapHeld` during save/load and while things are in transit (gravship, minified, caravan). Every Dimensions RePocketed crash falls in this group.
- Don't nest pockets. RePocketed corrupted saves when a pocket was opened inside another pocket map.
- Avoid map-wide scans in frequently called patches. The original Pocket Dimensions had a performance issue from exactly that.
- Pocket maps can't be exited from their edge, so the exit gate is the only way out.

## Build plan

1. ~~Project scaffold.~~ Done.
2. ~~Level 1: gate, exit, map generator, Pocket Gate psycast, path def, death handling, gravship close. Placeholder textures.~~ Done, untested.
3. **Playtest checklist (next):**
   - [x] Mod loads with no red errors; the Spatialist path shows in the VPE Misc tab (2026-11-01)
   - [x] Learn Pocket Gate; cast it on a nearby cell; the gate appears (2026-11-01)
   - [ ] Enter the pocket: lit 15x15 room, exit in the middle, stockpile zone can cover it right to the edges (enter and exit confirmed; zone edges not yet checked)
   - [ ] Haul items in with "Enter..."; carry some back out through the exit
   - [ ] Click the psycast while the gate is open: closes instantly, nothing charged, pawns inside are ejected
   - [ ] Reopen elsewhere and on a different map: same room, same contents
   - [ ] Save and reload with the gate open, with it closed, and with pawns inside (with a pawn inside and the gate open: confirmed 2026-11-01)
   - [ ] Caster stuck inside with the gate closed: the psycast offers evacuation
   - [ ] Gravship launch with the gate open on the launch map: gate closes, pawns land next to the engine and fly with the ship; pocket intact after landing
   - [ ] Caster dies with items and pawns inside: letter, everything spills near the body, pocket gone
   - [ ] Caster dies *inside* the pocket with the gate closed: everything spills onto the colony map
4. ~~Levels 2-5.~~ Built 2026-11-01. **Playtest checklist, round 2** (start a fresh pocket: dev-kill the old caster, or use a new one, because old 15x15 pockets can't expand):
   - [ ] No red errors on load. The tree shows 7 psycasts over 5 levels with the right icons.
   - [ ] New pocket: 45x45 map, open 15x15 in the middle, violet rock walls around it, no visual glitches where walls meet the map edge
   - [ ] Spatial Pockets: no button on the pawn. The stat breakdown shows "Spatial pockets: +100 kg" (mass capacity) and +75 (carrying capacity). Caravan capacity goes up.
   - [ ] Stash: target an item stack; it appears next to the exit inside the pocket. Greyed out with a reason before the pocket exists and from inside the pocket.
   - [ ] Displace: shows a 3x3 targeting ring; everything loose in it goes in
   - [ ] Expand Pocket x3: 25, 35, 45; crystals light each new ring; greyed out at 45 and on old 15x15 pockets
   - [ ] **Roof stays up on expand** (bug found 2026-11-02: removing the wall ring collapsed the whole roof). The fix is that roof collapse is disabled on our pocket maps. Saves hit by the bug heal on the next gate open or expand: missing roof is restored and collapsed rubble removed. Things crushed by the collapse are gone.
   - [ ] Sanctuary: caster, colonists, a prisoner and a colony animal nearby all land in the pocket; raiders don't
   - [ ] Spatial Bag: greyed out with the heat limiter on. With it off, the cast works, the overload hits, and a bag appears. Its label shows its kg (100 × the caster's sensitivity). Worn, it adds that to mass capacity and can't be worn with a VAE backpack.
   - [ ] Spatial Bag looks right when worn: north, south, east and west, on a thin, a hulk and a female pawn. Tune `wornGraphicData` offsets and scale if needed.
   - [ ] Save and reload with an expanded pocket: walls and crystals intact, open size remembered (a further expand still works)

## Known limitations (Level 1)

- On collapse, buildings inside are minified and spill out only if they're minifiable. Anything else (built walls, floors) is lost.
- The pocket uses vanilla's `Underground` biome, so it can technically roll diseases.
- The psycast's label doesn't change to "close" while the gate is open. The description says so, and the gate has its own "Close gate" button.
- The pocket's time of day follows the map where it was first created; it doesn't update after the colony moves.

## Art (done 2026-11-01)

All generated with Codex image generation (`Tools/codex_image.py`); jobs, raw output and references are in `Art/`.

| Texture | Size | Source | Notes |
|---|---|---|---|
| `Textures/Spatialist/Abilities/PocketGate.png` (+ `Stash`, `ExpandPocket`, `Sanctuary` for later levels) | 128 | `Art/raw/icons_v2.png`, split with Necro's `icon_sheet_splitter.py` | Matches VPE's outlined-silhouette ability style in a lavender palette; passed a family check next to the Skipmaster icons. v2 (2026-11-01) swaps v1's square rift for a simplified version of the stone gate sprite. |
| `Textures/Spatialist/UI/PathBackground.png` (background and altBackground) | 950x1515 | `Art/raw/background_v2.png` via `Tools/fit_cover.py` | Amethyst theme, nested-squares "folded room" emblem. v1 failed the family check (hairline frame, black field, neon glow); v2 copies the wide frame band, tinted field and solid black emblem. |
| `Textures/Spatialist/Things/PocketGate.png` (gate and exit) | 256 | `Art/raw/gate_v2.png` drawn on #00FF00, keyed with Necro's `chroma_key.py`, trimmed and padded to a square | Stone pillars and lintel with amethyst crystals and swirling violet light in the opening. Drawn upright, so the defs use `drawOffset (0,0,0.3)` to stand it on its tile. The earlier diamond-rift version is `Art/final/PocketGate_v1_rift.png`. |
| `Textures/Spatialist/Abilities/SpatialPockets.png`, `Displace.png`, `SpatialBag.png` | 128 | `Art/raw/icons_v4.png` | v3 failed the family check (painterly, thin outlines, an arch instead of the pillar gate); v4 copies the accepted sheet's look. Split with `--merge-distance 60` so Displace's dashed outline stays in one piece. |
| `Textures/Spatialist/Things/SpatialBag/SpatialBag.png` (128) + `_south`, `_north`, `_east` (256) | | `Art/raw/sprites_v1.png`, keyed with `chroma_key.py`, cut **by grid cell** so all worn views keep one scale | Modelled on VAE Accessories' backpack layout. The east view was drawn mirrored and is flipped. |
| `Textures/Spatialist/Things/LightCrystal.png` | 128 | same sheet, cell 5 | |
| Pocket walls | | vanilla `Things/Building/Linked/Rock_Atlas`, tinted `(88,76,110)` in XML | No art of ours. |
| `About/Preview.png` | 1280x720 | `Art/raw/preview_v2.png` (v1's composition with the stone gate; the hauler wears a Spatial Bag) | Reduced to 256 colours with dither to fit Steam's 1 MB preview limit (397 KB); full-colour versions are in `Art/final/`. |
| `About/ModIcon.png` | 128 | copy of the Pocket Gate icon | |

House style for future Spatialist art: muted lavender-grey and amethyst (#7a5aa6 family). **The stone gate is the path's motif.** Icons get thick near-black outlines, flat two-tone fills and chunky silhouettes (pass `Art/refs/ref_vpe_icons.png`, `Art/raw/icons_v2.png` and `Art/final/PocketGate_v2.png` as `--ref`). Opaque sprites are drawn on #00FF00 and keyed with `chroma_key.py`. Pure glow effects are drawn on black and keyed with `alpha_from_black.py`.

## Before publishing

- ~~All textures must be our own.~~ Done: no VPE textures are referenced any more. (The cast sound `VPE_SkipGate_Cast` is still VPE's, which is fine because VPE is a required dependency.)
- **Leave `Art/` and `Source/**/obj` out of the upload** (15 MB of raw art and references). RimWorld ignores them, but Steam would upload them.
- No code copied from unlicensed mods. Alpha Genes, RePocketed, PocketMapTent, Basement and the original Pocket Dimensions are reference only.
- Decide the packageId, author name, license and Workshop preview image.
