# Spec Delta

## ADDED Requirements

### Requirement: Joiners enter the waiting room first
A player who connects to a room SHALL be given a role as before, but SHALL enter the room's waiting state and appear in the waiting room; the player SHALL NOT receive a character until the match starts.

#### Scenario: Join a waiting room
- **WHEN** a player joins a waiting room by code
- **THEN** the player is shown in the waiting room with a role and no character in the world

### Requirement: A playing room refuses new players
A room whose match is running SHALL refuse a new connection with a reason, and the refusal SHALL NOT change anything for players already in the room.

#### Scenario: Joining a running match
- **WHEN** a player enters a valid code of a room that is playing
- **THEN** the join fails with a message that the match is already running, and the room is unchanged

#### Scenario: Capacity still applies
- **WHEN** a waiting room already has five players and another player tries to join
- **THEN** the join is rejected as full, as before
