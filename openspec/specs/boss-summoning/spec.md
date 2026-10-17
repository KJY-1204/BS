# boss-summoning Specification

## Purpose

Defines how the boss player calls mobs onto the field: the summon command, the energy it costs and how that energy refills, where summoned mobs appear, when a summon is refused, and what the boss player and the survivors see of it.

## Requirements

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

### Requirement: Summoned mobs appear near the boss, not on top of survivors
Summoned mobs SHALL appear on a ring around the boss and inside the arena. A position closer than the safe distance to a living survivor SHALL NOT be used; the server SHALL try other positions and create fewer mobs if no position is allowed.

#### Scenario: Around the boss
- **WHEN** a group is summoned
- **THEN** each mob appears within the ring distance of the boss and inside the arena

#### Scenario: A survivor stands in the ring
- **WHEN** a survivor stands where a summoned mob would appear
- **THEN** that mob is placed elsewhere on the ring, or not created if no allowed position is found, and is never placed within the safe distance of the survivor

### Requirement: A summon is refused when it cannot happen
The server SHALL refuse a summon, take no energy and create no mob, when the room is not playing, when the boss has fallen, when the mob count is at the hard cap, or when the cooldown has not passed. A group that only partly fits under the hard cap SHALL create the mobs that fit and charge only for those.

#### Scenario: Match over
- **WHEN** the boss gives the summon command after the match has ended
- **THEN** nothing is created

#### Scenario: Boss fallen
- **WHEN** the boss is at zero health and the command is given
- **THEN** nothing is created

#### Scenario: Field full
- **WHEN** the mob count equals the hard cap and the command is given
- **THEN** nothing is created and no energy is taken

#### Scenario: Cooldown
- **WHEN** a second summon is requested before the cooldown has passed
- **THEN** it is refused and no energy is taken

### Requirement: Summoned mobs look different from automatic ones
A summoned mob of the basic kind SHALL be drawn in a color different from an automatically spawned mob on every client, so everyone can tell the origins apart, and mobs of the other kinds SHALL be drawn in the colors of their kinds. This SHALL NOT need a new kind of network message.

#### Scenario: Seeing the difference
- **WHEN** a summoned basic mob and an automatically spawned mob are on the field
- **THEN** every client draws them in different colors

#### Scenario: Other kinds
- **WHEN** a summoned fast mob is on the field
- **THEN** every client draws it in the color of the fast kind

### Requirement: The boss sees its energy, others do not
The boss player SHALL see the current and maximum summon energy and a hint for the summon command. The server SHALL send the energy only to the boss player, and survivors SHALL NOT receive or show it. The boss SHALL get a short visible sign when a summon is refused.

#### Scenario: Boss screen
- **WHEN** a match is running and the boss player looks at the screen
- **THEN** an energy bar with the current and maximum values and the command hint are shown

#### Scenario: Survivor screen
- **WHEN** a survivor looks at the screen
- **THEN** no energy bar is shown and no energy value reached the survivor's client

#### Scenario: Refused summon
- **WHEN** the boss summons without enough energy
- **THEN** the energy bar flashes or a short notice is shown

### Requirement: Summoning stops with the match
The energy SHALL not refill and summons SHALL be refused while the room is not playing. When the room returns to waiting, the summon state SHALL be cleared with the rest of the match.

#### Scenario: Result screen
- **WHEN** the match has ended and the result is shown
- **THEN** the energy no longer changes and a summon command does nothing

#### Scenario: Next match
- **WHEN** the next match starts
- **THEN** the summon state is fresh and no earlier command applies
