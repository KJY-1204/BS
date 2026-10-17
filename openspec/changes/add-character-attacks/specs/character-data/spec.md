# Spec Delta

## REMOVED Requirements

### Requirement: Common rules are not character specific yet
**Reason**: Basic attacks and the Q/E active skills are now defined per character (see character-attacks and character-skills). Only the passive skills stay common.
**Migration**: The remaining shared rules are stated in the requirement below.

## ADDED Requirements

### Requirement: Attack and active skills are the character's, passives are shared
A character SHALL define its basic attack and own its two active skills, while the passive skills, the level-up growth and the skill selection rules SHALL stay the same for every character.

#### Scenario: Same passives
- **WHEN** two survivors of different characters are offered passive skills
- **THEN** both can learn the same passives with the same effects

#### Scenario: Different attack
- **WHEN** two survivors of different characters fire
- **THEN** their attacks follow their own character's data
