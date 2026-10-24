# Evolution offer guarantee (change `guarantee-evolution-offer`)

Natural play, normal bots, 120 matches per row (seeds 1 to 120), the state after `add-standard-boss-trait`. "no guarantee" is that measurement (`tools/harness/standard-trait`, `tools/harness/rescue-contest`: the guarantee did not exist yet), "guarantee" is this build.

| boss | no guarantee | guarantee | length median | falls |
|---|---|---|---|---|
| standard | 71 / 120 = 59.2% | 74 / 120 = 61.7% | 848 s (850 s) | 14.3 (14.1) |
| giant | 55 / 120 = 45.8% | 48 / 120 = 40.0% | 811 s (819 s) | 15.6 (15.7) |
| sniper | 64 / 120 = 53.3% | 64 / 120 = 53.3% | 771 s (767 s) | 12.8 (13.3) |

- Every kind stays inside the gate (35-70%). The differences (+2.5, -5.8, 0 points) are inside the noise of 120 matches; the giant is the one nearest the 35% edge (40.0%).
- No value was changed.
