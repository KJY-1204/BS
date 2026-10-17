# Spec Delta

## ADDED Requirements

### Requirement: Mobs have health and can be killed
Every mob SHALL have health with a fixed maximum. The server SHALL reduce a mob's health by the damage it is given and SHALL remove the mob when its health reaches zero.

#### Scenario: Damage
- **WHEN** a mob with 30 health takes 10 damage
- **THEN** the mob stays alive with 20 health

#### Scenario: Kill
- **WHEN** a mob's health reaches zero
- **THEN** the server removes the mob and every client stops displaying it

#### Scenario: Damage to an unknown mob
- **WHEN** damage is applied to a mob that no longer exists
- **THEN** nothing changes

### Requirement: Mobs hurt survivors on contact
A mob that touches a living survivor SHALL deal contact damage to that survivor. The survivor's protection time after a hit SHALL limit how often damage is dealt, and dead survivors SHALL NOT be damaged.

#### Scenario: Touching a survivor
- **WHEN** a mob reaches a living survivor who is not protected
- **THEN** the survivor takes the contact damage

#### Scenario: A crowd
- **WHEN** many mobs touch the same survivor at once
- **THEN** the survivor takes the contact damage once per protection time, not once per mob

#### Scenario: Dead survivor
- **WHEN** a mob touches a dead survivor
- **THEN** no damage is dealt
