# Boss phase pacing runs (change `add-boss-phases`)

Normal bots, 120 matches per row (seeds 1 to 120), the boss values of `tune-boss-pacing`. "off" is the same build with the phases switched off (`bossPhases 0`: no burst, the third skill locked, phase 1 all match). "on" is the build as shipped (cooldown factor of phase 3 0.85, burst of 8 mobs and 12 in phase 3). The `*.settings.json` files are the harness settings (the override `bossPhases` is the only difference) and the `*.summary.json` files the results.

Accepted when the survivors win 35% to 70%, the median length is 480 s to 840 s and the boss reaches half health in at least half of the matches (the gate of `tune-boss-pacing`).

## Standard boss

| variant | survivors win | length median | boss reaches half health | phase 2 / 3 start (mean) | falls | first fall | survivors alive at the end |
|---|---|---|---|---|---|---|---|
| phases off | 82 / 120 = 68.3% | 824 s | 93% | - | 15.8 | 33 s | 3.28 |
| phases on (shipped) | 74 / 120 = 61.7% | 832 s | 93% | 385 / 622 s | 17.2 | 33 s | 3.00 |
| first values: cooldown factor 0.7 | 62 / 120 = 51.7% | 834 s | 93% | 385 / 622 s | 18.1 | 33 s | 2.59 |
| burst of 6 mobs (factor 0.85) | 77 / 120 = 64.2% | 826 s | 93% | 385 / 619 s | 17.0 | 33 s | 2.92 |

## Giant

| variant | survivors win | length median | boss reaches half health | phase 2 / 3 start (mean) | falls | first fall | survivors alive at the end |
|---|---|---|---|---|---|---|---|
| phases off | 72 / 120 = 60.0% | 804 s | 77% | - | 16.5 | 23 s | 2.79 |
| phases on (shipped) | 47 / 120 = 39.2% | 787 s | 77% | 349 / 594 s | 18.5 | 23 s | 1.88 |
| first values: cooldown factor 0.7 | 35 / 120 = 29.2% | 776 s | 77% | 349 / 594 s | 19.1 | 23 s | 1.57 |
| burst of 6 mobs (factor 0.85) | 51 / 120 = 42.5% | 786 s | 77% | 349 / 593 s | 18.5 | 23 s | 1.91 |
| no cooldown factor (1.0) | 54 / 120 = 45.0% | 795 s | 77% | 349 / 594 s | 17.8 | 23 s | 2.31 |

## Sniper

| variant | survivors win | length median | boss reaches half health | phase 2 / 3 start (mean) | falls | first fall | survivors alive at the end |
|---|---|---|---|---|---|---|---|
| phases off | 69 / 120 = 57.5% | 730 s | 64% | - | 12.7 | 23 s | 2.24 |
| phases on (shipped) | 49 / 120 = 40.8% | 738 s | 63% | 369 / 583 s | 14.6 | 23 s | 1.56 |
| first values: cooldown factor 0.7 | 47 / 120 = 39.2% | 733 s | 63% | 369 / 583 s | 15.0 | 23 s | 1.51 |
| burst of 6 mobs (factor 0.85) | 48 / 120 = 40.0% | 723 s | 63% | 369 / 587 s | 14.4 | 23 s | 1.49 |

## What it says

- With the first values (cooldown factor 0.7) the standard boss and the sniper stayed inside the gate (51.7% and 39.2% survivor wins) but the giant fell to 29.2%. Without the factor the giant is at 45%, so the factor cost it about 16 points and the burst plus the third skill about 15 more. The giant probably loses most because its Q shockwave (7 s) and E roar (14 s, it adds mobs) are the skills the factor shortens most; this was not isolated with a separate run.
- A factor of 0.85 brings all three inside the gate: standard 61.7%, giant 39.2%, sniper 40.8%, each 7 to 21 points below the match without phases, with medians of 832, 787 and 738 seconds and the boss reaching half health in 93, 77 and 63% of the matches. Survivors fall 1.4 to 2 more times per match. The giant is the closest to the lower edge (one standard error is about 4.5 points).
- Shrinking the burst to 6 mobs moves the win share by 3 points or less in either direction, inside the noise, so the burst stays at 8 and 12.
- The phases begin at 349 to 385 seconds (phase 2) and 583 to 622 seconds (phase 3): about a third and two thirds of the way through a 900 second match.

No value of the third skills was changed.
