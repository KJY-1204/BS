# Spec Delta

## ADDED Requirements

### Requirement: Damage follows the shooter's own value
Each projectile SHALL deal the damage value its shooter had when it was fired, so a later change to the shooter's damage does not alter projectiles already in flight.

#### Scenario: Two survivors with different damage
- **WHEN** survivor A has damage 12 and survivor B has damage 10, and both hit a mob
- **THEN** the mob takes 12 from A's projectile and 10 from B's

#### Scenario: Damage changes in flight
- **WHEN** a survivor levels up while a projectile is in flight
- **THEN** that projectile still deals the damage it was fired with

### Requirement: The cooldown follows the shooter's own value
The firing cooldown SHALL follow each survivor's own value, which can be shortened by skills, and SHALL return to normal when the effect ends.

#### Scenario: A shorter cooldown
- **WHEN** a survivor's cooldown is halved by a skill and the survivor holds fire for one second
- **THEN** the survivor fires twice as many shots as an unmodified survivor

#### Scenario: Effect ends
- **WHEN** the skill effect ends
- **THEN** the survivor's cooldown is the normal one again
