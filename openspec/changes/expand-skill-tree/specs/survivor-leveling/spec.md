# Spec Delta

## MODIFIED Requirements

### Requirement: Killing a mob gives experience at once
When a mob is killed, the server SHALL give experience immediately, without any item to pick up. The base amount SHALL be the same for every recipient of that kill; a recipient with an experience passive SHALL receive that base amount raised by the passive, and nobody else's amount SHALL change.

#### Scenario: A mob dies
- **WHEN** a projectile or a skill kills a mob
- **THEN** the experience is granted in the same server step as the kill

#### Scenario: A recipient with the experience passive
- **WHEN** a mob dies near two survivors and only one has the experience passive
- **THEN** that one receives the base amount raised by the passive and the other receives the base amount
