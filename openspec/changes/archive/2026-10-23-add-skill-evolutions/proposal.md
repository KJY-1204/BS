# Proposal

## Why

Skill ranks only make a skill's numbers bigger. A survivor who takes the Q skill to its maximum rank has a stronger version of the same skill as everyone else of that character, and by level 20 nearly every skill is at its maximum, so builds converge. A choice at the top of a skill, where the skill itself changes, makes two survivors of the same character play differently and gives the end of a skill's growth a decision instead of a number. The R skills just added already made the skill bar richer; this is the other half of the growth that was deferred on purpose.

## What Changes

- Each of the four Q skills gets two evolutions, eight in all. An evolution is a skill of its own with one rank, its own mana cost, cooldown and effect values, and it replaces the Q skill on the Q key once learned. Each pair is a trade, not an upgrade: one is larger and heavier, the other smaller and lighter (for example Reimu's burst becomes a wide, weak one or a small, hard-hitting one; Marisa's beam becomes a wide short one or a thin long one).
- An evolution is offered in the normal three-way choice on a level up once its base skill has reached its maximum rank (the existing prerequisite rule). Learning one removes the other from every later choice: the choice is final.
- A new rule says that a skill can exclude another: a skill is not available while a skill that excludes it has been learned.
- The server fires the evolution instead of the base skill when the survivor has learned it, with the evolution's cost, cooldown, values and the character's passives; the skill event carries the evolution's id, so every client draws the evolved effect with no new message. The base skill keeps its rank, so the R skill's prerequisite (the Q skill at rank 2) still holds.
- The skill bar shows the evolution's name on the Q slot and its real cost and cooldown, and the level-up choices show an evolution by name and a short description of its trade.
- Development and measurement: `-bsDevEvolve` gives every survivor an evolution at the start, and two harness overrides (`startQRank`, `startEvolution`) start every survivor with the Q skill at a rank and an evolution, so the branches can be compared with the harness.

Not changed: the E and R skills (they get evolutions later by data), the passives, the boss, the number of choices on a level up, the skill id packing (the eight evolutions take ids 33 to 40 of the 42 it holds).

## Capabilities

### New Capabilities
- `skill-evolutions`: the evolutions of the Q skills: how one is offered, that the two exclude each other, that it replaces the Q skill, that each is a trade, and how it is shown and seen.

### Modified Capabilities
- `skill-tree-selection`: a skill is also not available when a skill that excludes it has been learned.
- `character-skills`: the one base skill per character on the Q slot may be replaced by its evolution.

## Impact

- Code: `SkillDef` (two fields: the skill it evolves from, the skill it excludes), `SkillCatalog` (eight definitions, ids 33 to 40, the exclusion in `IsAvailable`, a lookup of the skill a slot fires for a survivor's ranks), `SkillSim` (fire the evolved skill), `GrowthHud` (name and values of the evolved skill), `DevArgs`/`GrowthServer` (`-bsDevEvolve`), `HarnessSettings`/`MatchSession` (two overrides). The combat codec, the skill event and the client drawing do not change: they work from the skill id and its data.
- Network: no new message; an evolved skill is one skill event like any other.
- Tests: EditMode tests for the data (ids, trade, non-dominance), the exclusion and offers, firing and values of every evolution, the passives and the R prerequisite with an evolution, the display, the overrides; a multi-process run where each character's evolved skill fires and every client sees the same event; the harness comparison of no evolution, the first and the second.
- Not in scope: evolutions for E and R skills, new effect kinds or mechanics inside an evolution (they are value variants of the base effect), a guaranteed offer of the evolution, changing a chosen evolution, new art or sounds.
