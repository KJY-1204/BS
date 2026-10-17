# summon-kind-selection Specification

## Purpose

Defines how the boss player chooses which kind of mob to summon: the selection input, the server's check of the chosen kind, the cost and group size that depend on the kind, and what the boss sees of the choices.

## Requirements

### Requirement: The boss chooses the kind to summon
The boss player SHALL be able to choose one of the mob kinds with the keys 1 to 5 (basic, fast, tough, ranged, exploding), and the summon command SHALL summon a group of the chosen kind. The choice SHALL stay until changed and SHALL start each match as the basic kind, so a boss who never chooses summons as before. The choice SHALL travel to the server with each summon request and SHALL NOT be kept by the server between requests.

#### Scenario: Choosing
- **WHEN** the boss player presses the key of the tough kind and then summons
- **THEN** the group that appears is of the tough kind

#### Scenario: A new kind
- **WHEN** the boss player presses the key of the exploding kind and then summons
- **THEN** the group that appears is of the exploding kind

#### Scenario: The choice stays
- **WHEN** the boss summons twice after choosing the fast kind once
- **THEN** both groups are of the fast kind

#### Scenario: No choice made
- **WHEN** the boss summons without pressing any kind key
- **THEN** the group is of the basic kind

#### Scenario: A new match
- **WHEN** a new match starts after one in which the boss chose another kind
- **THEN** the choice starts as the basic kind

### Requirement: The server checks the chosen kind
The server SHALL accept a summon only for a known kind. A summon naming an unknown kind SHALL be refused with no mob created and no energy taken, and the boss SHALL be told. A request from anyone other than the boss SHALL be ignored whatever kind it names.

#### Scenario: Unknown kind
- **WHEN** a summon request names a kind that does not exist
- **THEN** nothing is created, no energy is taken, and the boss is told the summon failed

#### Scenario: Not the boss
- **WHEN** a survivor's client sends a summon request for the tough kind
- **THEN** the server ignores it and no mob is created

### Requirement: Cost and group size follow the kind
Each mob a summon creates SHALL cost the chosen kind's energy cost, and a summon SHALL try to create the chosen kind's group size. A summon SHALL create only as many mobs as the energy pays for, the field has room for and positions allow, up to that group size, and SHALL take energy only for the mobs it created. The cooldown and every refusal rule SHALL be the same for every kind. The values SHALL be adjustable without changing rules elsewhere.

#### Scenario: A costlier kind
- **WHEN** the boss with the same energy summons the tough kind instead of the basic one
- **THEN** it pays more per mob, so fewer mobs are affordable

#### Scenario: Energy for part of a group
- **WHEN** the boss has energy for one tough mob but the tough group is two
- **THEN** one tough mob is created and the energy drops by its cost

#### Scenario: No energy
- **WHEN** the boss has less energy than one mob of the chosen kind costs
- **THEN** nothing is created, no energy is taken, and the boss is told the summon failed

#### Scenario: Same rules for every kind
- **WHEN** the boss summons any kind during the cooldown, with the field full, after the match ended or after the boss fell
- **THEN** the summon is refused as for the basic kind, with no energy taken

### Requirement: The boss sees the kinds and its choice
The boss player's screen SHALL list the kinds with their keys, names and costs and SHALL mark the chosen one. Survivors SHALL NOT see this list, and no kind choice SHALL be sent to survivors.

#### Scenario: Boss screen
- **WHEN** a match is running and the boss player looks at the screen
- **THEN** the five kinds are listed with key, name and cost, and the chosen kind is marked

#### Scenario: Changing the choice
- **WHEN** the boss player presses another kind's key
- **THEN** the mark moves to that kind at once

#### Scenario: Survivor screen
- **WHEN** a survivor looks at the screen
- **THEN** no kind list is shown
