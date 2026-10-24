# Rescue point contest (change `add-rescue-point-contest`)

Normal bots, 120 matches per row (seeds 1 to 120). "no contest" is the earlier measurement of `add-rescue-points` (`tools/harness/rescue/*-on`, same seeds), "contest" is this build (the boss's body in the circle stops the progress). The harness boss bot is unchanged: it chases the nearest survivor.

| boss | no contest | contest | length median | falls |
|---|---|---|---|---|
| standard | 82 / 120 = 68.3% | 82 / 120 = 68.3% | 846 s (841 s) | 13.9 (13.4) |
| giant | 57 / 120 = 47.5% | 55 / 120 = 45.8% | 819 s (804 s) | 15.7 (15.5) |
| sniper | 72 / 120 = 60.0% | 64 / 120 = 53.3% | 767 s (782 s) | 13.3 (13.2) |

- Every kind stays inside the gate (35-70%). Values in brackets are the no-contest run.
- The contest takes 0, 2 and 8 matches of 120 from the survivors. The standard boss is unchanged because its bot rarely stands in the circle while survivors do; the sniper (fast, shoots from range) is contested most.
- The bot does not aim for the point, so the numbers are a lower bound of what a boss player who goes to the point does. The standard boss stays next to the 70% ceiling; the first knobs if it needs to come down are the heal (50%) and the capture time (12 s), not the contest.
- No value was changed.
