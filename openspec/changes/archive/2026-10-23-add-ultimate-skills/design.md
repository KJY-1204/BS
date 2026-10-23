# Design

## Context

Skills are data (`SkillDef` in `SkillCatalog`): an id, a key slot (0 Q, 1 E for the active ones), the owning character, an effect kind, per-rank values, a mana cost, a cooldown, and an optional prerequisite (`RequiresSkill` at `RequiresRank`). The server side is already slot-generic: `SkillSim.TryActivate(client, character, slot, ...)` looks the skill up with `SkillCatalog.ForSlot(slot, character)`, checks the survivor, the rank, the cooldown and the mana, applies the character's passives to cost, cooldown, power, range and duration for any active skill of that character, and `SkillExecutor` runs one of eight effect kinds. A fired skill is one small skill event (skill id, rank, position, aim, range and duration scales) and the clients draw it from the skill's data. The level-up offer takes every skill that is available to the character (owned, prerequisite met, below its maximum rank) and picks up to three at random.

What is fixed at two slots today: `SkillCatalog.SkillSlots` (the cooldown arrays in `SkillSim` and `PlayerGrowth`, the loop that draws the skill bar, the development grant of skills, the bots' loop over slots), the key reading in `PlayerGrowth` (Q and E only), and the label in `GrowthHud` (`slot == 0 ? "Q" : "E"`). Skill ids 1 to 28 are in use and the packed ranks sent to the owner hold 42, so four more skills need no change to the packing. The boss's skill warnings use ids 200 and up, so they cannot collide. The boss player's R key (devices) belongs to another role and another component; `PlayerGrowth` ignores the boss.

See proposal.md for why. The earlier exploration chose: R slot first, branching later; the R skill is earned through the normal choice with a prerequisite; no new effect kinds at first.

## Goals / Non-Goals

**Goals:**
- A third slot that follows every rule of the first two (server decides, same events, same passives, same display), added by data and by raising the slot count, not by a parallel mechanism.
- Four R skills that feel like peaks of their characters while reusing the existing effect kinds.
- A measurable effect on the match: the pacing the boss was tuned to stays inside its range, checked with the harness before and after.

**Non-Goals:**
- New effect kinds (reviving a fallen survivor, a screen-wide stop), new art or sounds.
- Branching or evolving skills at their maximum rank, and R passives; both are for the next change.
- Retuning the boss to make room for the R skills; the R numbers move first.

## Decisions

**1. The slot count becomes three and everything that loops over slots follows it.**
`SkillCatalog.SkillSlots = 3`. The cooldown arrays, the skill bar, the development grant and the bots already loop to `SkillSlots`, so they pick up the new slot without a rule of their own. The spots that name keys are changed by hand: `PlayerGrowth.ReadInput` (R key, one more `RequestSkill(2)`), `GrowthHud` (a key label from a small array, the bar centred for three slots), `DevArgs` (`-bsAutoSkill r` and `qer`; `qe` keeps meaning Q and E).
Alternative: a separate "ultimate" mechanism with its own state and RPC. Rejected: it would repeat the cooldown, mana, result and event code for one slot.

**2. R skills are four ordinary `SkillDef` entries with `Slot = 2` and ids 29 to 32.**
They are active, owned by their character, three ranks, and use an existing effect kind: Reimu `AreaDamage`, Marisa `Beam`, Sakuya `Volley`, Sanae `Heal`. The client already draws a skill by its effect kind and per-rank sizes, so a larger radius, beam or ring is drawn without a client change. Starting values (to be tuned by the harness, see decision 5):

| Character | Skill | Mana | Cooldown | Rank 1 / 2 / 3 |
|---|---|---|---|---|
| Reimu | area burst | 70 | 40 s | radius 5 / 6 / 7, damage 80 / 110 / 140 |
| Marisa | beam | 80 | 45 s | length 16, half width 1.2 / 1.4 / 1.6, damage 120 / 160 / 200 |
| Sakuya | ring of knives | 70 | 40 s | 36 / 48 / 60 knives, damage 12 / 14 / 16 each |
| Sanae | heal | 70 | 45 s | radius 8, heal 80 / 110 / 140 |

Each is above its character's Q and E in cost, cooldown and reach, as the spec requires. The passives of the character apply with no code, because `SkillSim` applies them to every active skill of the character.
Alternative: a new "revive" effect for Sanae's R. Rejected for this change: it needs a rule against the boss's wipe condition and a new revival source; it is listed as a later candidate.

**3. An R skill is earned through the existing prerequisite.**
`RequiresSkill` is the character's own Q skill id and `RequiresRank` is 2. `SkillCatalog.IsAvailable` already honours that, so the offer, the random pick and the maximum rank need no change. The level at which a survivor first sees it depends on how soon the player takes Q twice, which is the intended pull: a build that invests in its Q gets its R sooner. No automatic grant.
Alternative: grant it at a fixed level. Rejected in the exploration: it needs a new rule in the level-up flow and removes the choice.

**4. Cooldown and mana are the existing per-slot state; nothing is shared between slots.**
`SkillSim` keeps one cooldown per slot, so the R cooldown does not touch Q and E and the other way round. Mana is shared as before, so a 70 mana R competes with Q and E for the same pool; at 5 mana per second the pool refills a 70 cost in about 14 seconds, which suits a 40 second cooldown.

**5. The harness decides whether the numbers hold.**
The bots press every slot every 0.5 seconds when anything is near, and pick offers at random, so they will use R skills with no new bot rule. Before the change the pacing baseline (`tools/harness/pacing`) is recorded again as the reference for the current build; after it the same configurations are run. The acceptance is what `tune-boss-pacing` established for normal bots: the survivors win a share that stays inside the earlier range (about 55 to 65 percent) and the median match stays above 700 seconds. If the R skills push the survivors above it, the R damage, mana cost or cooldown is adjusted first (the boss health is the dial of last resort). The result and the final numbers go in the README.

**6. Traffic is checked, not assumed.**
The biggest R burst is Sakuya's ring: up to 60 projectile fire events at once, once per 40 seconds, plus their hits. The existing worst-case traffic test (`TheWorstCaseTrafficOfAllFourCharactersStaysFarUnderTheBudget`) is extended with the R skills of the four characters at their cooldowns and asserts the same budget. The projectile cap (256) stays: a ring that does not fit fires the knives that fit, as the ring code already does.

## Risks / Trade-offs

- [Four heavy skills lift the survivors' win share and shorten matches] → the harness comparison in decision 5, with the R numbers as the first thing to turn; the boss pacing spec is untouched.
- [An R skill dilutes the level-up choices, so a player sees the passives less] → the offer already has three slots and five to nine candidates; the R skill is one candidate among them, and only after Q rank 2.
- [Passives that help skills (power, cost, cooldown) stack on the R skill and make it too strong] → the passives are bounded (cooldown multiplier floor 0.4, cost reduction cap 0.9), and the harness runs the passives through the bots' random picks; a note in the README lists the combinations worth hand testing later.
- [Sanae's wide heal makes the boss's wipe condition hard to reach] → it heals and does not revive; the survivors' own win share in the harness is the check.
- [Three keys on a keyboard plus aiming is more to press] → R is next to E; the key is only used after the survivor has chosen to learn it.

## Open Questions

- Whether the skill bar should show the R slot before it is learnable (greyed, with the Q rank it needs). The first version shows "not learned" like the other slots.
