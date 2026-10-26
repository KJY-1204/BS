# Contest reward (change `add-contest-reward`)

Normal bots, 360 matches per boss kind on seeds 1001 to 1360, no overrides. "before" is `tools/harness/leap-delay`.

| boss | before | with the reward | length median | ends under 200 s |
|---|---|---|---|---|
| standard | 173 / 360 = 48.1% | 177 / 360 = 49.2% (+-5.2) | 893 s | 3 |
| giant | 221 / 360 = 61.4% | 195 / 360 = 54.2% (+-5.1) | 822 s | 10 |
| sniper | 206 / 360 = 57.2% | 210 / 360 = 58.3% (+-5.1) | 860 s | 9 |

- Every kind stays inside the gate (35-70%). Only the giant moves beyond noise (-7 points): its leap strike, which lands on a survivor, comes back faster while its boss bot stands in a point the survivors are trying to take.
- The harness boss bot does not aim for the point, so this is a lower bound of what a player who holds the point gets.
- No value was changed.

## Follow-up: can the standard boss's median match reach the 480-840 s band?

Standard boss, 120 matches (seeds 1001+), health scan with the shipped build: 185000 gives 67.5% wins and a median of 852 s; 175000 gives 81.7% and 815 s; 165000 gives 93.3% and 769 s. The median only drops under 840 s once the survivors win about 78% of matches, which is over the 70% ceiling. The matches end near the 900 s limit because half of them are boss wins by time, so with a win share near 50-65% the median sits at 850-895 s. The two gates (wins 35-70%, median 480-840 s) cannot both be met for the standard boss with a 900 s limit; the win-share gate is kept and the median band is treated as informative for this kind. No value was changed.
