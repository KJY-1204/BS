# Spec Delta

## ADDED Requirements

### Requirement: Base values come from the character
A survivor's level-1 maximum health, maximum mana and mana recovery SHALL come from that survivor's character data. The per-level and per-skill increases SHALL be the common amounts and SHALL add to the character's base values, with the same no-heal rules.

#### Scenario: Level up on a character with a larger base
- **WHEN** two survivors with different base maximum health both gain one level
- **THEN** both maximums rise by the same amount, and the survivor with the larger base is still larger by the same difference

#### Scenario: Leaving the match resets growth
- **WHEN** the room returns to waiting
- **THEN** every survivor's level, experience, mana and learned skills are cleared, and the next match starts again from the character's base values
