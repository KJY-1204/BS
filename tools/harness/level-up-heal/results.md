# Level-up heal (change `add-level-up-heal`)

Normal bots, 360 matches per boss kind on the seeds of the earlier regressions (1001 to 1360), no overrides. All rows have the bots that evade announced skills (`add-bot-skill-evasion`).

| state | boss | survivors win | ends under 200 s | length median | falls |
|---|---|---|---|---|---|
| before (no heal) | standard | 220 / 360 = 61.1% | 24 | 852 s | 10.9 |
| before (no heal) | giant | 175 / 360 = 48.6% | 67 | 819 s | 9.4 |
| before (no heal) | sniper | 225 / 360 = 62.5% | 81 | 788 s | 13.5 |
| heal 10%, boss values as before | standard | 262 / 360 = 72.8% | 0 | 845 s | 9.5 |
| heal 10%, boss values as before | giant | 230 / 360 = 63.9% | 3 | 841 s | 8.3 |
| heal 10%, boss values as before | sniper | 296 / 360 = 82.2% | 0 | 801 s | 12.0 |
| **heal 5%, standard 195000, sniper 182000 (shipped)** | standard | 223 / 360 = 61.9% (+-5.0) | 2 | 866 s | 10.2 |
| **shipped** | giant | 226 / 360 = 62.8% (+-5.0) | 12 | 830 s | 8.9 |
| **shipped** | sniper | 241 / 360 = 66.9% (+-4.9) | 6 | 852 s | 13.8 |

- The heal does what it was for: the matches that ended within 200 seconds (the whole team falling in the first minutes) fall from 24 / 67 / 81 of 360 to 2 / 12 / 6, and the first-fall cascade is gone.
- Those early ends were costing the survivors 15 to 20 points of win share, so the win share rises by about that much whatever the size of the heal: with 10% the standard and the sniper boss passed the 70% ceiling (72.8%, 82.2%), and a scan on 120 matches showed 3% and 5% still gave 70.8% / 75.8% and 71.7% / 78.3% (standard / sniper) with the old boss health. The boss was therefore retuned, not the heal.
- Scan with heal 5% (120 matches, seeds 1001+): standard health 200000 gave 55.0%, 210000 gave 28.3%; sniper 190000 gave 47.5%, 205000 gave 20.0% (the time limit of 900 s makes the win share fall steeply with the boss's health). The shipped values, 195000 for the standard boss and 182000 for the sniper, sit between the steps of the scan and were confirmed on 360 matches.
- Every kind is inside the gate (35-70%); the sniper (66.9%) is the nearest the ceiling. The medians of the standard boss (866 s) and the sniper (852 s) are over the 840 s edge of the band, as the standard boss's already was (845 to 857 s): the boss now dies at about 835 s on average with a limit of 900 s, so the matches are decided late as designed.

## Follow-up: sniper health 186000

The sniper at 182000 was the nearest kind to the ceiling (66.9%). With 186000 health, 360 matches on the same seeds: 205 / 360 = 56.9% (+-5.1), 5 matches under 200 s, median 870 s, the boss dies at 840 s on average (`sniper-186000.summary.json`). That is shipped; the three kinds are now at 61.9% (standard), 62.8% (giant) and 56.9% (sniper).
