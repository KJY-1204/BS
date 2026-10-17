# Spec Delta

## MODIFIED Requirements

### Requirement: The boss can summon a group of mobs
While a match is running and the boss is standing, the boss player SHALL be able to summon a group of mobs of the chosen kind with one command, and the server SHALL create the group around the boss's position. A summoned mob SHALL have the stats of its kind and the summoned origin. Only the boss player SHALL be able to summon, and a summon request from anyone else SHALL be ignored.

#### Scenario: A summon
- **WHEN** the boss player gives the summon command with enough energy and room for mobs
- **THEN** the server creates a group of the chosen kind around the boss and every client sees them appear like any other mob

#### Scenario: Not the boss
- **WHEN** a survivor's client sends a summon request
- **THEN** the server ignores it and no mob is created

#### Scenario: Holding the command
- **WHEN** the boss player keeps the summon command pressed
- **THEN** summons repeat no faster than the summon cooldown allows

### Requirement: Summoning costs energy that refills over time
The boss SHALL have a summon energy with a maximum. Energy SHALL refill at a steady rate while the match is running and SHALL NOT exceed the maximum. Each mob a summon actually creates SHALL cost the energy cost of its kind, and a summon SHALL create only as many mobs as the energy pays for, up to the group size of its kind. The boss SHALL start a match with a part of the maximum energy. All of these values SHALL be adjustable without changing rules elsewhere.

#### Scenario: Paying for a group
- **WHEN** the boss has energy for a whole group and summons
- **THEN** the group is created and the cost of the created mobs is taken from the energy

#### Scenario: Energy for part of a group
- **WHEN** the boss has energy for fewer mobs than the group size and summons
- **THEN** only that many mobs are created and the energy drops by their cost

#### Scenario: No energy
- **WHEN** the boss has less energy than one mob of the chosen kind costs and summons
- **THEN** nothing is created, no energy is taken, and the boss is told the summon failed

#### Scenario: Refilling
- **WHEN** time passes in a running match
- **THEN** the energy rises at the refill rate until it reaches the maximum and stays there

#### Scenario: A new match
- **WHEN** a match starts after an earlier one ended
- **THEN** the energy starts at its starting value, not at what the earlier match left

### Requirement: Summoned mobs look different from automatic ones
A summoned mob of the basic kind SHALL be drawn in a color different from an automatically spawned mob on every client, so everyone can tell the origins apart, and mobs of the other kinds SHALL be drawn in the colors of their kinds. This SHALL NOT need a new kind of network message.

#### Scenario: Seeing the difference
- **WHEN** a summoned basic mob and an automatically spawned mob are on the field
- **THEN** every client draws them in different colors

#### Scenario: Other kinds
- **WHEN** a summoned fast mob is on the field
- **THEN** every client draws it in the color of the fast kind
