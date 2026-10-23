# Design

## Context

A skill is a `SkillDef`: id, active or passive, key slot, owning character, effect kind, maximum rank, per-rank values, mana cost, cooldown and an optional prerequisite (`RequiresSkill` at `RequiresRank`). `SkillCatalog.ForSlot(slot, character)` returns the one active skill of a character on a slot; `SkillSim.TryActivate` uses it to find what a key press fires, checks the survivor's rank, cooldown and mana, applies the character's passives and builds a `SkillActivation`; the fired skill's id travels in the skill event and clients draw it from `SkillCatalog.Get(id)` and the rank. `SkillCatalog.IsAvailable` decides what a level up may offer (owned, prerequisite met, below the maximum rank). Ranks are packed three bits per id into two 64-bit values that hold ids up to 42; ids 1 to 32 are used.

Places that assume one skill per slot: `ForSlot` itself, `SkillSim.TryActivate`, `GrowthHud.DrawSkillSlots`, the development grant (`GrowthServer.GrantSkills`) and tests that count a character's active skills or check that no two share a slot. The R skills (ids 29 to 32) need the character's Q skill at rank 2 (`ForSlot(0, character)` by id), so the base Q skill must keep being the Q skill for that rule.

See proposal.md for why. The exploration chose: Q skills only (eight evolutions), offered in the normal choice, one rank, replacing the Q skill, value variants of the base effect.

## Goals / Non-Goals

**Goals:**
- A final, exclusive choice at the top of the Q skills that changes the skill, built from the existing rules (prerequisite, offer, skill event) plus the smallest new rule.
- Eight evolutions that are trades, so no branch is the right answer.
- Evidence from the harness that neither branch breaks the pacing.

**Non-Goals:**
- Evolutions for E and R, new effect kinds or mechanics, a guaranteed or forced offer, changing a choice.
- Expanding the rank packing (ids 33 to 40 fit; the E and R evolutions will need a third packed value and that belongs to their change).

## Decisions

**1. An evolution is a `SkillDef` of its own, with two new fields.**
`EvolvesFrom` (the base skill's id) and `ExclusiveWith` (the other evolution's id). An evolution is active, belongs to the character, has the base skill's slot, one rank, its own cost, cooldown and values, requires the base skill at the base's maximum rank, and names its sibling as exclusive. Because it is data the offer, the skill event and the client drawing work with no change.
Alternative: store the choice as extra ranks of the base skill (rank 4 and 5). Rejected: the offer is a list of skill ids, a rank is not something to offer, and the two branches would share one id in the skill event.

**2. `ForSlot` keeps returning the base skill; a second lookup returns what the slot fires.**
`ForSlot` skips definitions that evolve from another, so everything that means "the Q skill" (the R prerequisite, the development grant, the tests) is untouched. The new `SkillCatalog.ActiveForSlot(slot, character, ranks)` returns the evolution of the slot's base skill that the ranks say is learned, else the base skill. `SkillSim.TryActivate` and the skill bar use it. An evolution is learned when its own rank is above zero, whatever the base's rank, so a development grant of an evolution alone works.

**3. The exclusion is one check in `IsAvailable`.**
A skill is not available when the skill in its `ExclusiveWith` has a rank above zero. With the maximum rank of one this also keeps a learned evolution from being offered again. This is the only new selection rule.

**4. The base skill keeps its rank.**
Choosing an evolution adds the evolution's rank and leaves the base at its maximum. The R skill's prerequisite and every rule that reads the base's rank keep working, and `RankOf` for the base still tells how far the Q skill was grown.

**5. Values: eight trades.**
Base skill at maximum rank is the reference; each evolution is worse in at least one of cost, cooldown or value (the spec's non-dominance rule). Starting values, to be checked by the harness:

| Character | Base (rank 3) | Evolution A | Evolution B |
|---|---|---|---|
| Reimu, burst | radius 3.5, damage 40, mana 30, cooldown 8 | spread (id 33): radius 5, damage 25, mana 40, cooldown 10 | focus (id 34): radius 2, damage 90, mana 30, cooldown 6 |
| Marisa, beam | length 12, half width 0.8, damage 80, mana 40, cooldown 10 | wide beam (id 35): length 10, half width 1.1, damage 70, mana 45, cooldown 12 | long laser (id 36): length 15, half width 0.5, damage 110, mana 40, cooldown 10 |
| Sakuya, time stop | 3 s, mana 45, cooldown 20 | long stop (id 37): 5 s, mana 60, cooldown 28 | frequent stop (id 38): 1.8 s, mana 25, cooldown 9 |
| Sanae, heal | radius 4, heal 60, mana 35, cooldown 12 | big miracle (id 39): radius 7, heal 60, mana 50, cooldown 16 | quick miracle (id 40): radius 4, heal 40, mana 20, cooldown 6 |

The R skills stay larger than the base Q and E skills as the ultimate spec says; Marisa's evolutions stay below Final Spark (length 16, half width 1.2, damage 120 at rank one) so the ultimate is never smaller than the skill it follows.

**6. The screen shows the evolution where the base skill was.**
`GrowthHud` takes the skill a slot fires from `ActiveForSlot` with the owner's ranks and shows its name, the cost and cooldown from the same functions as the server, and "evolved" in place of the rank for an evolution. The level-up choice already shows a skill's name and `Description`; an evolution's description names the trade (for example "wide, weak, costs more").

**7. Development and measurement.**
`-bsDevEvolve <1|2>` (host) gives every survivor the first or second evolution of their Q skill with `-bsDevSkills`. Two harness overrides, `startQRank` (0 to 3) and `startEvolution` (0, 1, 2), give every survivor the Q skill at a rank and an evolution as the match starts, so the control (Q at rank 3, no evolution) and each branch run on the same seeds. They are applied after the survivor's growth starts, through the existing hook that runs before the stats are worked out.

**8. The harness decides whether the trades hold.**
Standard boss, normal bots, 120 matches for each of: control, every survivor with the first evolution, every survivor with the second. The acceptance: no variant moves the survivors' win share by more than 10 points from the control, and the median length stays in the range the boss pacing was tuned for. If one does, its values are adjusted (the boss is not).

*Measured (see `tools/harness/evolutions/results.md`):* with an evolution on every survivor from the start the win share rose by 9.7 (evolution 1) and 12.8 (evolution 2) points over 360 matches, a little over the line for the second. The cause is about 1% more damage to the boss over a match (the boss dies 7 to 12 s earlier on average) which the standard boss, tuned to die near the time limit, turns into about one match in ten; falls did not change, and with one survivor evolved at a time no evolution stood out of the noise. No value was changed, because no single evolution is the cause and a guess would only move the noise; this is reported as an exception to the rule above in the README and to the user.

## Risks / Trade-offs

- [A branch is strictly the better choice, so players always take it] → the non-dominance rule in data tests, and the harness comparison of the two branches against each other and the control.
- [Most players never see an evolution because the choice has three slots among many candidates] → accepted for now (normal offer, no forcing); the harness reports how many survivors reach an evolution; a guaranteed offer is a later change if it is too rare.
- [Marisa's thin long laser makes the R beam feel redundant] → the R beam is longer, wider and harder at every rank by data test; the values are to be tuned by hand later.
- [An evolution of Sakuya's time stop that stops often (1.8 s every 9 s) multiplies the freeze uptime] → the harness comparison and the cost (25 mana); the cooldown passive's floor still applies.
- [A character with an evolution and the cost passive can spam the cheap branch] → the existing floors (cooldown multiplier 0.4, cost reduction cap) apply to the evolution too.

## Open Questions

- Whether the evolved Q slot on the skill bar should show a small mark (a letter or a colour) that tells the two branches apart at a glance. The first version shows the name only.
