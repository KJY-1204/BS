# multiplayer-session Specification

## Purpose

Defines how a game room is created, joined and left: a host opens a Relay-backed room, up to five players join by code, and each player is assigned a survivor or boss role by the server.

## Requirements

### Requirement: Host can create a room
The system SHALL let a player start a room as host, which obtains a Relay allocation and shows a join code to the host.

#### Scenario: Host starts a room
- **WHEN** the player presses the Host button
- **THEN** the system creates a room, the host is connected as a player, and a join code is displayed

#### Scenario: Relay unavailable
- **WHEN** the Host button is pressed and the Relay allocation fails
- **THEN** no room is created and an error message is shown to the player

### Requirement: Client can join by code
The system SHALL let a player join an existing room by entering its join code.

#### Scenario: Valid code
- **WHEN** a player enters a valid join code of a room that has a free slot and presses Join
- **THEN** the player connects to the room and appears to every connected player

#### Scenario: Invalid code
- **WHEN** a player enters a code that matches no room
- **THEN** the connection attempt fails and an error message is shown, with no change to the room

### Requirement: Room capacity is five players
The system SHALL allow at most five players in a room: four survivor slots and one boss slot.

#### Scenario: Room is full
- **WHEN** a sixth player tries to join a room that already has five players
- **THEN** the join is rejected and the existing players are not affected

### Requirement: Server assigns roles
The server SHALL assign each player exactly one role, either the boss or a numbered survivor slot, and SHALL tell every client each player's role. The host chooses its own role when starting the room, and each joiner receives the lowest free survivor slot, or the boss role if it is still free and no survivor slot is.

#### Scenario: Host starts as boss
- **WHEN** the host starts a room with the boss role selected and four players join
- **THEN** the host holds the boss role and the joiners hold survivor slots 1 to 4 in join order

#### Scenario: Host starts as survivor
- **WHEN** the host starts a room as a survivor and four players join
- **THEN** the host holds survivor slot 1, the next three joiners hold survivor slots 2 to 4, and the fourth joiner holds the boss role

### Requirement: Disconnect is handled
The system SHALL free a player's slot and remove that player's character from all clients when the player disconnects.

#### Scenario: Client leaves
- **WHEN** a connected client disconnects
- **THEN** the remaining players no longer see that character and the slot becomes available to a new joiner

#### Scenario: Host leaves
- **WHEN** the host disconnects
- **THEN** the room ends and every remaining client returns to the connection screen with a notice

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
