# Proposal

## Why

The numbers of the game (boss health and damage, summon energy, devices, survivor growth over a 15 minute match) were never tuned together: each change added its own values and was checked for working, not for how a whole match feels. The aim for the next balance pass is long matches decided late, which can only be tuned by trying many values and measuring the match length, the winner and how fast the boss loses health. Playing by hand gives a handful of matches a day; the game has no way to play a match by itself, and its server logic is scattered over four components that run in a Unity-specific order, so a match cannot be run in a test or a batch job without the network and the scene.

## What Changes

- The order in which the server advances a match (mobs, summon energy, combat, growth and the match clock) moves into one plain class, `MatchWorld`, with one method per phase and a `Step` that runs the phases in that order. The four server components keep their network work (reading avatars, sending messages, writing replicated values) and call the phase methods at the same Unity execution order as before. The game's behavior does not change.
- The phase order lives in one place (`MatchPhases`), and a test fails if the servers' execution order and the harness's order ever differ.
- A headless match harness runs a full match in plain C# with no scene, no network and no wall clock, from a seed: it builds a `MatchWorld`, moves bodies, applies the bots' commands each fixed step and stops when the match has a result.
- Bots play both sides: four survivor bots (move away from threats, aim at the boss or the nearest mob, use skills, revive fallen teammates, take the automatic skill choice) and a boss bot (chase, shoot, use Q/E, summon, place devices). A skill profile (weak, normal, strong) sets the aim error, the reaction delay and how much the survivors prefer the boss to mobs.
- A runner plays many matches from a settings file (boss kind, skill profile, character mix, value overrides, number of matches, seeds) and writes one CSV row per match and a summary in markdown and JSON: result shares, match length distribution, the times the boss reaches 50%, 25% and 0% health, survivor falls and revivals, final levels, device counts, and the change against an earlier summary. It runs from the Unity command line (`-executeMethod`) and from the Unity MCP.
- Tests and the README cover the extraction, the determinism of a seed, the bots and the report.

## Capabilities

### New Capabilities
<!-- None: the harness is development tooling and the server's behavior is unchanged, so no spec changes. The change sets skip_specs. -->

### Modified Capabilities
<!-- None. -->

## Impact

- Code: new `MatchWorld`/`MatchPhases`, the bots, the harness core, the metrics and report writer (runtime assembly, no Unity objects); an editor entry point for the command line. `MobServer`, `SummonServer`, `CombatServer`, `GrowthServer` and `RoleTable` hand their simulation calls to `MatchWorld` and keep the network parts.
- Behavior: none. The existing EditMode tests and the multi-process comparisons (mob positions, combat events, summon and order, growth, boss outcomes) must pass unchanged.
- Network and data: none.
- Not in scope: changing any game value (the balance pass is the next change), smarter bots (kiting a boss skill, hunting devices first, orders), running matches in parallel threads, a window or chart for the results.
