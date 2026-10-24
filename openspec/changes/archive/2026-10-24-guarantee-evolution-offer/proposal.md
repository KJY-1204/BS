# Proposal

## Why

The Q, E and R evolutions are a late choice, but a survivor can reach the top of a skill and never be offered its evolution: an offer holds three skills drawn at random from everything that can be learned, and near the end of the tree many other skills compete. In natural play only 47, 32 and 4 of 160 survivors ended a match with a Q, E or R evolution (the R evolutions are the rarest). The evolution is the payoff of a build, so it should show up when it can.

## What Changes

- When a level-up offer is built and at least one evolution is available (its base skill is at the maximum rank and no evolution of that skill is taken), the offer SHALL contain at least one evolution. If the three random choices hold none, one of them is replaced by an evolution picked at random among the available ones.
- Nothing else about the offer changes: the number of choices, the shuffle, the rules for which skills are available, and the final choice of an evolution.
- The harness gets an override `evolutionOfferGuarantee` (1 is the default, 0 turns the guarantee off) so the effect can be measured against the earlier behaviour.
- The balance gate is kept: 35-70% survivor wins and a median match of 480-840 s for the standard, giant and sniper boss.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `skill-evolutions`: an available evolution is always among the offered choices, with a switch for the harness.

## Impact

- `LevelingSim` (`GuaranteeEvolutionOffer` and the offer building), `HarnessSettings` (`evolutionOfferGuarantee`), tests, README (the limit "offers are not guaranteed" is lifted), `tools/harness/evolution-offer/`.
