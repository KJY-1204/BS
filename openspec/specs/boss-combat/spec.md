# boss-combat Specification

## Purpose

Defines the boss as a fighter: the data that describes it, how it takes damage from the survivors' shots and skills, what happens when it falls, how it hurts survivors it touches, and what the boss player and everyone else see of its health.

## Requirements

### Requirement: The boss is defined as data
The boss SHALL be defined in data with a display name, a maximum health, a movement speed, a body radius, a contact damage value and a color, and the data SHALL be changeable without changing the rules that use it. The boss SHALL start a match with full health. Until boss kinds exist there SHALL be exactly one boss definition.

#### Scenario: Starting values
- **WHEN** a match starts with a boss player
- **THEN** the boss has full health of its definition, moves at its definition's speed and is drawn in its definition's color

#### Scenario: Changing a value
- **WHEN** the boss's maximum health is changed in the data
- **THEN** the boss starts the next match with the new maximum and no rule needs to change

### Requirement: Survivors' shots hurt the boss
A survivor's projectile SHALL damage the boss when it touches the boss body, by the projectile's own damage, and the boss SHALL take no damage from anything else a survivor does by chance. A projectile that cannot pierce SHALL stop at the boss. A piercing projectile SHALL count the boss as one of the targets it may hit and SHALL hit the boss at most once.

#### Scenario: A shot reaches the boss
- **WHEN** a projectile with damage 14 touches the boss
- **THEN** the boss loses 14 health and the projectile ends there

#### Scenario: A piercing shot
- **WHEN** a piercing projectile that can hit four targets passes two mobs and then the boss
- **THEN** it damages all three and keeps flying with one hit left

#### Scenario: Not twice
- **WHEN** a piercing projectile overlaps the boss over several simulation steps
- **THEN** the boss takes its damage once

#### Scenario: A mob in front
- **WHEN** a projectile that cannot pierce meets a mob before it reaches the boss
- **THEN** it stops at the mob and the boss takes no damage

### Requirement: Skills that hurt mobs also hurt the boss
The area burst, the beam and the ring of knives SHALL damage the boss under the same conditions as they damage mobs (inside the radius, on the line, or by the knives' hits). The skills that stop mobs or push them away SHALL NOT affect the boss, and the heal and the barrier SHALL NOT touch the boss.

#### Scenario: Burst reaches the boss
- **WHEN** the area burst fires with the boss inside its radius
- **THEN** the boss takes the burst's damage

#### Scenario: Beam reaches the boss
- **WHEN** the beam fires along a line that crosses the boss
- **THEN** the boss takes the beam's damage

#### Scenario: Time stop
- **WHEN** a survivor stops time
- **THEN** the boss keeps moving normally

#### Scenario: Wind veil
- **WHEN** a survivor pushes the mobs around her and the boss stands inside the radius
- **THEN** the boss is not moved

### Requirement: The boss's health is decided by the server and seen by everyone
The server SHALL keep the boss's health and SHALL tell every client the current and maximum health. Every client SHALL show a health bar over the boss. A boss hit SHALL be shown on every client like a mob hit.

#### Scenario: Everyone sees the bar
- **WHEN** the boss loses health
- **THEN** every client shows the boss's bar lower and a hit effect at the hit position

#### Scenario: A client cannot change it
- **WHEN** a client tries to set the boss's health
- **THEN** nothing changes

### Requirement: A boss at zero health falls
When the boss's health reaches zero it SHALL fall: it cannot move, it is shown greyed out like a fallen survivor, and it can take no more damage. A boss SHALL NOT be revived.

#### Scenario: Defeat
- **WHEN** the boss's health reaches zero
- **THEN** the boss stops moving on every client and is shown fallen

### Requirement: The boss hurts survivors it touches
A living boss that touches a living survivor SHALL deal its contact damage to that survivor, which is a different value from a mob's. The short protection after a hit SHALL apply as for any damage, so a survivor is not hit again until it ends. A fallen boss SHALL NOT deal damage.

#### Scenario: Touching the boss
- **WHEN** the boss touches a survivor with enough health
- **THEN** the survivor loses the boss's contact damage and is protected briefly

#### Scenario: Staying in contact
- **WHEN** the boss stays in contact with a survivor
- **THEN** damage repeats no faster than the protection time allows

#### Scenario: A fallen boss
- **WHEN** the boss has zero health and a survivor touches it
- **THEN** the survivor takes no damage

#### Scenario: Time stop does not stop the boss
- **WHEN** mobs are stopped by a skill and the boss touches a survivor
- **THEN** the survivor still takes the boss's contact damage

### Requirement: The boss does not block revival
The boss SHALL NOT interrupt a survivor's revival of another survivor by standing near or touching them, apart from the damage the boss deals and the rule that a moving rescuer resets the gauge.

#### Scenario: Boss next to a rescue
- **WHEN** the boss stands next to a survivor who is reviving another and does not touch them
- **THEN** the revival gauge keeps filling

### Requirement: The boss player sees the fight
The boss player SHALL see the boss's own health, the remaining time, and a summary of every survivor (character, health, and whether fallen), without needing a survivor in view. The camera SHALL keep following the boss's own character.

#### Scenario: Survivor summary
- **WHEN** a survivor is hurt or falls
- **THEN** the boss player's summary shows that survivor's lower health or fallen state

#### Scenario: Survivors do not see the summary
- **WHEN** a survivor looks at the screen
- **THEN** the boss-only survivor summary is not shown, but the time and the boss's health bar are
