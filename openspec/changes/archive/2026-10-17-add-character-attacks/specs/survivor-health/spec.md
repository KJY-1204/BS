# Spec Delta

## ADDED Requirements

### Requirement: Health can be restored by skills
A survivor's health SHALL be restorable by an amount, never above that survivor's maximum, and only while the survivor is alive. Restoring health SHALL be shown to every player through the health value and bar.

#### Scenario: Partial heal
- **WHEN** a survivor with 40 of 100 health is healed by 30
- **THEN** health is 70

#### Scenario: Capped
- **WHEN** a survivor with 90 of 100 is healed by 30
- **THEN** health is 100

#### Scenario: Dead survivors
- **WHEN** a dead survivor would be healed
- **THEN** the survivor stays dead

### Requirement: A skill can make a survivor invulnerable for a time
A survivor SHALL be able to be made unhurtable for a time by a skill. While it lasts, damage SHALL NOT reduce health, and it SHALL stop by itself. A longer protection already running SHALL NOT be shortened by a shorter one.

#### Scenario: Protected
- **WHEN** a survivor under protection is touched by mobs
- **THEN** health does not drop

#### Scenario: Ends
- **WHEN** the time has passed
- **THEN** damage reduces health again
