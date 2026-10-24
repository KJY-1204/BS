# Spec Delta

## ADDED Requirements

### Requirement: The standard boss has Command
The mobs the standard boss summons SHALL move 25% faster than a mob of their kind normally does. Automatic mobs, devices (which stand still) and the mobs of the giant and the sniper SHALL be unaffected, and the summon energy and its cost SHALL be unchanged.

#### Scenario: Summoned mobs
- **WHEN** the standard boss summoned mob and an automatic mob of the same kind chase a survivor
- **THEN** the summoned mob moves 25% faster

#### Scenario: Other kinds
- **WHEN** the giant or the sniper summons a mob
- **THEN** it moves at its normal speed

#### Scenario: Switched off
- **WHEN** a match runs with `bossTraits` 0
- **THEN** the summoned mobs of the standard boss move at their normal speed

## MODIFIED Requirements

### Requirement: A kind may have a trait as data
A boss kind SHALL be able to carry a trait in its data: a still time after which the boss's basic shots deal more damage and the multiple they deal, the multiples of the boss's contact damage while it moves and while it stands still, and a multiple of the speed of the mobs it summons. A kind with no trait value SHALL scale nothing for it (multiples of 1 and a still time of 0).

#### Scenario: Standard boss
- **WHEN** the standard boss shoots and touches survivors
- **THEN** its shots and contacts deal the damage they dealt before traits existed, however it moves

#### Scenario: Complete data
- **WHEN** every boss kind is examined
- **THEN** each trait value is positive and a kind with a still time for its shots also has a shot multiple above one

### Requirement: A trait is named and described as data
Every boss kind SHALL carry a short name and a one-line description of its trait in its data. The description SHALL state the numbers the rule uses.

#### Scenario: Kinds with a trait
- **WHEN** every boss kind is examined
- **THEN** each has a trait name and a description that mentions its numbers

### Requirement: The boss player sees the trait of the kind being played
During a match with a boss, the boss player's screen SHALL show the name and description of the playing kind's trait next to its skills. Survivors' screens SHALL not show it.

#### Scenario: Playing the sniper
- **WHEN** the boss player plays the sniper
- **THEN** the screen shows the name and description of Steady near the skill cells

#### Scenario: Standard boss
- **WHEN** the boss player plays the standard kind
- **THEN** the screen shows the name and description of Command near the skill cells
