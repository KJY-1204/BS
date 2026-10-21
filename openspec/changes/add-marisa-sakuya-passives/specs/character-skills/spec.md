# Spec Delta

## MODIFIED Requirements

### Requirement: The beam hits every mob on a line
The Marisa Q skill SHALL damage every mob on a straight line from her along the aim she had when the server fired it, up to a length and within a width that grow with rank and with her range passive, and SHALL NOT stop at the first mob. The line SHALL stop at the arena edge. Mobs it kills SHALL give experience.

#### Scenario: Mobs in a row
- **WHEN** the beam fires toward four mobs in a row inside its length and one mob to the side outside its width
- **THEN** the four take damage and the one to the side does not

#### Scenario: Beyond the length
- **WHEN** a mob stands on the line farther than the beam's length
- **THEN** it takes no damage

#### Scenario: Widened by the passive
- **WHEN** a mob stands beyond the defined length or beside the defined width but inside the length or width multiplied by the range scale
- **THEN** it takes damage

### Requirement: Time stop freezes every mob
The Sakuya Q skill SHALL stop every mob for a time that grows with rank and with her duration passive: frozen mobs do not move, new mobs do not appear, and frozen mobs do not hurt survivors on contact. Frozen mobs SHALL still take damage from shots and skills, and SHALL move again when the time ends. Using the skill again while it is active SHALL NOT shorten it.

#### Scenario: Mobs stand still
- **WHEN** the skill fires with mobs chasing survivors
- **THEN** no mob moves and none appears for the whole duration, on every client

#### Scenario: Harmless on contact
- **WHEN** a survivor stands inside frozen mobs
- **THEN** the survivor takes no contact damage

#### Scenario: Damage still works
- **WHEN** a shot hits a frozen mob
- **THEN** the mob takes damage and can die

#### Scenario: Time resumes
- **WHEN** the duration has passed
- **THEN** mobs chase again and new mobs appear again

#### Scenario: Lengthened by the passive
- **WHEN** she uses the skill with her duration passive learned
- **THEN** mobs stay stopped for the defined duration multiplied by the duration scale
