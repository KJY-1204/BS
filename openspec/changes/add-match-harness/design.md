# Design

## Context

All the simulations are plain C# (`MobSim`, `SummonSim`, `SurvivorSim`, `AttackSim`, `ProjectileSim`, `BossSim`, `BossSkillSim`, `MobActionRunner`, `LevelingSim`, `SkillSim`, `MatchSim`) and the EditMode tests already run them without a scene. What is missing is the order they are called in: it lives in four components that Unity runs by `DefaultExecutionOrder`, each mixing simulation calls with network work (reading avatar transforms, `ServerWrite`, `Publish`, RPC replies):

```
-100 MobServer.FixedUpdate     MobSim.Step(survivors from avatars), FlushEvents, snapshots
 -90 SummonServer.FixedUpdate  SummonSim.Step, write energy to the boss's replicated values
 -50 CombatServer.FixedUpdate  boss position, SurvivorSim.Step, mob actions, boss skills, attacks,
                               projectiles, replicated health, FlushEvents (kills of shots)
 -40 GrowthServer.FixedUpdate  kills -> experience, skill requests, Leveling.Step, Skills.Step,
                               stats, replicated growth values
     RoleTable.StepMatch       MatchSim.Step from the boss and survivor state
```

The state a phase needs from the scene is small: where each survivor and the boss stand, whether a survivor is moving, which character a survivor plays, and the requests that arrived (aim and fire, skill, pick, summon, order, device, boss skill). Randomness comes from `MobSim` (seeded), `SummonSim` (seeded) and `LevelingSim` (takes a `Random`); `BossCatalog.CurrentKind` and `DevArgs` are statics. See proposal.md for why.

## Goals / Non-Goals

**Goals:**
- One match, from a seed, runs in plain C# in a test or a batch process, with the same order of simulation calls as the server.
- The server's behavior stays exactly as it is; the order cannot drift apart from the harness without a test failing.
- Many matches per run with per-match rows and a summary that can be compared with an earlier run.

**Non-Goals:**
- Changing any game value, adding bot strategies beyond the listed ones, running matches on several threads, a chart or window for the results.
- A deterministic server (the server is still driven by real time and the network).

## Decisions

**1. `MatchWorld` owns the simulations and exposes one method per phase; the servers keep the network and call the phases at their existing Unity order.**
`MatchWorld` holds the sims listed above and a participant list (client id, role, character, position, moving, plus the growth entry). Its phase methods are `StepMobs(dt)`, `StepSummon(dt)`, `StepCombat(dt)`, `StepGrowth(dt)` and `StepMatch(dt)`, and `Step(dt)` runs them in the order of `MatchPhases`. `MobServer`, `SummonServer`, `CombatServer`, `GrowthServer` and `RoleTable` still run at -100, -90, -50, -40 and where they are now, and each calls its own phase method, then does its network work (flushing events, snapshots, replicated values). Request handlers (`OnSummonRequest`, `OnPick`, ...) keep their checks of the sender and call the world.
Alternative: one driver component that calls every phase in `Step`. Rejected: it moves where events are flushed (the mob events and the kill events must go out in the same tick as now) and changes the network timing the integration comparisons depend on. Keeping the Unity order costs nothing and keeps the behavior.

**2. The order is data, and a test ties the servers to it.**
`MatchPhases` lists the phases in order with the execution order each server declares (`MatchPhases.MobsOrder = -100` and so on, used in the `[DefaultExecutionOrder]` attributes). A test reads the attributes by reflection and checks that they are strictly increasing in the same order as `Step` calls the phases. A new phase or a changed order that does not touch both fails the test.

**3. The scene is replaced by plain inputs, filled by the servers and by the bots.**
Each tick the server copies avatar positions, `ServerMoving`, alive flags and the boss position into the world's participants (what it already copies into `SurvivorBody`, `ShooterBody`, `SurvivorInfo` and `GrowthBody`), and requests go to the world's methods (`SetAim`, `RequestSkill`, `Summon`, `PlaceDevice`, `BossSkill`, `Pick`). The harness does the same from bots: it moves bodies (speed from the character or the boss kind, clamped by `ArenaConfig.ClampCharacter`, the same clamp the server uses) and calls the same methods.

**4. Determinism: one seed gives all the randomness, and statics are set per match.**
The harness derives the seeds for `MobSim`, `SummonSim`, `LevelingSim` and the bots from the match seed, uses the fixed step 0.02 s and never reads the wall clock. `BossCatalog.SetCurrentKind` and the value overrides (a settings file entry applied to the sims' public fields and config objects, not to `DevArgs`) are set before each match and restored after it. A test runs the same seed twice and compares the rows, and a different seed must differ.
Alternative: make the sims take their statics as constructor arguments. Rejected as a larger refactor for no gain: matches run one after another in one process.

**5. Bots are plain classes that return commands from the world's state.**
`SurvivorBot` and `BossBot` have `Decide(world, self, dt) -> BotCommand` (move vector, aim angle, firing, skill slots to use, and for the boss summon kind, device, skill keys). They read only what a player could see. The behaviors are those proposed in the exploration:
- Survivor: step sideways out of the way of a hostile shot that is about to hit (added after the first runs showed bots that never dodged were hit by every boss shot), move away from the nearest mob or boss contact when too close, otherwise hold ground, drift toward a fallen teammate to revive; aim at the boss when it is within shot range and the boss preference allows, else the nearest mob; fire whenever a target is in range; use Q/E when the mana, cooldown and a target allow; take the automatic skill choice (the existing timeout pick) or pick at random from the offer with the bot's own random.
- Boss: chase the nearest living survivor, fire at it, use Q and E when ready, summon the kinds in turn when the energy is above a threshold, place a device when ready and the limit allows.
A `SkillProfile` (weak, normal, strong) sets the aim error in degrees, a reaction delay in seconds before the bot acts on what it sees, and the boss preference. The three profiles are data and sit in one table.
Alternative: record and replay human play. Rejected: there is none to record.

**6. The runner is a plain class with an editor entry point.**
`HarnessRunner.Run(HarnessSettings)` (runtime assembly) plays the matches one after another and returns rows and a summary. The settings file is JSON (`JsonUtility`): boss kind, profile, survivor characters, number of matches, first seed, a list of named overrides (`bossHealth`, `bossSkillDamage`, `mobHealth`, `summonRegen`, `deviceInterval` and so on, each applied by a small table of setters) and an optional baseline summary path. The editor entry `BS.Editor.MatchHarnessCli.Run` reads `-bsHarness <settings path>` and `-bsHarnessOut <folder>` from the command line, so it works with `Unity -batchmode -executeMethod` and from the Unity MCP, and writes `rows.csv`, `summary.md` and `summary.json`.
Alternative: an `[Explicit]` EditMode test. Rejected: values to try change on every run, and a settings file is easier to keep and compare.

**7. Metrics are collected from the world, not from logs.**
After each step a `MatchRecorder` notes the first time the boss health fraction crosses 50%, 25% and 0%, survivor falls (a living survivor to fallen), revivals, the highest level reached, device placements, destroyed and expired devices, and at the end the result, reason and match length. The summary gives the result shares, mean, median and 10th/90th percentile of the length, the mean of the other values and, with a baseline, the change of each against it.

**8. The extraction goes one server at a time, each followed by the full EditMode run.**
Order of moves: mobs, summon, combat (survivors, mob actions, boss skills, attacks, projectiles), growth, match clock. After each, the existing 979 tests and a short multi-process comparison must still pass. The harness is built last on top of the finished `MatchWorld`.

## Risks / Trade-offs

- [The extraction changes the server's behavior in a way no test notices, e.g. an event is flushed a tick later] → keep each server's flush and write calls where they are and in the same order relative to the phase call; compare mob positions and combat events between the server and clients before and after (the existing scripts), and compare the counts a fixed-seed dev run logs.
- [Bots are far from human play, so absolute win rates mislead] → the report is for comparing values against each other and against the target shape; three profiles bracket the skill; a final check is playing by hand.
- [A match takes too long: 45 000 steps with up to 100 mobs and many projectiles] → measured: a full 900 s match takes about 0.6 s of real time, so the server's step stays the default; a coarser step is still a setting and the report marks it.
- [Static state (`BossCatalog.CurrentKind`, `DevArgs`) leaks between matches or into the editor session] → set and restore it around each match and test that the value after a run equals the value before.
- [Phase methods grow long because the servers' bodies are moved whole] → move the simulation calls and their small helpers only; leave logging and the network in the servers.

## Open Questions

- The bots' first numbers (aim error, reaction delay, boss preference, summon threshold) are starting values and are tuned together with the balance pass, so they stay in one data table.
