# E and R evolution pacing runs (change `extend-skill-evolutions`)

Standard boss, no boss overrides, 120 matches per row (seeds 1 to 120). The `*.settings.json` files are the harness settings and the `*.summary.json` files the results (`BS.Editor.MatchHarnessCli.RunWith(settings, folder)` with the same seeds gives the same rows).

## Natural play: the gate

No overrides: the survivors learn what the offers give them and the bots choose at random. "off" is the same build with the sixteen new evolutions made unobtainable (their prerequisite set above the maximum rank); "on" is the build as shipped.

| variant | survivors win | length median | boss dies at (mean) | boss health left (mean) | falls |
|---|---|---|---|---|---|
| normal bots, E and R evolutions off | 74 / 120 = 61.7% | 832 s | 816 s | 9.2% | 17.2 |
| normal bots, E and R evolutions on | 65 / 120 = 54.2% | 845 s | 821 s | 11.0% | 16.9 |

Weak bots barely survive a natural match (4 of 120 wins with the evolutions off), so they give no headroom to measure here.

## Every survivor starts with the evolution: a comparison that cannot be read as the gate

These runs start every survivor with the Q, E and R skills at rank 3 (the control) and with evolution 1 or 2 of the chosen slot already learned (`startEvolution` and `startEvolutionSlot`). They show how much stronger a survivor is with an evolution picked for free from the first second, which is not what the evolutions change in a real match: in the control a survivor still has to spend level ups on the evolutions, in the variants those level ups go to passives instead.

### Normal bots (a ceiling)

| variant | survivors win | length median | boss dies at (mean) | boss health left (mean) | falls |
|---|---|---|---|---|---|
| control: Q, E, R at rank 3 | 116 / 120 = 96.7% | 789 s | 784 s | 0.1% | 5.3 |
| E evolution 1 | 119 / 120 = 99.2% | 785 s | 784 s | 0.1% | 4.9 |
| E evolution 2 | 120 / 120 = 100.0% | 768 s | 770 s | 0.0% | 5.3 |
| R evolution 1 | 118 / 120 = 98.3% | 769 s | 774 s | 0.2% | 5.4 |
| R evolution 2 | 118 / 120 = 98.3% | 768 s | 771 s | 0.0% | 4.2 |
| all three slots, evolution 1 | 119 / 120 = 99.2% | 759 s | 761 s | 0.0% | 3.9 |
| all three slots, evolution 2 | 120 / 120 = 100.0% | 720 s | 722 s | 0.0% | 4.1 |

The control already wins 96.7%: the Q, E and R skills at rank 3 from the first second are far stronger than any natural build, so the variants cannot show a difference. The controls with fewer skills at rank 3 are high as well (standard boss: Q and E 98.3%, Q and R 80.0%; giant: Q alone 65.8%, Q and E 95.0%, Q and R 80.8%; their summaries are in this folder).

### Weak bots (headroom)

| variant | survivors win | length median | boss dies at (mean) | boss health left (mean) | falls |
|---|---|---|---|---|---|
| control: Q, E, R at rank 3 | 71 / 120 = 59.2% | 873 s | 851 s | 9.3% | 8.7 |
| E evolution 1 | 87 / 120 = 72.5% | 863 s | 850 s | 5.9% | 8.3 |
| E evolution 2 | 93 / 120 = 77.5% | 839 s | 831 s | 7.5% | 8.1 |
| R evolution 1 | 93 / 120 = 77.5% | 858 s | 846 s | 4.8% | 8.8 |
| R evolution 2 | 91 / 120 = 75.8% | 854 s | 840 s | 4.5% | 8.1 |
| all three slots, evolution 1 | 109 / 120 = 90.8% | 832 s | 827 s | 1.1% | 6.3 |
| all three slots, evolution 2 | 113 / 120 = 94.2% | 799 s | 798 s | 0.2% | 6.7 |

Every variant is 13 to 35 points above the control. When the values of all sixteen evolutions were pulled 40% of the way back to their base skills (the `weak2-*` summaries, same settings) the win shares went up, not down: E 77.5% and 84.2%, R 87.5% and 88.3%, all three 96.7% and 97.5%. So the rise is not carried by what the evolutions do: it is the free level ups, and the evolutions themselves cost a little against the base skills they replace. The values were therefore not changed.

## What it says

- In natural play the new evolutions do not raise the survivors' win share: 54.2% with them against 61.7% without, a drop of 7.5 points that is inside the noise of 120 matches (one standard error of the difference is about 6 points), and the median match is 845 seconds against 832.
- In natural play with normal bots, 160 survivors in 40 matches: 78 took the Q skill to its maximum rank, 49 the E skill and 10 the R skill; 47, 32 and 4 of them ended the match with an evolution of their Q, E and R skill. The R evolutions are rare because the R skill needs five choices on the way to rank 3 before an evolution can be offered.
- The weak-bot comparison is reported for the record and not as the gate: it measures the value of a free choice, not of the choice's content.

No evolution value was changed after the measurement.
