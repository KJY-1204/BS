# Spec Delta

## MODIFIED Requirements

### Requirement: Projectiles harm mobs and the boss only
A survivor's projectile SHALL damage mobs and the boss, and SHALL NOT damage any survivor, passing through them. (The boss's own projectiles follow the boss-projectiles rules and hurt survivors only.)

#### Scenario: Teammate in the way
- **WHEN** a survivor's projectile passes through another survivor's position
- **THEN** that survivor takes no damage and the projectile continues

#### Scenario: The boss in the way
- **WHEN** a survivor's projectile touches the boss
- **THEN** the boss takes the projectile's damage as the boss-combat rules say

#### Scenario: A boss projectile in the way
- **WHEN** a survivor's projectile passes through the position of a boss projectile
- **THEN** neither is affected, because projectiles do not collide with each other
