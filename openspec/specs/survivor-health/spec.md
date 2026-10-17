# survivor-health Specification

## Purpose

Defines survivor health: how much each survivor has, how damage and the short protection after a hit work, when a survivor dies, what a dead survivor can no longer do, and how health is shown to players.

## Requirements

### Requirement: Survivors have health
Every survivor SHALL have a health value with a fixed maximum. A survivor SHALL start a room with full health, and every player SHALL be able to see every survivor's current health.

#### Scenario: Room start
- **WHEN** a survivor's character is created
- **THEN** the survivor has maximum health and all clients show that value

#### Scenario: Health is visible to everyone
- **WHEN** a survivor's health changes
- **THEN** every client shows the new value for that survivor

### Requirement: Health is controlled by the server
Only the server SHALL change a survivor's health. A client SHALL NOT be able to raise or lower any survivor's health.

#### Scenario: Client tries to set health
- **WHEN** a client attempts to change a survivor's health
- **THEN** the server ignores the attempt and the health value is unchanged

### Requirement: Damage reduces health and never goes below zero
Damage dealt to a survivor SHALL reduce health by the damage amount, and health SHALL NOT go below zero.

#### Scenario: Normal hit
- **WHEN** a survivor with health 100 takes damage 10
- **THEN** the survivor has health 90

#### Scenario: Overkill
- **WHEN** a survivor with health 5 takes damage 10
- **THEN** the survivor has health 0

### Requirement: A hit protects the survivor briefly
After a survivor takes damage from any source (a mob's touch, the boss's touch or a boss projectile), the survivor SHALL take no damage from any source for a short protection time, and the same protection SHALL apply after being revived.

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

### Requirement: A survivor dies at zero health
A survivor whose health reaches zero SHALL die. Death SHALL be visible to every player.

#### Scenario: Last hit
- **WHEN** a survivor's health reaches zero
- **THEN** the survivor is dead on every client and is shown as fallen

### Requirement: A dead survivor cannot act
A dead survivor SHALL NOT move, SHALL NOT fire, and SHALL NOT be chosen as a target by mobs. The dead survivor's character SHALL stay where it fell.

#### Scenario: Input while dead
- **WHEN** a dead survivor's player presses move or fire
- **THEN** the character does not move and no projectile is fired

#### Scenario: Mobs ignore the dead
- **WHEN** a survivor dies
- **THEN** mobs stop chasing that survivor and chase the nearest living survivor instead

### Requirement: Own health is shown clearly
Each player SHALL see their own survivor's current and maximum health on screen, and every survivor SHALL have a health bar next to its character.

#### Scenario: Taking damage
- **WHEN** the local survivor takes damage
- **THEN** the on-screen value and the bar over the character both show the lower health

#### Scenario: Dead local survivor
- **WHEN** the local survivor is dead
- **THEN** the screen shows that the survivor is dead

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

### Requirement: Survivors start with their character's health
A survivor SHALL start the match with full health of that survivor's character's starting maximum, and every player SHALL see that maximum for every survivor.

#### Scenario: Different starting maximums
- **WHEN** a match starts with two survivors whose characters have different starting maximum health
- **THEN** each has full health and a health bar over its own maximum on every client

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
