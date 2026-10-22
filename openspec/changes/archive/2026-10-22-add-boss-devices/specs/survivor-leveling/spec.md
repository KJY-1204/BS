# Spec Delta

## ADDED Requirements

### Requirement: Destroying a boss device gives experience
When a boss device is destroyed by damage, the server SHALL give experience at once to the survivors who share a kill, by the same rules as a killed mob: every living survivor within the share distance of the place where the device stood receives the full amount, a recipient with an experience passive receives it raised by the passive, and dead survivors and the boss receive nothing. The base amount for a device SHALL be larger than the base amount for a mob and SHALL be adjustable without changing rules elsewhere. A device that disappears because its lifetime has passed, or because the match has ended, SHALL give no experience.

#### Scenario: A device is destroyed
- **WHEN** a survivor's shot destroys a device with two survivors within the share distance and one beyond it
- **THEN** the two receive the device's base amount and the other receives nothing

#### Scenario: More than a mob
- **WHEN** a mob and a device are destroyed in the same place
- **THEN** the experience for the device is larger than the experience for the mob

#### Scenario: A recipient with the experience passive
- **WHEN** a device is destroyed near two survivors and only one has the experience passive
- **THEN** that one receives the device's base amount raised by the passive and the other receives the base amount

#### Scenario: Expiry gives nothing
- **WHEN** a device's lifetime passes without it being destroyed
- **THEN** no survivor receives experience

#### Scenario: Mobs of a device
- **WHEN** a mob created by a device is killed
- **THEN** it gives the same experience as any other mob
