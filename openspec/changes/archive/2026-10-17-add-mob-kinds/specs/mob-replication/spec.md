# Spec Delta

## MODIFIED Requirements

### Requirement: Mob appearance and removal are reliable
The server SHALL tell clients reliably when a mob appears and when it is removed, so a mob is never left on screen after it is gone and never missing after it appears. The appearance message SHALL carry the mob's kind and origin, so every client draws the mob as its kind from the first frame.

#### Scenario: Mob spawns
- **WHEN** the server spawns a mob
- **THEN** every client shows a new mob at its spawn position, drawn as the mob's kind

#### Scenario: Mob removed
- **WHEN** the server removes a mob
- **THEN** every client stops displaying that mob

#### Scenario: Stale mob
- **WHEN** a client has not received any position for a displayed mob for 2 seconds
- **THEN** the client removes that mob from the display

### Requirement: Late joiners see current mobs
A player who joins a room in progress SHALL see all existing mobs at their current positions, each drawn as its own kind, without waiting for new spawns.

#### Scenario: Join mid-game
- **WHEN** a player joins while mobs are alive
- **THEN** within 1 second the player's screen shows all current mobs at their current positions

#### Scenario: Kinds for a late joiner
- **WHEN** a player joins while mobs of several kinds are alive
- **THEN** each mob appears with the color and size of its own kind
