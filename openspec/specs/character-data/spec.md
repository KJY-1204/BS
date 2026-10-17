# character-data Specification

## Purpose

Defines how playable characters are described as data and how a character's data shapes a survivor: starting health and mana, mana recovery, movement speed and look. It is the base that character-specific attacks and skills are attached to later.

## Requirements

### Requirement: Characters are defined as data
The system SHALL define each playable character in data, with a stable identifier, a display name, a starting maximum health, a starting maximum mana, a mana recovery rate, a movement speed, a placeholder color, and a place for a sprite reference that is empty for now. Adding or changing a character SHALL NOT need changes to the rules that use the data. Four characters SHALL exist: Reimu, Marisa, Sakuya and Sanae, with different values so that choosing matters.

#### Scenario: Four distinct characters
- **WHEN** the characters are listed
- **THEN** there are exactly four, each with a different identifier and name, and no two have identical starting values and color

#### Scenario: Identifiers stay stable
- **WHEN** a character is renamed or its values are tuned
- **THEN** its identifier does not change, and choices made by identifier remain valid

### Requirement: The server applies the character to the survivor
When a survivor is created, the server SHALL set the survivor's starting maximum health, starting maximum mana, mana recovery and movement speed from that survivor's character data, and the survivor SHALL start with full health and full mana of those values. Growth per level and from skills SHALL stay the same common rules for every character and add to the character's starting values.

#### Scenario: Different starting values
- **WHEN** two survivors with different characters start
- **THEN** each starts with its own character's full health and full mana, and each shows its own maximum

#### Scenario: Growth adds to the character's base
- **WHEN** a survivor of any character reaches level 2
- **THEN** the maximum health and mana rise by the common per-level amounts above that character's starting values, and current health and mana are not healed

### Requirement: Movement speed follows the character
A character's movement speed SHALL come from its data on the server, and the owner's own local movement SHALL use the same value, so that the owner is not corrected during normal movement.

#### Scenario: A faster character
- **WHEN** a survivor whose character has a higher speed holds a direction for the same time as a slower one
- **THEN** the faster character is farther along on the server and on every client

#### Scenario: No correction in normal play
- **WHEN** a survivor of any character moves normally with a healthy connection
- **THEN** the owner's local position and the server's position stay together without a visible correction

### Requirement: Every client shows the character the same way
Every client SHALL show each survivor with its character's color, and the character a survivor plays SHALL be known to every client, not only to the owner.

#### Scenario: Others see the chosen character
- **WHEN** a match starts
- **THEN** each client shows each survivor in that survivor's character color, and the character names can be shown for every survivor

### Requirement: Attack and active skills are the character's, passives are shared
A character SHALL define its basic attack and own its two active skills, while the passive skills, the level-up growth and the skill selection rules SHALL stay the same for every character.

#### Scenario: Same passives
- **WHEN** two survivors of different characters are offered passive skills
- **THEN** both can learn the same passives with the same effects

#### Scenario: Different attack
- **WHEN** two survivors of different characters fire
- **THEN** their attacks follow their own character's data
