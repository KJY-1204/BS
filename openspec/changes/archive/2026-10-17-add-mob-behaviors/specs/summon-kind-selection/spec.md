# Spec Delta

## MODIFIED Requirements

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
