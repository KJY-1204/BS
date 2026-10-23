# R skills pacing runs (change `add-ultimate-skills`)

Normal bots, 120 matches per row (seeds 1 to 120), no overrides, the boss values of `tune-boss-pacing`.
"off" is the same build with the R skills made unobtainable (their prerequisite set above the maximum rank); "on" is the build as shipped.
The `*.settings.json` files are the harness settings (`BS.Editor.MatchHarnessCli.RunWith(settings, folder)` with the same seeds gives the same rows) and the `*.summary.json` files are the two results.

| kind | R skills | survivors win | length median (p10 - p90) | boss at half health | first fall | falls / revives |
|---|---|---|---|---|---|---|
| standard | off | 81 / 120 = 67.5% | 829 s (705 - 900) | 95% | 33 s | 19.3 / 18.4 |
| standard | on | 85 / 120 = 70.8% | 821 s (688 - 900) | 93% | 33 s | 15.9 / 15.1 |
| giant | off | 72 / 120 = 60.0% | 808 s (64 - 900) | 79% | 23 s | 19.2 / 18.0 |
| giant | on | 74 / 120 = 61.7% | 804 s (64 - 900) | 75% | 23 s | 16.0 / 14.7 |
| sniper | off | 70 / 120 = 58.3% | 736 s (79 - 822) | 68% | 23 s | 13.9 / 12.1 |
| sniper | on | 68 / 120 = 56.7% | 748 s (79 - 841) | 68% | 23 s | 13.3 / 11.5 |

The win share moves by at most 3.3 points, which is inside the noise of 120 matches (one standard error is about 4 points), and the median length does not move. What changes is the number of falls per match, 3 fewer for the standard and giant bosses (the large heal and the big burst keep survivors up). The boss reaches half health as often as before, so the R skills hurt the boss little: its health is what the survivors' basic attacks wear down.

Why the 40-match rows of `tune-boss-pacing` read 63% for the standard boss and the 120-match "off" row 67.5%: with 40 matches one match is 2.5 points. The standard boss sits close to the time limit (about 9% of its health is left at 900 s on average), so a few matches flip with any small change; a 40-match run of the new build gave 82.5%, and that was noise of the same kind: switching the R skills of one character on at a time moved it by 1 to 3 matches each, and cutting the damage of the R skills did not lower it.

No R value was changed after the measurement.
