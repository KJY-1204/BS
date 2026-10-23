# Evolution pacing runs (change `add-skill-evolutions`)

Standard boss, normal bots, no boss overrides, seeds 1 to N. The control starts every survivor with the Q skill at rank 3 (`startQRank 3`); a variant also starts every survivor with evolution 1 or 2 of it (`startEvolution`), or only the survivor of one role (`startEvolutionRole`). The `*.settings.json` files are the harness settings and the `*.summary.json` files the results (`BS.Editor.MatchHarnessCli.RunWith(settings, folder)` with the same seeds gives the same rows).

## Every survivor with an evolution, 360 matches

| variant | survivors win | length median | boss dies at (mean) | boss health left at the end (mean) | falls |
|---|---|---|---|---|---|
| control (Q rank 3, no evolution), 360 matches | 248 / 360 = 68.9% | 865 s | 832 s | 1.8% | 7.7 |
| every survivor with evolution 1, 360 matches | 283 / 360 = 78.6% | 850 s | 825 s | 1.3% | 7.0 |
| every survivor with evolution 2, 360 matches | 294 / 360 = 81.7% | 838 s | 820 s | 1.1% | 7.5 |

## One survivor at a time, 120 matches

| variant | survivors win | length median | boss dies at (mean) | boss health left at the end (mean) | falls |
|---|---|---|---|---|---|
| control, 120 matches | 78 / 120 = 65.0% | 868 s | 833 s | 2.0% | 7.8 |
| only Reimu with evolution 1, 120 matches | 80 / 120 = 66.7% | 866 s | 829 s | 1.9% | 8.1 |
| only Reimu with evolution 2, 120 matches | 88 / 120 = 73.3% | 859 s | 836 s | 1.5% | 7.7 |
| only Marisa with evolution 1, 120 matches | 90 / 120 = 75.0% | 861 s | 831 s | 1.2% | 7.6 |
| only Marisa with evolution 2, 120 matches | 88 / 120 = 73.3% | 859 s | 829 s | 1.2% | 7.9 |
| only Sakuya with evolution 1, 120 matches | 90 / 120 = 75.0% | 856 s | 830 s | 1.6% | 7.8 |
| only Sakuya with evolution 2, 120 matches | 76 / 120 = 63.3% | 868 s | 829 s | 1.6% | 7.7 |
| only Sanae with evolution 1, 120 matches | 91 / 120 = 75.8% | 855 s | 834 s | 1.4% | 6.9 |
| only Sanae with evolution 2, 120 matches | 90 / 120 = 75.0% | 862 s | 837 s | 1.5% | 6.9 |

## What it says

- With every survivor holding an evolution from the first second, the survivors win 9.7 points (evolution 1) and 12.8 points (evolution 2) more often than the control. One standard error of the difference is about 3 points, so the rise is real, and it is a little over the 10 points the task named for evolution 2.
- The cause is small. The boss dies 7 to 12 seconds earlier on average and has less health left at the end (1.8% in the control, 1.3% and 1.1% with the evolutions): the evolutions add about 1% of the boss's health in damage over a match, and the falls do not change. The standard boss is tuned to die near the time limit (the median kill is at 840 s of 900), so that 1% turns about one match in ten from a boss win into a survivor win. The same cliff made the R skills look different in 40-match runs.
- No single evolution is an outlier: with one survivor evolved the win share is 63 to 76%, all within the noise of 120 matches (one standard error is about 4 points) of each other and of the control. A rise of about 10 points for all four together, and 1 to 3 points each, is what a 1% change in boss damage spread over four skills looks like.
- In natural play (no overrides, 40 matches, normal bots that choose among the offered skills at random): 75 of 160 survivors took the Q skill to rank 3 and 46 of those 160 ended the match with an evolution.

No evolution value was changed after the measurement.
