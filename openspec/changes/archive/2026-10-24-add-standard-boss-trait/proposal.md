# Proposal

## Why

The giant and the sniper each have a trait that asks the boss player to move in a certain way; the standard boss has none, so it is the only kind with no identity beyond its numbers. It is also the kind the survivors beat most easily (68% wins in the harness, next to the 70% ceiling). A trait that suits the standard boss as the all-round summoner gives it an identity and a little more pressure. A first version (summon energy refilling 40% faster) was measured and made the survivors win more (79% against 68%), because more mobs are more experience for them; the trait is therefore about the speed of the mobs, not their number.

## What Changes

- The standard boss gets the trait Command: the mobs it summons move 25% faster (devices stand still and are unaffected, and automatic mobs are not summoned ones).
- The trait is data like the others (`SummonedSpeedMultiple` on the kind) and is applied by the server only. It is turned off by the same override as the other traits, `bossTraits` 0.
- The trait's name and description are shown like the others: on the standard kind's button in the waiting room and on the boss player's screen.
- The standard kind is no longer a kind without a trait; the requirements that said so are reworded. The giant's and the sniper's traits are unchanged.
- The balance gate stays: 35-70% survivor wins and a median match of 480-840 s. The standard boss is measured with the trait on and off, and the other two kinds are checked to be unchanged.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `boss-kind-traits`: a trait may also be faster summoned mobs, the standard kind has Command, and the name and description and the boss player's screen cover it.
- `boss-kinds`: the kind choice in the waiting room shows the standard kind's trait too.

## Impact

- `BossDef` (`SummonedSpeedMultiple`), `BossCatalog` (standard values and texts), `BossSim` (the current multiple), `MobSim` (the factor in the chase), `MatchWorld` (hands it to the mob simulation), tests (the ones that said the standard kind has no trait), README, `tools/harness/standard-trait/`.
