# Spec Delta

## REMOVED Requirements

### Requirement: A room is either waiting or playing
**Reason**: A match that has ended now leaves the room in a third state, finished, until the host returns it to waiting.
**Migration**: The three states are stated in the requirement below.

## MODIFIED Requirements

### Requirement: Joining is closed while playing
A room whose match is playing or finished SHALL refuse new players with a clear reason, and the players already in the room SHALL NOT be affected. A room that is waiting SHALL accept players as long as it has room.

#### Scenario: Join during play
- **WHEN** a new player tries to join a playing room
- **THEN** the join is rejected with a reason shown to that player, and the playing room is unchanged

#### Scenario: Join while waiting
- **WHEN** a new player joins a waiting room that has a free slot
- **THEN** the player enters the waiting room

#### Scenario: Join after a result
- **WHEN** a new player tries to join a room whose match has finished and shows its result
- **THEN** the join is rejected with a reason shown to that player, and the room is unchanged

### Requirement: The host can return the room to waiting
The host SHALL be able to end the match from a playing room or a finished room, and the server SHALL then remove every character, mob, projectile and skill effect and reset all growth and the result, and put the room back to waiting with the same connected players. Every character chosen before SHALL stay chosen and SHALL be changeable. Only the host SHALL be able to end the match.

#### Scenario: Host ends the match
- **WHEN** the host ends a playing match
- **THEN** every client sees no characters, mobs, projectiles or skill effects, shows the waiting room with the same players, and every survivor still has the chosen character

#### Scenario: A second match starts fresh
- **WHEN** the host starts again after returning
- **THEN** levels, experience, learned skills, health, mana and mob count start from the beginning, exactly as in the first match

#### Scenario: A client tries to end the match
- **WHEN** a client other than the host sends an end request
- **THEN** the match continues

#### Scenario: Returning after a result
- **WHEN** the host ends a finished match
- **THEN** every client sees the waiting room with the same players and no result

## ADDED Requirements

### Requirement: A room is waiting, playing or finished
The server SHALL keep every room in exactly one of three states, waiting (lobby), playing or finished, and SHALL tell every client the current state. A newly created room SHALL be waiting. Characters, mobs, projectiles and growth SHALL exist only while the room is playing or finished, and nothing of a match SHALL advance while the room is finished. A room SHALL become finished only when the match ends with a result, and SHALL leave the finished state only by returning to waiting.

#### Scenario: New room
- **WHEN** a host opens a room
- **THEN** the room is waiting, and no character or mob exists

#### Scenario: Everyone sees the state
- **WHEN** the room changes state
- **THEN** every connected client shows the new state

#### Scenario: A match with a result
- **WHEN** a match ends because the boss fell
- **THEN** the room is finished, its characters are still on every screen and the result is shown

#### Scenario: Starting a finished room
- **WHEN** anyone asks to start a match in a finished room
- **THEN** the request is ignored and the room stays finished
