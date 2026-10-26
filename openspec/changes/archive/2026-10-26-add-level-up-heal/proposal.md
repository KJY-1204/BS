# Proposal

## Why

Survivors have no way to recover health early in a match. Their health only goes down: the only recoveries are a revival, a Sanae heal and, from 90 seconds, a rescue point. The harness shows what that does: in about one match in six of the giant and the sniper, all four survivors fall within the first one to two minutes, while the boss is still at 96 to 99% health (a time series of one such match shows the team's health falling steadily and the boss barely scratched). Neither the boss's skills (a grace for them did not help), the bots' avoidance of them, nor the rescue behaviour is the cause; the cause is attrition with no recovery. Survivors also level up fast early (about level 8 after a minute), which makes the level-up the natural moment to give something back.

## What Changes

- A survivor who levels up is healed by 5% of their maximum health (a fraction of the new maximum, rounded, at least 1). A fallen survivor is not healed and not revived by it. Nothing else about a level-up changes.
- The heal is done by the server's growth step, so it applies to every match with a boss and to practice matches alike, and clients see it through the health they already replicate.
- The harness gets an override `levelUpHeal` (the fraction, 0 to 1; 0 turns the heal off, 0.05 is the default) so the effect can be measured against the earlier behaviour.
- Because the early wipes were costing the survivors 15 to 20 points of win share, removing them lifts the win share by about that much whatever the heal; the boss's health is retuned to keep the gate (standard 190000 to 195000, sniper 170000 to 182000; the giant is unchanged) instead of shrinking the heal to nothing. The default fraction is 5%.
- The balance gate stays: 35-70% survivor wins and a median match of 480-840 s for the standard, giant and sniper boss, plus the share of matches that end within 200 seconds before and after. Survivors getting stronger can push the standard and sniper boss (61% and 62%) toward the ceiling, so the fraction is chosen with that in mind.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `survivor-leveling`: a level-up heals the survivor by a fraction of their maximum health, with a harness switch, and the "no healing" wording of the stats requirement is reworded.
- `character-data`: the per-level growth scenario no longer says health is not healed.

## Impact

- `GrowthConfig` (the default fraction), `BossCatalog` (the standard and sniper health), `MatchWorld` (the heal on a level-up event and the override), `HarnessSettings` (`levelUpHeal`), tests (including level-up tests that assume health does not change), README, `tools/harness/level-up-heal/`.
