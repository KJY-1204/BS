# room-lobby Specification

## Purpose

Defines the two states of a room, waiting and playing: what is shown while players wait, when the host may start, what happens at the start, and how a running room returns to waiting.

## Requirements

### Requirement: A room is either waiting or playing
The server SHALL keep every room in exactly one of two states, waiting (lobby) or playing, and SHALL tell every client the current state. A newly created room SHALL be waiting. Characters, mobs, projectiles and growth SHALL exist only while the room is playing.

#### Scenario: New room
- **WHEN** a host opens a room
- **THEN** the room is waiting, and no character or mob exists

#### Scenario: Everyone sees the state
- **WHEN** the room changes state
- **THEN** every connected client shows the new state

### Requirement: The waiting room shows who is here
While the room is waiting, every player SHALL see each connected player, that player's role (boss or survivor slot), and, for survivors, the character that player has chosen or that none is chosen yet.

#### Scenario: A player joins the waiting room
- **WHEN** a player joins a waiting room
- **THEN** every client shows that player with a role and no character chosen yet

#### Scenario: A player leaves the waiting room
- **WHEN** a player disconnects from a waiting room
- **THEN** every client no longer shows that player and that player's character becomes available

### Requirement: Only the host starts, and only when ready
The host SHALL be able to start the match only while the room is waiting, at least one survivor is in the room, and every survivor has chosen a character. Any other attempt to start SHALL be ignored by the server, whoever sends it. Non-host players SHALL NOT be able to start the match.

#### Scenario: Everyone is ready
- **WHEN** every survivor has chosen a character and the host presses start
- **THEN** the room starts playing

#### Scenario: A survivor has not chosen
- **WHEN** one survivor has no character and the host presses start
- **THEN** the start button is not available to press, and a start request that arrives anyway is ignored

#### Scenario: A client tries to start
- **WHEN** a client other than the host sends a start request
- **THEN** the room stays waiting

#### Scenario: No survivors
- **WHEN** the only player in the room is the boss
- **THEN** the match cannot be started

### Requirement: Starting spawns every character at once
When the room starts playing, the server SHALL spawn one character for each survivor, using the survivor's chosen character and the spawn position of that survivor's role, and one for the boss. Every client SHALL see all characters appear from the same start.

#### Scenario: Start with four survivors
- **WHEN** four survivors have chosen four different characters and the host starts
- **THEN** four characters appear on every client, each as the character its owner chose, at the spawn positions of their roles, and mobs begin to come

#### Scenario: Start state is fresh
- **WHEN** the room starts playing
- **THEN** every survivor starts at level 1 with full health, full mana, no learned skills and no experience

### Requirement: Joining is closed while playing
A room that is playing SHALL refuse new players with a clear reason, and the players already in the room SHALL NOT be affected. A room that is waiting SHALL accept players as long as it has room.

#### Scenario: Join during play
- **WHEN** a new player tries to join a playing room
- **THEN** the join is rejected with a reason shown to that player, and the playing room is unchanged

#### Scenario: Join while waiting
- **WHEN** a new player joins a waiting room that has a free slot
- **THEN** the player enters the waiting room

### Requirement: The host can return the room to waiting
The host SHALL be able to end the match from a playing room, and the server SHALL then remove every character, mob, projectile and skill effect and reset all growth, and put the room back to waiting with the same connected players. Every character chosen before SHALL stay chosen and SHALL be changeable. Only the host SHALL be able to end the match.

#### Scenario: Host ends the match
- **WHEN** the host ends a playing match
- **THEN** every client sees no characters, mobs, projectiles or skill effects, shows the waiting room with the same players, and every survivor still has the chosen character

#### Scenario: A second match starts fresh
- **WHEN** the host starts again after returning
- **THEN** levels, experience, learned skills, health, mana and mob count start from the beginning, exactly as in the first match

#### Scenario: A client tries to end the match
- **WHEN** a client other than the host sends an end request
- **THEN** the match continues

### Requirement: A match survives players leaving
A playing room SHALL keep running when a client leaves, and SHALL free that client's slot and character. A client who leaves during play SHALL NOT be able to rejoin the running match; they can join once the room is waiting again.

#### Scenario: Client leaves during play
- **WHEN** a client disconnects from a playing room
- **THEN** that character disappears for everyone, the rest of the match continues, and growth state for that client is removed

### Requirement: The host leaving ends the room
When the host disconnects, in either state, the room SHALL end and every remaining client SHALL return to the connection screen with a notice, as before.

#### Scenario: Host leaves a waiting room
- **WHEN** the host leaves a waiting room
- **THEN** every client returns to the connection screen with a notice
