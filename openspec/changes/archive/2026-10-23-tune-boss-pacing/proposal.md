# Proposal

## Why

The match is 15 minutes long and survivors grow to level 20, but a match lasts about 30 seconds: the boss wipes the survivors long before they grow, whichever kind it is. The match harness (change `add-match-harness`) measured this with bots: 0 of 60 matches won by the survivors, median length 31 s, first survivor down after about 10 s. Switching the boss's damage sources off one at a time showed the cause. The boss moves as fast as the survivors (6 against 5.5 to 7), so it catches one and stays on it, and its shots and contact damage finish a survivor in seconds; with the boss slower than the survivors the same bots play 12 to 14 minute matches decided late, and with the boss's health far above its current 6000 the survivors need those minutes to wear it down. The aim for the game is long matches decided late, so the boss kinds need new numbers.

## What Changes

- Every boss kind moves slower than every survivor: standard 6 to 4.5, giant 4.5 to 4.0, sniper 8 to 5.0 (the survivors move at 5.5 to 7). The order giant, standard, sniper stays.
- Every boss kind has far more health: standard 6000 to about 190000, giant 9000 to about 220000, sniper 4500 to about 130000. The final values are set with the harness (see design).
- The sniper's damage is cut so its speed can stay above the others': basic attack 12 to about 4, beam 45 to about 25 (the shot ring and the rate of fire stay).
- The boss's health is told to the clients in a wider value, so a bar and a number can show health above 65535 exactly.
- The boss kinds' spec no longer says the standard kind has the values the boss had before kinds existed, and says instead that no boss moves as fast as the slowest survivor.
- The values are recorded as a harness run (a baseline summary and the settings that made it) so later changes can be compared with them.
- Not changed: damage of the standard and giant bosses, their cooldowns and skills, mobs, the summon, devices, survivor growth. The experience from summoned mobs and the harness's dodge/kite limits are left for a follow-up if hand play shows they matter.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `boss-kinds`: the standard kind is no longer defined as the old boss; a new requirement says every boss is slower than the slowest survivor.
- `boss-combat`: the boss's health shown to every client is exact for any health the data can give, including beyond 65535.

## Impact

- Code: `BossCatalog` (health and speed of the three kinds, the sniper's attack and beam damage), `PlayerCombat` (the replicated health and maximum health become wide enough), the README and the dev argument note for `-bsBossHealth`.
- Tests: the boss kind tests that assert the standard kind equals the old boss, and the per-kind speed checks, are changed to the new values and to the slower-than-survivors rule; a test that a health of 250000 passes through the replication write and reads back whole.
- Network: the boss health value in the replicated state grows from 2 to 4 bytes (survivors use the same component, so their health value grows too); no new message.
- Balance: the numbers are tuned for the harness's bots. People dodge and kite better than the bots, so the real game is likely easier than the harness says; a hand play of a few matches after this change decides whether a small follow-up change is needed.
