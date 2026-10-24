# boss-kind-traits Specification

## Purpose
Defines the trait each boss kind can have: server-side rules that scale the boss's damage by how the boss player moves, so that the kinds ask for different ways of playing.

## Requirements

### Requirement: The server knows whether the boss is standing still
The server SHALL keep, for the boss of a match, whether it moved during the last step and how many seconds it has stood still since it last moved. A change of position smaller than a small fixed distance in one step SHALL count as standing still. A match without a boss, and a boss that has fallen, SHALL have no stillness to report.

#### Scenario: Moving
- **WHEN** the boss player moves the boss during a step
- **THEN** the boss counts as moving and its still time is zero

#### Scenario: Standing
- **WHEN** the boss does not move for 1.5 seconds
- **THEN** its still time is 1.5 seconds and it does not count as moving

#### Scenario: Moving again
- **WHEN** the boss that stood still moves
- **THEN** its still time starts again from zero

### Requirement: A kind may have a trait as data
A boss kind SHALL be able to carry a trait in its data: a still time after which the boss's basic shots deal more damage and the multiple they deal, and the multiples of the boss's contact damage while it moves and while it stands still. A kind with no trait SHALL scale nothing. The standard kind SHALL have no trait.

#### Scenario: Standard boss
- **WHEN** the standard boss shoots and touches survivors
- **THEN** its shots and contacts deal the damage they dealt before traits existed, however it moves

#### Scenario: Complete data
- **WHEN** every boss kind is examined
- **THEN** each trait value is positive and a kind with a still time for its shots also has a shot multiple above one

### Requirement: The sniper is Steady
Once the sniper has stood still for one second, each basic shot that leaves it SHALL deal 60% more damage than it would otherwise, rounded to a whole number. The shot SHALL keep the damage it left with. The bonus SHALL end the moment the sniper moves.

#### Scenario: Aiming
- **WHEN** the sniper has stood still for one second and fires
- **THEN** the shot deals 60% more than the same shot would deal while moving, on top of the enrage of its remaining health

#### Scenario: Not yet steady
- **WHEN** the sniper has stood still for half a second and fires
- **THEN** the shot deals its normal damage

#### Scenario: Moving
- **WHEN** the steady sniper moves and fires
- **THEN** the shot deals its normal damage

#### Scenario: A shot in the air
- **WHEN** a steady shot is in the air and the sniper moves
- **THEN** the shot still deals the damage it left with

### Requirement: The giant has Momentum
The damage the giant deals to a survivor by touching it SHALL be 30% higher while the giant moved during the step and 30% lower while it stands still, rounded to a whole number and never below one. Everything else about contact damage (the reach, the protection time after a hit, the boss going first) SHALL stay as it is.

#### Scenario: Charging
- **WHEN** the moving giant touches a survivor
- **THEN** the survivor loses 30% more than the giant's contact damage

#### Scenario: Standing
- **WHEN** a survivor touches the giant that stands still
- **THEN** the survivor loses 30% less than the giant's contact damage

#### Scenario: Other bosses
- **WHEN** the standard boss or the sniper touches a survivor
- **THEN** the survivor loses exactly the boss's contact damage whether or not it moves

### Requirement: The traits can be turned off
The harness SHALL accept an override `bossTraits`: 1 (the default) applies the traits, 0 applies none, so that every kind deals the damage it dealt before traits existed. Any other value SHALL be refused with a reason.

#### Scenario: Off
- **WHEN** a match runs with `bossTraits` 0
- **THEN** the steady sniper's shots and the moving giant's contacts deal the unscaled damage

#### Scenario: Bad value
- **WHEN** `bossTraits` is 2
- **THEN** the settings are refused and the reason names the override

### Requirement: A trait is named and described as data
A boss kind that has a trait SHALL carry a short name and a one-line description of it in its data, and a kind with no trait SHALL carry neither. The description SHALL state the numbers the rule uses.

#### Scenario: Kinds with a trait
- **WHEN** every boss kind is examined
- **THEN** the giant and the sniper each have a name and a description that mentions their multiples, and the standard kind has neither

### Requirement: The boss player sees the trait of the kind being played
During a match with a boss, the boss player's screen SHALL show the name and description of the playing kind's trait next to its skills, and SHALL show nothing for a kind with no trait. Survivors' screens SHALL not show it.

#### Scenario: Playing the sniper
- **WHEN** the boss player plays the sniper
- **THEN** the screen shows the name and description of Steady near the skill cells

#### Scenario: Standard boss
- **WHEN** the boss player plays the standard kind
- **THEN** no trait line is shown
