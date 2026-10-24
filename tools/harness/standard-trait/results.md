# Standard boss trait Command (change `add-standard-boss-trait`)

Normal bots, standard boss, 120 matches per row (seeds 1 to 120), rescue points and the contest on (the state after `add-rescue-point-contest`). "off" is `bossTraits` 0.

| trait | survivors win | length median | boss dies at (mean) | falls |
|---|---|---|---|---|
| off | 82 / 120 = 68.3% | 846 s | 826 s | 13.9 |
| Command, mobs 25% faster | 71 / 120 = 59.2% | 850 s | 823 s | 14.1 |

- Inside the gate (35-70%) and off the ceiling: the standard boss goes from 68.3% to 59.2%.
- A first version, summon energy refilling 40% faster, gave 95 / 120 = 79.2% (median 816 s, falls 12.8) and was dropped: more mobs are more experience for the survivors, so the boss helped them. Faster mobs do not give extra experience.
- The giant and the sniper have no summon-speed trait, so their matches are identical to the contest measurement (`tools/harness/rescue-contest`: giant 45.8%, sniper 53.3%).
- The median match of the standard boss (850 s) is a few seconds over the 840 s edge of the band, as it already was before this change (846 s, 845 s without points); the boss reaches half health in every match.
