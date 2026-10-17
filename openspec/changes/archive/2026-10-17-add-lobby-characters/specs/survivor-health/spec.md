# Spec Delta

## ADDED Requirements

### Requirement: Survivors start with their character's health
A survivor SHALL start the match with full health of that survivor's character's starting maximum, and every player SHALL see that maximum for every survivor.

#### Scenario: Different starting maximums
- **WHEN** a match starts with two survivors whose characters have different starting maximum health
- **THEN** each has full health and a health bar over its own maximum on every client
