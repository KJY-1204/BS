# Design

## Context

`LevelingSim.OpenNextOffer` collects every available skill with `SkillCatalog.IsAvailable`, shuffles the first `count` positions with the sim's random source and offers them. An evolution is a skill with `EvolvesFrom` set that becomes available when its base skill is at the maximum rank and nothing excludes it; the survivor's character is already respected by `IsAvailable`.

## Goals / Non-Goals

**Goals:** an available evolution always reaches the offer; same offer size; the random source stays the only source of choice.
**Non-Goals:** guaranteeing any other kind of skill, changing which skills are available, favouring one evolution of a pair.

## Decisions

1. After the shuffle that fills `Choices`, if the guarantee is on and none of the first `count` choices is an evolution, the sim picks one evolution from `available` (positions `count` and later, since the first `count` hold no evolution) with `rng.Next`, and writes it over the last choice. Draws happen only when needed, so an offer that already holds an evolution, and every offer without an available evolution, consume the random source exactly as before.
2. `LevelingSim.GuaranteeEvolutionOffer` (default true) is the switch; the harness override `evolutionOfferGuarantee` sets it on the world.
3. The replaced choice is the last of the shuffled ones, which is as random as any other.
4. Acceptance: 120 natural-play matches per boss kind with the guarantee on, compared with the previous measurement (the guarantee did not exist then, so the earlier runs are the off baseline: standard 59.2%, giant 45.8%, sniper 53.3%), keeping 35-70% survivor wins and the median match in 480-840 s.

## Risks / Trade-offs

- A survivor may be offered an evolution before a skill it wants more; the choice stays free, and an unwanted evolution can simply be passed over.
- More evolutions taken may raise the survivors' win share; if a kind passes 70%, the evolution values (not the guarantee) are looked at.
