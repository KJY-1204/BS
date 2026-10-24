# Proposal

## Why

The Q skills can evolve at their maximum rank, and survivors of one character already play differently because of it. The E and R skills still only get bigger numbers. A survivor's E skill, the barrier or the time-limited speed boost or the ring of knives or the push, ends the match as it began, and the R skill, the peak of the whole growth, has no decision at its top at all. Giving the E and R skills the same final choice makes all three keys a place where a build can go its own way, and it finishes what the first evolution change left on purpose for later. The limit that left it for later was the rank packing: the ranks of every skill are sent to the owner in two 64-bit values that hold skill numbers up to 42, and eight more skills fit while sixteen do not.

## What Changes

- Each E skill and each R skill of the four characters gets two evolutions, sixteen in all, with the same rules as the Q evolutions: offered in the normal choice once the base skill has reached its maximum rank, the two exclude each other and the choice is final, the evolution replaces the base skill on its key, it is a trade (worse than the base in at least one of its cost, cooldown or values), the character's passives apply, and everyone sees its effect.
- The R evolutions stay ultimates: each costs more mana and cools down longer than every base Q and E skill of its character.
- The rank packing grows to three 64-bit values and skill numbers up to 63: the packing and unpacking work on a list of values instead of two named ones, and the owner receives one more replicated value. The eight Q evolutions and everything else keep their numbers; the new evolutions take 41 to 56.
- The skill bar and the choice text already show the skill a slot fires and the choice's name; the longer names are checked in a captured window.
- Development and measurement: `-bsDevEvolve` gives every survivor the chosen evolution of the Q, E and R skills, and the harness overrides learn `startERank`, `startRRank` and `startEvolutionSlot` (which slot `startEvolution` applies to), so each slot's evolutions can be compared with the harness on its own.

Not changed: the Q evolutions' values (a decision made after the first change's measurements: the new evolutions are fitted to the gate and the old ones left alone), the passives, the boss, the number of choices on a level up, how a skill is offered and fired.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `skill-evolutions`: the E and R skills have evolutions too, and an R evolution is still an ultimate.
- `skill-tree-selection`: the packed ranks hold at least sixty-three skills.
- `ultimate-skills`: the R skills are compared with the Q and E skills by their base skills, so an evolution of the R skill or of the Q and E skills does not change the rule.
- `character-skills`: a learned evolution of any base skill is fired in place of it.

## Impact

- Code: `SkillCatalog` (sixteen definitions, ids 41 to 56, the packing for any number of values, `MaxSkillId`), `PlayerGrowth` (a third replicated rank value), `GrowthServer` (writes it, the development grant), `DevArgs`, `HarnessSettings` and `MatchWorldGrowth` (the new overrides). `ActiveForSlot`, the exclusion rule, the server's firing, the skill event, the client drawing and the skill bar already work for any slot and do not change.
- Network: one more owner-only 64-bit value, written only when a rank changes; no new message.
- Tests: EditMode tests for the packing (the whole range, the edges between the three values), the data of the sixteen evolutions (ids, trade, non-dominance, the R rule), the offers and the exclusion for E and R, the effect and values of every evolution, the traffic of the biggest ring (90 knives), the development grant and the overrides; a multi-process run where each character's E and R evolutions fire and the owner's client receives the ranks of ids above 42; the pacing harness run slot by slot.
- Not in scope: evolutions with more than one rank, new effect kinds, evolutions for the passives, changing the Q evolutions, a guaranteed offer, new art or sounds.
