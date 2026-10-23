# Boss pacing runs (change `tune-boss-pacing`)

Values chosen with the match harness (40 matches per row, seeds 1 to 40, no overrides: the values are in `BossCatalog`).
The `*.settings.json` files are the harness settings that made each `*.summary.json`; rerun one with
`BS.Editor.MatchHarnessCli.RunWith(settings, folder)` and the same seeds give the same rows.

| kind | speed | health | other |
|---|---|---|---|
| standard | 4.5 (was 6) | 190000 (was 6000) | none |
| giant | 4.0 (was 4.5) | 230000 (was 9000) | none |
| sniper | 5.0 (was 8) | 170000 (was 4500) | basic attack 5 (was 12), beam 30 (was 45) |

Survivors move at 5.5 (Marisa, Sanae), 6 (Reimu) and 7 (Sakuya).

## Accepted: normal bots

Accepted when the survivors win 35% to 70%, the median length is 480 s to 840 s and the boss reaches half health in at least half of the matches.

| kind | survivors win | length median (p10 - p90) | boss at half health | first fall | falls / revives |
|---|---|---|---|---|---|
| standard | 63% | 820 s (687 - 900) | 93% of matches, at 500 s | 35 s | 19.6 / 18.5 |
| giant | 60% | 789 s (85 - 900) | 78%, at 463 s | 33 s | 18.7 / 17.4 |
| sniper | 55% | 749 s (76 - 846) | 70%, at 474 s | 26 s | 14.4 / 12.6 |

## Reported, not gated: weak and strong bots

| kind | profile | survivors win | length median (p10 - p90) | boss at half health | first fall |
|---|---|---|---|---|---|
| standard | weak | 5% | 108 s (43 - 900) | 35% | 21 s |
| standard | strong | 75% | 778 s (107 - 856) | 88% | 40 s |
| giant | weak | 5% | 62 s (31 - 809) | 13% | 15 s |
| giant | strong | 53% | 758 s (69 - 840) | 68% | 28 s |
| sniper | weak | 18% | 417 s (78 - 900) | 48% | 30 s |
| sniper | strong | 15% | 119 s (60 - 752) | 18% | 22 s |

The three profile values change together (aim error, reaction time, boss preference), so a higher skill is not always better for the survivors: a strong sniper fight is worse than a normal one because the strong bots go close to the boss.

## How far the plateau reaches (normal bots)

| kind | variation | survivors win | length median (p10 - p90) | boss at half health | first fall |
|---|---|---|---|---|---|
| standard | chosen | 63% | 820 s (687 - 900) | 93% | 35 s |
| standard | speed +10% | 30% | 784 s (71 - 900) | 65% | 25 s |
| standard | speed -10% | 93% | 781 s (717 - 883) | 100% | 43 s |
| standard | health +20% | 25% | 900 s (836 - 900) | 95% | 35 s |
| standard | health -20% | 93% | 701 s (619 - 743) | 95% | 35 s |
| giant | chosen | 60% | 789 s (85 - 900) | 78% | 33 s |
| giant | speed +10% | 38% | 785 s (64 - 900) | 65% | 23 s |
| giant | speed -10% | 83% | 758 s (582 - 839) | 90% | 29 s |
| giant | health +20% | 10% | 900 s (90 - 900) | 75% | 32 s |
| giant | health -20% | 83% | 668 s (108 - 731) | 83% | 32 s |
| sniper | chosen | 55% | 749 s (76 - 846) | 70% | 26 s |
| sniper | speed +10% (5.5) | 20% | 120 s (55 - 839) | 33% | 18 s |
| sniper | speed -10% | 78% | 758 s (266 - 836) | 80% | 49 s |
| sniper | health +20% | 18% | 891 s (74 - 900) | 70% | 26 s |
| sniper | health -20% | 60% | 621 s (76 - 698) | 63% | 26 s |

Health moves the win share smoothly (about 2% per 1000 health for the standard boss) and is the dial to turn after hand play. Speed
is the lever with a cliff: the sniper collapses once it is as fast as the slowest survivor (5.5), which is the line the data test
and the spec ("No boss is as fast as a survivor") keep.
