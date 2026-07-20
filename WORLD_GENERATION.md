# Arenas controlled world generation

`ArenasSubworld` is one logical 4200x1200 world. In multiplayer it runs in Subworld
Library's child server process. Repeated rounds with the same fight preset reuse that
process and terrain. Selecting a preset for a different biome restarts generation so the
arena can follow that preset's natural terrain.

## Admin workflow

Open **Arenas: World Gen Manager** from the admin quickbar.

- **Clear all tiles** restarts only the Arenas child, runs `WorldGen.clearWorld`, and runs
  no generation passes.
- Clicking a step restarts the Arenas child and rebuilds from `Reset` through the selected
  step. This is intentionally prefix-based: vanilla passes depend on `GenVars`, structures,
  tiles, and other state made by earlier passes.
- **Generate complete match world** runs Terraria's complete generator plus every Arenas
  pass and marks the child match-ready.
- **Enter generated preview** moves only the requesting admin into the current child without
  regenerating it. A later generation request evacuates every player to Main first.

The manager's completion markers describe the latest generated prefix, not an independently
mutable checklist. Choosing an earlier step deliberately discards later work.

## Safety and multiplayer lifecycle

1. The authoritative server validates the admin request.
2. Every player in Arenas is moved to Main.
3. Main waits for the transferred player slots, then stops the old child.
4. The request (seed, mode, target, request ID, teams) is copied through `ICopyWorldData`.
5. Main starts an empty Arenas child. No player is transferred during generation.
6. The child generates and sends an explicit ready message through Subworld Library's pipe.
7. World Gen Manager requests leave players in Main. A normal match request transfers the
   roster only when the child reports a complete, validated combat world.

The child sends heartbeats while it stays alive. If it disappears, Main marks it unavailable
and the next match request performs a complete bootstrap.

## Generation implementation

`ArenaWorldGenerationSystem.Generate` calls `WorldGen.GenerateWorld`. It does not invoke
individual `GenPass` instances directly. This preserves Terraria/tModLoader's seeded RNG,
`GenVars`, `StructureMap`, world flags, configuration, and `PreWorldGen`/`PostWorldGen`
lifecycle. The selected prefix is applied in `ModifyWorldGenTasks` after custom Arenas passes
are inserted, and `totalWeight` is recalculated.

The three custom passes are:

1. **Arenas: Reserve Combat Region**, immediately after `Terrain`. It deliberately defers
   placement because the natural Jungle and Temple do not exist yet; it does not reserve or
   flatten biome terrain.
2. **Arenas: Build Combat Region**, immediately before `Final Cleanup`. It resolves the fight
   preset against the completed vanilla world—Plantera selects the underground Jungle and
   Golem selects the vanilla Temple. It preserves the natural arena interior and background
   walls. The only stamping is a three-tile Lihzahrd-brick perimeter with three tile-clear
   bands immediately inside and outside it. Team spawns occupy the bottom inner-clearance band.
3. **Arenas: Validate Combat Region**, immediately after `Final Cleanup`. It checks all layout
   invariants and every border/clearance tile, frames the edited perimeter, and republishes the
   authoritative spawn.

The perimeter is late-stamped so vanilla first produces complete, natural biome terrain and
structures. Arenas does not add platforms, floors, artificial biome walls, or a replacement
arena interior.

### Plantera generation profile

When the active fight preset resolves to `PlanteraJungle`, Arenas disables every generation
pass outside an explicit Jungle dependency allow-list. The runnable profile retains base
terrain, cave formation, mud and Jungle conversion, Wet Jungle, Temple, hives, honey, Jungle
chests and plants, liquids, natural underground dressing, tile cleanup, and all three Arenas
passes. Desert, ocean, snow, evil-biome, Dungeon, floating-island, Underworld, beach, and
unrelated surface-decoration passes remain registered but disabled, so their bodies do not run.
Other fight generators continue to use the complete vanilla pass list.

## Adding another Arenas pass

1. Give the pass a stable, unique name in `ArenaWorldGenerationCatalog` at its real order.
2. Insert it relative to a stable vanilla pass name in `ModifyWorldGenTasks`; fail loudly if
   the anchor is missing.
3. Use `GenerationProgress.Message` and `Set` for meaningful progress.
4. Use `WorldGen.genRand`/the generation RNG, never an unseeded `Random`.
5. Reserve large structures with `GenVars.structures.AddProtectedStructure`.
6. Keep edits inside clamped tile coordinates. Remove chest/sign/tile-entity metadata before
   clearing tiles that own those objects.
7. Log the pass inputs, selected region, and result. Use `Log.Chat` only for milestones that
   an admin should see.
8. Test it by selecting the new pass in World Gen Manager, inspecting the preview, then run
   the complete world and verify multiplayer entry.

This follows the lifecycle and pass-order guidance in the
[tModLoader World Generation guide](https://github.com/tModLoader/tModLoader/wiki/World-Generation).
