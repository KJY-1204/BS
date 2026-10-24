# Spec Delta

## ADDED Requirements

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
