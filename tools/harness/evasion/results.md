# Bot skill evasion (change `add-bot-skill-evasion`)

Normal bots, 360 matches per boss kind on the seeds of the earlier regression (1001 to 1360), no overrides. "before" is `tools/harness/regression` (bots ignore announced skills), "evasion" is this build.

| boss | survivors win before | evasion (95% interval) | length median | falls per match | ends under 200 s |
|---|---|---|---|---|---|
| standard | 208 / 360 = 57.8% | 220 / 360 = 61.1% (+-5.0) | 852 s (857 s) | 10.9 (13.7) | 24 |
| giant | 168 / 360 = 46.7% | 175 / 360 = 48.6% (+-5.2) | 819 s (809 s) | 9.4 (15.5) | 67 |
| sniper | 210 / 360 = 58.3% | 225 / 360 = 62.5% (+-5.0) | 788 s (795 s) | 13.5 (13.9) | 81 |

- The bots that step out of announced shockwaves and beams fall less (giant 15.5 to 9.4 per match, standard 13.7 to 10.9) and win a little more (+1.9 to +4.2 points, inside the noise of 360 matches). Every kind stays inside the gate (35-70%); the standard boss's median is 852 s (857 s before), still over the 840 s edge of the band.
- The early ends are not removed: 67 of 360 giant matches and 81 of 360 sniper matches still end within 200 s (the 10th percentile of the length is 84 s and 78 s, as before). So the announced skills are not what wipes the team early. The earlier experiments showed that the boss's skills, contact and shots all take part (each one set to 1 damage cut the early ends to a third), which points at the whole team being caught together in the first minute, for instance rescuers standing still beside a fallen teammate near the boss. That is the next thing to look at; it is a bot or opening problem, not an evasion one.
- No game value was changed.
