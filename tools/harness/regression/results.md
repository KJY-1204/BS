# Regression after the stacked features (2026-10-25)

State: rescue points with the boss contest, the three boss traits (Command, Momentum, Steady), the evolution offer guarantee, on top of the E and R evolutions, boss phases and the earlier work. Normal bots, no overrides, 360 matches per boss kind on fresh seeds (1001 to 1360). Settings and summaries are in this folder.

| boss | survivors win (95% interval) | length median | p10 / p90 | boss dies at (mean) | falls |
|---|---|---|---|---|---|
| standard | 208 / 360 = 57.8% (+-5.1) | 857 s | 743 s / 900 s | 830 s | 13.7 |
| giant | 168 / 360 = 46.7% (+-5.2) | 809 s | 65 s / 900 s | 826 s | 15.5 |
| sniper | 210 / 360 = 58.3% (+-5.1) | 795 s | 86 s / 892 s | 812 s | 13.9 |

- The survivors' win share is inside the gate (35-70%) for every kind, with the whole 95% interval inside it. The three kinds are within 11 points of each other (giant lowest, sniper highest).
- The median match is inside the 480-840 s band for the giant and the sniper. The standard boss's median is 857 s, 17 s over the edge of the band; it was 845 s before the rescue points. The match limit is 900 s and the boss dies at 830 s on average, so the standard boss's matches are decided late as designed; the band edge is the one gate this build does not meet.
- The 10th percentile of the giant's and the sniper's match length is 65 s and 86 s: about one match in ten ends within a minute and a half, which is the survivors falling early. This is not new (the same figure is in the earlier measurements back to `tools/harness/phases`: 64 s and 79 s), so it is not caused by the stacked features, but it is the weakest part of the pacing and a candidate for a later change (early safety for the survivors).
- Nothing was changed after this measurement.
