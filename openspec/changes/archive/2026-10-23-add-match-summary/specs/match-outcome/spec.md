# Spec Delta

## ADDED Requirements

### Requirement: The result shows how the match went
When a match with a boss ends, every client SHALL be shown, with the result, a summary of the match and of each survivor that was in the match at its end. The summary of the match SHALL give how long the match lasted, how much of the boss's health was left and its maximum, and how many of the boss's devices were placed and destroyed. The summary of a survivor SHALL give its character and role, the level it reached, how many times it fell, how many times it was revived and whether it stands. The numbers SHALL be those of the moment the match ended and SHALL be the same on every client, the boss player's included. A survivor who left during the match SHALL NOT be listed. A match without a boss SHALL have no result and no summary.

#### Scenario: A summary after a win
- **WHEN** a match ends because the boss was defeated
- **THEN** every client shows the length of the match, a boss health of zero, the devices placed and destroyed, and a line for every survivor with its character, role, level, falls, revivals and whether it stands

#### Scenario: A summary after a wipe
- **WHEN** a match ends because every survivor has fallen
- **THEN** every client shows the boss's health left, and every survivor's line shows that it has fallen

#### Scenario: Counting falls and revivals
- **WHEN** a survivor falls twice in a match and is revived once
- **THEN** its line shows two falls and one revival, and it is shown as fallen

#### Scenario: A survivor who left
- **WHEN** a survivor leaves the room during the match and the match then ends
- **THEN** the summary has no line for that survivor and the other lines are unchanged

#### Scenario: Everyone sees the same
- **WHEN** a match ends
- **THEN** the boss player and every survivor see the same numbers

#### Scenario: Length of the match
- **WHEN** a match ends after the countdown has run for 754 seconds
- **THEN** the summary shows a match length of 12:34

#### Scenario: Practice match
- **WHEN** a match without a boss is ended by the host
- **THEN** no result and no summary are shown

### Requirement: The summary goes away with the result
The summary SHALL be shown only while the room is finished. When the host returns the room to waiting, or a new match starts, no summary of the earlier match SHALL remain on any client, and a new match SHALL count falls, revivals and devices from zero.

#### Scenario: Back to waiting
- **WHEN** the host returns a finished room to waiting
- **THEN** no summary is shown on any client

#### Scenario: A second match
- **WHEN** a second match ends after the first
- **THEN** its summary has only what happened in the second match
