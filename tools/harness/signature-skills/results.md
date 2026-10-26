# Boss signature skills (change `add-boss-signature-skills`)

Normal bots, 360 matches per boss kind on seeds 1001 to 1360, no overrides. The bots evade announced shockwaves and beams.

| boss | before (shipped state) | first values | shipped values |
|---|---|---|---|
| standard (rally on F) | 223 / 360 = 61.9% | 173 / 360 = 48.1% (+-5.2), median 896 s, ends under 200 s: 2 | same skill, unchanged |
| giant (leap strike on Q) | 226 / 360 = 62.8% | 272 / 360 = 75.6% (+-4.4), ends under 200 s: 24 | **222 / 360 = 61.7% (+-5.0)**, median 789 s, ends under 200 s: 37 |
| sniper (targeted shot on E) | 241 -> 205 / 360 = 56.9% (186000 health) | 206 / 360 = 57.2% (+-5.1), median 859 s | same skill, unchanged |

- The first giant values (radius 5, cooldown 9 s, warning 1.4 s) made the leap strike much weaker than the old boss-centred Q it replaced, because a long warning on a spot the survivor can leave is easy to dodge and the bots dodge: the giant went to 75.6%, over the ceiling. The shipped values (radius 5.5, cooldown 7 s, warning 1.1 s, damage 40) brought it to 61.7%; a 120-match check of the same values gave 64.2%.
- The standard boss lost about 14 points with the rally on F (more mobs around the survivors in phase 2 are real pressure even though they also give experience), which is the middle of the gate rather than near the ceiling it was at before (61.9%).
- All three kinds are inside the gate (35-70%): standard 48.1%, giant 61.7%, sniper 57.2%. The standard boss's median match is 896 s (limit 900 s): its matches are decided at the very end, so the median band (480-840 s) is not met for it, as before.
- The matches that end under 200 s are 2 (standard), 37 (giant) and 12 (sniper) of 360: the leap strike lands on a survivor from the first moments of a match and the giant's early ends rose again from 12 to 37 (they were 67 before the level-up heal). Not tuned further in this change.

Network check (dev build, boss host plus three survivors, `-bsAutoBossSkill qef 2`, `-bsCombatDump`, 50 s, each kind): 0 exceptions, and `tools/compare_combat.py` reports every client matching the server with 0 missing and 0 extra events (including the skill events that carry the announced place and aim: 14, 15 and 14 skill events per kind).
