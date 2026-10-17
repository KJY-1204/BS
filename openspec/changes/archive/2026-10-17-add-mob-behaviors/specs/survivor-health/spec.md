# Spec Delta

## MODIFIED Requirements

### Requirement: A hit protects the survivor briefly
After a survivor takes damage from any source (a mob's touch, the boss's touch, a boss projectile, a mob's shot or a mob's blast), the survivor SHALL take no damage from any source for a short protection time, and the same protection SHALL apply after being revived.

#### Scenario: Repeated contact
- **WHEN** a survivor is touched by a mob again during the protection time
- **THEN** no further damage is dealt until the protection time has passed

#### Scenario: Protection ends
- **WHEN** the protection time has passed and a mob still touches the survivor
- **THEN** the survivor takes damage again

#### Scenario: A boss projectile during protection
- **WHEN** a boss projectile touches a survivor who is within the protection time
- **THEN** the survivor takes no damage and the projectile continues

#### Scenario: A boss projectile starts protection
- **WHEN** a boss projectile hurts a survivor
- **THEN** a mob touching that survivor right afterwards deals no damage until the protection time has passed

#### Scenario: A blast during protection
- **WHEN** an exploding mob blasts while a survivor inside its radius is within the protection time
- **THEN** that survivor takes no damage from the blast

#### Scenario: A mob's shot starts protection
- **WHEN** a mob's shot hurts a survivor
- **THEN** a blast or touch right afterwards deals no damage until the protection time has passed
