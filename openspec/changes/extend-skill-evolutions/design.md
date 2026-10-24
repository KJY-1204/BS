# Design

## Context

An evolution is a `SkillDef` of its own (see `skill-evolutions`): active, one rank, the slot and character of its base skill, `EvolvesFrom` the base skill's id, `ExclusiveWith` its sibling, and the base skill at its maximum rank as its prerequisite. `SkillCatalog.ForSlot` skips evolutions and gives the base skill; `ActiveForSlot(slot, character, ranks)` gives the evolution of the slot's base skill that the ranks say is learned, else the base skill; `SkillSim.TryActivate`, the skill bar and every rule built on them already use it for any slot. The skill event carries the skill id and the client draws from the skill's data. `IsAvailable` already has the exclusion. So what an E or R evolution needs is mostly data.

The one thing that is not generic is the rank packing. A survivor's ranks travel to the owner as 3 bits per skill id in two 64-bit values: ids 1 to 21 in the first and 22 to 42 in the second, `MaxSkillId = 42`. Ids 1 to 40 are used (4 common passives, 8 Q and E skills, 16 own passives, the 4 R skills and the 8 Q evolutions). The sixteen new evolutions need 41 to 56. `LevelingSim` and `PlayerGrowth` size their rank arrays by `MaxSkillId`, `PlayerGrowth` holds one replicated value for each packed value, and `GrowthServer` writes both when a rank changes. Sixteen tests call the two-value packing and unpacking.

See proposal.md for why. The exploration chose: E and R together (sixteen evolutions), the Q evolutions left as they are, a third 64-bit value for the packing.

## Goals / Non-Goals

**Goals:**
- Sixteen evolutions as data on the rules that already exist, with no new mechanism for firing, offering, drawing or excluding.
- A packing that takes any number of 64-bit values, so the next time skills are added it is a constant and not a rewrite.
- Slot-by-slot measurements with the harness, so a slot that moves the pacing is found by itself.

**Non-Goals:**
- More ranks per evolution, new effect kinds, a guaranteed offer, changing the Q evolutions.
- A denser packing (a bitset for the one-rank evolutions): it would split the rule in two for the sake of a few bytes sent when a rank changes.

## Decisions

**1. The packing works on a list of values.**
`SkillCatalog.PackedValues = 3` and `MaxSkillId = 63` (21 ids to a value, ids 1 to 63). `Pack(ranks, index)` gives value `index`; `PackLow` and `PackHigh` stay as the names of the first two and `PackThird` is added; `Unpack(IReadOnlyList<ulong> values, int[] into)` replaces the two-value `Unpack`. The sixteen tests that used the old signature are updated; the edge tests gain the edge between ids 42 and 43. The rank arrays follow `MaxSkillId` by themselves.
Alternative: keep two values and give the evolutions a separate field in the owner's data. Rejected: a second place to look up a rank for the same kind of thing, and every rule that reads ranks (`RankOf`, `IsAvailable`, the HUD) would need to know about it.

**2. One more replicated value, written with the others.**
`PlayerGrowth` gets a third owner-only 64-bit variable and unpacks all three when any of them changes; `ServerWrite` takes the three values and `GrowthServer.WriteReplicated` packs them. A rank change is rare (a level up), so one more 8-byte value costs nothing measurable.

**3. The ids and values.**
E evolutions are 41 to 48 (Reimu's barrier, Marisa's Stardust, Sakuya's knives, Sanae's wind veil, two each in that order) and R evolutions 49 to 56 (the burst, the beam, the knives, the heal). Each has the slot of its base skill, one rank, the base skill at its maximum rank as prerequisite, and its sibling as exclusion. Starting values, to be checked by the harness (the base skill's maximum rank for comparison):

| Skill (rank 3) | Evolution A | Evolution B |
|---|---|---|
| Reimu E barrier: 2.5 s, mana 30, cooldown 14 | long: 4 s, mana 45, cooldown 22 | short: 1.2 s, mana 15, cooldown 7 |
| Marisa E Stardust: 6 s at 0.4 of the cooldown, mana 25, cooldown 12 | long: 8 s at 0.55, mana 35, cooldown 18 | rapid: 3 s at 0.3, mana 20, cooldown 12 |
| Sakuya E knives: 20, damage 14, mana 30, cooldown 9 | many: 28, damage 8, mana 40, cooldown 12 | focused: 8, damage 24, mana 25, cooldown 7 |
| Sanae E wind veil: radius 4, push 5, mana 25, cooldown 10 | wide: radius 6.5, push 4, mana 35, cooldown 14 | impact: radius 3, push 8, mana 25, cooldown 10 |
| Reimu R burst: radius 7, damage 140, mana 70, cooldown 40 | wide: radius 10, damage 90, mana 85, cooldown 50 | focused: radius 4.5, damage 220, mana 70, cooldown 35 |
| Marisa R beam: 16 long, half width 1.6, damage 200, mana 80, cooldown 45 | broad: 14 long, half width 2.4, damage 160, mana 90, cooldown 50 | piercing: 24 long, half width 1.0, damage 240, mana 80, cooldown 45 |
| Sakuya R knives: 60, damage 16, mana 70, cooldown 40 | dense: 90, damage 11, mana 85, cooldown 50 | precise: 36, damage 30, mana 60, cooldown 30 |
| Sanae R heal: radius 8, amount 140, mana 70, cooldown 45 | wide: radius 12, amount 110, mana 85, cooldown 55 | quick: radius 8, amount 90, mana 55, cooldown 30 |

Each is worse than its base skill in at least one value and every R evolution keeps a higher cost and a longer cooldown than every base Q and E skill of its character (the cheapest, Sanae's quick heal, costs 55 against 35 and 25 and cools down in 30 against 12 and 10).

**4. The R rule compares base skills.**
The `ultimate-skills` rule that an R skill costs more, cools down longer and reaches farther or hits harder than the Q and E skills now says "base" in the text, so no evolution of the Q, E or R skills is part of the comparison, and a new rule keeps the R evolutions ultimates against the base Q and E skills. The data tests follow.

**5. The slot code does not change.**
`ForSlot`, `ActiveForSlot`, the exclusion, `SkillSim`, the skill event and the client drawing already take any slot. The skill bar draws the name of the skill a slot fires in a box that wraps to two lines; the longest of the new names are checked in a captured window.

**6. Development and measurement tools follow the slots.**
`-bsDevEvolve <1|2>` gives every survivor evolution 1 or 2 of the Q, E and R skills (it gave the Q evolution only; the grant already gives every slot its base skill). The harness gets `startERank` and `startRRank` (the rank of the E and R skill every survivor starts with) and `startEvolutionSlot` (0 Q, 1 E, 2 R: the slot `startEvolution` applies to; 0 when not given, which is what it did). With them each slot's evolutions are compared with a control that has the same base skills at rank 3 and no evolution.

**7. Balance is measured slot by slot and the old evolutions are left alone.**
The standard boss, normal bots, 120 matches (360 where a result lands near the line), for: the control (Q, E and R at rank 3, no evolution), then every survivor with an evolution 1 or 2 of the E skill only, of the R skill only, and of all three slots together (the last reported, not gated). The Q evolutions' values are not changed. The acceptance for each of the two new slots is the one of the first evolution change: the survivors' win share moves by no more than 10 points from the control, and the boss's median clear time and the falls stay within the noise; if a slot does, its values are adjusted (the boss is not). The measurement also reports how many survivors end a natural match with an evolution of each slot.

**8. Traffic.**
The biggest burst is the dense ring of 90 knives; the existing worst-case traffic test and the one-tick chunk test are extended with it and with four survivors using their R skills at once.

## Risks / Trade-offs

- [The sixteen evolutions on top of the eight lift the survivors' win share past the pacing gate, as the Q evolutions already did by about 10 points] → measured per slot with the control; the new values are fitted, the old ones are not changed; the full set together is reported.
- [More candidates on a level up make an evolution rarer in natural play] → the natural reach is measured and reported; a guaranteed offer stays out of scope.
- [A packing change breaks a rank somewhere that only looked at two values] → tests that pack and unpack every id from 1 to 63 and the edges, and a multi-process run where the owner's client logs the ranks of ids above 42.
- [The 90-knife ring crowds the projectile cap] → the ring already fires the knives that fit; the traffic and chunk tests cover it.
- [Long evolution names crowd the skill bar] → checked in a captured window; shortened if cut.

## Open Questions

- Whether the third packed value should be reserved with room for more ids now (it holds ids 43 to 63; ids 57 to 63 are free).
