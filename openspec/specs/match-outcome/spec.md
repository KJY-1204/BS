# match-outcome Specification

## Purpose

Defines how a match ends: the time limit, who wins and why, what a finished match looks like, and how the room goes back to waiting. A match with a boss is a race between the survivors killing the boss and the boss defeating them within the time; a match without a boss has no limit and no winner.

## Requirements

### Requirement: A match with a boss has a time limit
When a match starts with a boss player, the server SHALL start a countdown from a configured time limit and SHALL tell every client the remaining time. The default time limit SHALL be fifteen minutes, and a development setting MAY replace it. The countdown SHALL run only while the match is playing. A match without a boss player SHALL have no countdown.

#### Scenario: Countdown
- **WHEN** a match with a boss runs for 30 seconds
- **THEN** every client shows 30 seconds less than the limit

#### Scenario: Practice match
- **WHEN** a match starts with no boss player
- **THEN** no countdown is shown and the match runs until the host ends it

#### Scenario: A fresh countdown
- **WHEN** a second match starts after the first ended
- **THEN** the countdown starts again from the full limit

#### Scenario: Default limit
- **WHEN** a match with a boss starts with no development setting
- **THEN** the countdown starts at fifteen minutes

#### Scenario: Replaced limit
- **WHEN** a match starts with the development setting for the time limit
- **THEN** the countdown starts from that value instead

### Requirement: The survivors win by defeating the boss
The match SHALL end with a survivor victory when the boss's health reaches zero before the time runs out. The boss's defeat SHALL take priority over every other way the match could end in the same moment.

#### Scenario: Boss defeated
- **WHEN** the boss's health reaches zero with time remaining
- **THEN** the match ends as a survivor victory with the reason "boss defeated"

#### Scenario: Both at once
- **WHEN** the boss's health reaches zero in the same step as the last survivor falls
- **THEN** the match ends as a survivor victory

### Requirement: The boss wins by defeating the survivors or by time
The match SHALL end with a boss victory when no survivor is alive, counting every fallen survivor and every survivor who left as not alive, or when the time limit runs out with the boss still alive.

#### Scenario: All survivors fallen
- **WHEN** the last living survivor falls and the boss is alive
- **THEN** the match ends as a boss victory with the reason "all survivors fallen"

#### Scenario: Time runs out
- **WHEN** the countdown reaches zero with the boss alive and a survivor alive
- **THEN** the match ends as a boss victory with the reason "time up"

#### Scenario: A revival in time
- **WHEN** a fallen survivor is revived before the last living survivor falls
- **THEN** the match does not end

### Requirement: A boss who leaves forfeits the match
When the boss player disconnects during a match that had a boss, the match SHALL end with a survivor victory and the reason "boss left", unless the match already ended.

#### Scenario: Boss disconnects
- **WHEN** the boss player disconnects during play
- **THEN** the match ends as a survivor victory with the reason "boss left"

### Requirement: A finished match stops
When the match ends, the room SHALL enter a finished state in which nothing of the match advances: mobs stop and none appear, projectiles and skill effects stop, survivors and the boss cannot move or attack, no skill can be used, no choice timer runs, and the countdown stops. Every character stays where it was on every client. The finished state SHALL last until the host returns the room to waiting.

#### Scenario: Everything stops
- **WHEN** a match ends
- **THEN** no mob moves, nobody can move, shoot or use a skill, and the remaining time stays as it was on every client

#### Scenario: The result stays
- **WHEN** the match has ended and nobody does anything
- **THEN** the result stays on every screen until the host returns the room to waiting

### Requirement: Everyone sees the result
When the match ends, every client SHALL show the outcome (survivors win or boss wins), the reason, and the time that was left. The result SHALL NOT be shown while the match is playing or waiting.

#### Scenario: Result screen
- **WHEN** the survivors defeat the boss with 1 minute 12 seconds left
- **THEN** every client shows a survivor victory, the reason, and 1:12 left

### Requirement: The host returns the room to waiting after a result
After a result the host SHALL be able to return the room to waiting with the same players, which clears every character, mob, projectile, effect and growth as ending a match does. A client who is not the host SHALL see that the host decides and SHALL NOT be able to return the room.

#### Scenario: Back to waiting
- **WHEN** the host chooses to return after a result
- **THEN** every client sees the waiting room with the same players and the chosen characters kept, with no result shown

#### Scenario: A second match
- **WHEN** the host starts again
- **THEN** the match begins as fresh as the first: full boss and survivor health, full time, level 1, no mobs

### Requirement: Everyone sees the time and the boss's health
During a match with a boss, every client SHALL show the remaining time and the boss's health bar at the top of the screen.

#### Scenario: Survivor view
- **WHEN** a survivor plays
- **THEN** the survivor sees the remaining time and the boss's health at the top of the screen

#### Scenario: No boss
- **WHEN** a match has no boss
- **THEN** neither the time nor a boss bar is shown
