# Spec Delta

## REMOVED Requirements

### Requirement: Projectiles only harm mobs
**Reason**: The boss is now a target that survivors' projectiles damage; only the other survivors are passed through.
**Migration**: See the requirement below and the boss-combat capability.

## ADDED Requirements

### Requirement: Projectiles harm mobs and the boss only
A projectile SHALL damage mobs and the boss, and SHALL NOT damage any survivor, passing through them.

#### Scenario: Teammate in the way
- **WHEN** a projectile passes through another survivor's position
- **THEN** that survivor takes no damage and the projectile continues

#### Scenario: The boss in the way
- **WHEN** a projectile touches the boss
- **THEN** the boss takes the projectile's damage as the boss-combat rules say
