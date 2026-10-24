# Rescue points (change `add-rescue-points`)

Normal bots, 120 matches per row (seeds 1 to 120), no other overrides. "off" is `rescuePoints` 0, "on" is the build as shipped (survivor bots walk to an open point when nothing is near them). Settings and summaries are in this folder.

| boss | points | survivors win | length median | boss dies at (mean) | falls |
|---|---|---|---|---|---|
| standard | off | 65 / 120 = 54.2% | 845 s | 821 s | 16.9 |
| standard | on | 82 / 120 = 68.3% | 841 s | 825 s | 13.4 |
| giant | off | 46 / 120 = 38.3% | 790 s | 826 s | 18.6 |
| giant | on | 57 / 120 = 47.5% | 804 s | 821 s | 15.5 |
| sniper | off | 49 / 120 = 40.8% | 759 s | 793 s | 15.0 |
| sniper | on | 72 / 120 = 60.0% | 782 s | 801 s | 13.2 |

- Every kind with points on is inside the gate (35-70% survivor wins). The standard boss at 68.3% is close to the upper edge, and its median match (841 s, 845 s without points) is at the edge of the 480-840 s band as it was before this change.
- The points raise the survivors' win share by 9 to 19 points and cut falls by 2 to 3.5 per match: the heal is worth more than the shooting time the bots lose. Boss death time does not move (the bots still kill the boss at about the same moment), so the effect is the survivors staying up.
- Values were not changed. If a later change raises the survivors' strength, this is the first place to look (heal 50%, slow 0.6).
