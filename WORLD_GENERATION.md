# Arenas controlled world generation

`ArenasSubworld` is one logical, reusable 4200x1200 world. In multiplayer it runs in
Subworld Library's child server process. Boss presets and votes reuse that process and do
not generate a new world.

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

1. **Arenas: Reserve Combat Region**, immediately after `Terrain`. It creates the deterministic
   layout and adds its rectangle to `GenVars.structures`.
2. **Arenas: Build Combat Region**, immediately before `Final Cleanup`. It removes conflicting
   chests, signs, tile entities, tiles, walls, and liquids in that rectangle, then builds the
   protected frame, biome walls/floor, spawn rooms, and platforms.
3. **Arenas: Validate Combat Region**, immediately after `Final Cleanup`. It checks all layout
   invariants, frames the edited region, and republishes the authoritative spawn.

The combat region is late-stamped because not every vanilla pass honors `StructureMap`.
Reserving early reduces conflicts; rebuilding late guarantees the arena itself is deterministic.

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
