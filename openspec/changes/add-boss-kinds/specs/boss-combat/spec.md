# Spec Delta

## MODIFIED Requirements

### Requirement: The boss is defined as data
The boss SHALL be defined in data by its kind, with a display name, a maximum health, a movement speed, a body radius, a contact damage value, a color, a basic attack and two skills, and the data SHALL be changeable without changing the rules that use it. The boss SHALL start a match with full health. The kinds that exist and how one is chosen are defined by the boss kinds.

#### Scenario: Starting values
- **WHEN** a match starts with a boss player
- **THEN** the boss has full health of its definition, moves at its definition's speed and is drawn in its definition's color

#### Scenario: Changing a value
- **WHEN** the boss's maximum health is changed in the data
- **THEN** the boss starts the next match with the new maximum and no rule needs to change

#### Scenario: Kind decides the definition
- **WHEN** a match starts with a boss of a given kind
- **THEN** every value above is the value of that kind's definition
