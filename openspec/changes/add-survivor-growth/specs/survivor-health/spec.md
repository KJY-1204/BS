# Spec Delta

## ADDED Requirements

### Requirement: Maximum health can rise per survivor
A survivor's maximum health SHALL be able to differ from other survivors' and to rise during a room. The maximum SHALL be visible to every player, and health bars SHALL show health as a fraction of that survivor's own maximum.

#### Scenario: A higher maximum
- **WHEN** a survivor's maximum health rises from 100 to 110
- **THEN** every client shows the new maximum and the health bar uses it

### Requirement: Raising the maximum does not heal
When a survivor's maximum health rises, current health SHALL stay the same. Health SHALL never exceed the maximum.

#### Scenario: No healing
- **WHEN** a survivor with 40 health has the maximum raised from 100 to 110
- **THEN** current health is still 40

### Requirement: Revival uses the survivor's own maximum
A revived survivor SHALL come back with the configured fraction of that survivor's own current maximum health.

#### Scenario: Revived with a raised maximum
- **WHEN** a survivor with a maximum of 120 is revived and the fraction is half
- **THEN** the survivor comes back with 60 health
