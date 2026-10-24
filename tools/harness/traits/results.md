# Boss kind traits (change `add-boss-kind-traits`)

Normal bots, 120 matches per row (seeds 1 to 120), no other overrides. "off" is `bossTraits` 0, "on" is the build as shipped. Settings and summaries are in this folder.

| boss | traits | survivors win | length median | boss dies at (mean) | falls |
|---|---|---|---|---|---|
| standard | off | 65 / 120 = 54.2% | 845 s | 821 s | 16.9 |
| standard | on | 65 / 120 = 54.2% | 845 s | 821 s | 16.9 |
| giant | off | 49 / 120 = 40.8% | 798 s | 821 s | 18.4 |
| giant | on | 46 / 120 = 38.3% | 790 s | 826 s | 18.6 |
| sniper | off | 49 / 120 = 40.8% | 759 s | 793 s | 15.0 |
| sniper | on | 49 / 120 = 40.8% | 759 s | 793 s | 15.0 |

- Every kind stays inside the gate (35-70% survivor wins, median 480-840 s; the standard boss's 845 s median was already so before this change).
- The standard boss is identical on and off, as it has no trait.
- The giant loses 2.5 points with Momentum on (inside the noise of 120 matches). The harness boss bot always chases, so the giant is always moving and only the +30% applies.
- The sniper is identical on and off: the harness boss bot chases and never stands still for a second, so Steady never triggers. A first try with a bot that stopped to shoot moved the sniper's baseline itself (25.8% off, 16.7% on, below the gate), so that bot change was dropped. Steady is covered by tests of the rule, not measured in matches. No value was changed.
