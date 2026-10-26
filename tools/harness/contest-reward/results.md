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
