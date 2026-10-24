# Proposal

## Why

The boss traits (the giant's Momentum, the sniper's Steady) change how the boss should be played, but nothing on screen says they exist. A player cannot choose a kind for its trait, or play to it, without reading the README.

## What Changes

- Each boss kind carries a trait name and a one-line description as data (empty for the standard kind).
- In the waiting room the boss player sees the trait of each kind beside its skills.
- During the match the boss player sees the trait of the kind being played on a line above the skill cells.
- Display only: no rule, value or wire format changes. Everyone already knows the kind, so the text comes from the catalog.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `boss-kind-traits`: a kind's trait has a name and a description that the interface shows.
- `boss-kinds`: the kind choice in the waiting room lists the trait.

## Impact

- `BossDef` (`TraitName`, `TraitText`), `BossCatalog` (texts), `NetworkMenu` (label), `SummonHud` (trait line and a pure text helper), tests, README. No scene or prefab change.
