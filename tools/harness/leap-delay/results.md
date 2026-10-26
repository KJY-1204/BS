# Giant leap strike opening cooldown (change `delay-giant-leap-strike`)

Normal bots, 360 matches per boss kind on seeds 1001 to 1360, no overrides. "before" is `tools/harness/signature-skills` (leap strike ready at once).

| boss | before | shipped | length median | ends under 200 s |
|---|---|---|---|---|
| standard | 173 / 360 = 48.1% | 173 / 360 = 48.1% (+-5.2) | 896 s | 2 (2) |
| giant | 222 / 360 = 61.7% | **221 / 360 = 61.4% (+-5.0)** | 830 s (789 s) | **8 (37)** |
| sniper | 206 / 360 = 57.2% | 206 / 360 = 57.2% (+-5.1) | 859 s | 12 (12) |

- Holding the leap strike back for 20 seconds cuts the giant's matches that end within 200 seconds from 37 to 8 of 360 (the 120-match experiment gave 15 to 2). The standard boss and the sniper are identical to before (neither has an opening cooldown).
- With the leap strike held back the survivors win more (71.7% in the 120-match experiment), so the giant's health was raised 230000 to 236000: a scan gave 230000 71.7%, 236000 61.7%, 242000 56.7% (120 matches); the 360-match result is 61.4%.
- Every kind is inside the gate (35-70%): standard 48.1%, giant 61.4%, sniper 57.2%. The giant's median match (830 s) is inside the 480-840 s band again; the standard boss's (896 s) and the sniper's (859 s) are not, as before.
- An earlier test of a grace on all boss skills (0, 20, 30, 45 s on the giant, before the leap strike existed) was inconclusive and was dropped; a scan of the skill damage (30 and 20 for all skills) gave 14 and 10 of 120 early ends against 15, so the damage was not what mattered; the timing of the first strike was.
