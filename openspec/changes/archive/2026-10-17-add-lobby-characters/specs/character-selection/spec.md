# Spec Delta

## Purpose

Defines how survivors choose their character in the waiting room: one character per player and one player per character, the server deciding who gets a character first, and how choices are shown.

## ADDED Requirements

### Requirement: Survivors choose a character while waiting
While the room is waiting, each survivor SHALL be able to choose one of the available characters. A choice SHALL only be possible in the waiting room and only for survivors. The boss SHALL NOT choose a character.

#### Scenario: Choosing a free character
- **WHEN** a survivor chooses a character nobody holds
- **THEN** the survivor holds it and every client shows that survivor with that character

#### Scenario: Choosing while playing
- **WHEN** a choice request arrives while the room is playing
- **THEN** it is ignored and nothing changes

#### Scenario: The boss tries to choose
- **WHEN** a request to choose a character arrives from the boss
- **THEN** it is ignored

### Requirement: One player per character
A character SHALL be held by at most one player at a time, and the server SHALL decide. Whoever's request reaches the server first holds the character; a later request for a held character SHALL be refused and the requester SHALL be told it is taken.

#### Scenario: Two players choose the same character
- **WHEN** two survivors choose the same free character and the server receives one first
- **THEN** the first one holds it, the other does not, and the other is told the character is taken

#### Scenario: Choosing a held character
- **WHEN** a survivor chooses a character another survivor holds
- **THEN** the choice is refused, the survivor's previous choice (if any) is unchanged, and all clients show the character as held by its owner

### Requirement: A survivor can change or release a choice
A survivor SHALL be able to switch to another free character or release the choice, and the character left behind SHALL become available to others at the same moment.

#### Scenario: Switching
- **WHEN** a survivor holding one character chooses a different free character
- **THEN** the survivor holds the new one and the old one is free for anyone

#### Scenario: Releasing
- **WHEN** a survivor releases the choice
- **THEN** the survivor has no character and cannot be started with

### Requirement: Requests are checked by the server
The client SHALL send only which character it wants. The server SHALL ignore unknown character numbers, requests from players who are not survivors in the room, and requests arriving in the wrong room state, without changing anything.

#### Scenario: Unknown character number
- **WHEN** a request names a character that does not exist
- **THEN** it is ignored

### Requirement: A leaving player gives up the character
When a player disconnects, any character the player held SHALL become available to the others immediately.

#### Scenario: Holder leaves
- **WHEN** the survivor holding a character disconnects
- **THEN** the character shows as free on every client

### Requirement: Choices are visible to everyone
Every player SHALL see which characters are held, by whom, and which are free, and each player SHALL see their own current choice clearly. A refusal SHALL be shown to the player who was refused.

#### Scenario: Own choice is clear
- **WHEN** a survivor holds a character
- **THEN** that survivor's screen marks it as their own, and the other characters show as free or held by others
