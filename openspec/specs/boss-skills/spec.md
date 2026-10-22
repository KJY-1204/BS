# boss-skills Specification

## Purpose

Defines the boss's two skills on the Q and E keys: a ring of shots and a shockwave around the boss. It covers who may use them and when, the cooldowns, what each skill does to survivors, how the boss's loss of health strengthens them, how everyone sees them, and what the boss player sees of the cooldowns.

## Requirements

### Requirement: The boss has two skills on Q and E
The boss player SHALL be able to use two skills, Q and E, whose effects are those the playing boss kind gives them (the shot ring, the shockwave, the beam or the roar). A skill SHALL be usable only while a match is running and the boss has not fallen, and only when its cooldown has passed. A request from anyone other than the boss player SHALL be ignored. A skill SHALL NOT cost summon energy; each skill SHALL have a cooldown of its own and nothing else. All skill values SHALL be adjustable without changing rules elsewhere.

#### Scenario: Using a skill
- **WHEN** the boss player presses Q while the match is running, the boss stands and the Q cooldown has passed
- **THEN** the Q cooldown starts at once, a warning is shown and the Q skill's effect is released when the warning time has passed

#### Scenario: Still cooling down
- **WHEN** the boss player presses a skill key before its cooldown has passed
- **THEN** nothing happens and the cooldown is unchanged

#### Scenario: No energy needed
- **WHEN** the boss has no summon energy and uses a skill
- **THEN** the skill works and the summon energy is unchanged

#### Scenario: Not the boss
- **WHEN** a survivor's client sends a boss skill request
- **THEN** the server ignores it and nothing happens

#### Scenario: Match over or boss fallen
- **WHEN** the match has ended or the boss has fallen and the boss player presses a skill key
- **THEN** nothing happens

#### Scenario: Time stop
- **WHEN** a survivor's skill has stopped the mobs
- **THEN** the boss's skills still work

### Requirement: A skill is announced before it takes effect
When the boss uses a skill, every player SHALL first see a warning at the place where the skill will take effect, and the skill SHALL take effect only after a fixed warning time. The place and the aim SHALL be those of the moment the skill was used, even if the boss moves during the warning. The cooldown SHALL start when the skill is used, not when it takes effect. If the match ends or the boss falls during the warning, the skill SHALL NOT take effect. The warning SHALL be a placeholder shape that shows the size of what is coming (the full radius for the shockwave, a ring around the boss for the shot ring), and its time SHALL be adjustable without changing rules elsewhere. The warning SHALL NOT need a new kind of network message.

#### Scenario: Time to react
- **WHEN** the boss uses the shockwave
- **THEN** every client shows a warning of the shockwave's radius around the boss, and a survivor who leaves the radius before the warning time has passed takes no damage

#### Scenario: The boss moves
- **WHEN** the boss walks away during the warning
- **THEN** the skill takes effect at the place where it was used

#### Scenario: Cooldown from the use
- **WHEN** the boss uses a skill
- **THEN** its cooldown starts at once and does not wait for the warning to end

#### Scenario: Interrupted
- **WHEN** the match ends or the boss falls during the warning time
- **THEN** the skill does not take effect

#### Scenario: Warning for everyone
- **WHEN** the boss uses a skill
- **THEN** survivors and the boss player see the warning at the moment it starts

### Requirement: Cooldowns run with the match and start ready
Each skill's cooldown SHALL run down only while the match is running, and a new match SHALL start with both skills ready. The two cooldowns SHALL be independent: using one SHALL NOT start or change the other.

#### Scenario: A new match
- **WHEN** a match starts after one in which the boss used both skills
- **THEN** both skills are ready at once

#### Scenario: Independent skills
- **WHEN** the boss uses Q and then E
- **THEN** each has its own cooldown and E is not refused because of Q

### Requirement: The shot ring fires around the boss
When the warning of the shot ring has passed, it SHALL release a fixed number of projectiles from the place where it was used, spread evenly around it, the first in the direction the boss was aiming when it was used. The projectiles SHALL be the boss's own kind of projectile: they hurt survivors only, pass through mobs, the boss, other projectiles, fallen survivors and survivors within the protection time, and count toward the same hard cap on projectiles as every other projectile. If the cap leaves room for only some of them, those SHALL be fired, and if it leaves room for none, none SHALL be.

#### Scenario: A full ring
- **WHEN** the boss uses the shot ring with room for all of the projectiles
- **THEN** the projectiles leave the boss evenly spaced around it and the first follows the boss's aim

#### Scenario: Projectile cap
- **WHEN** the projectile cap leaves room for fewer projectiles than the ring holds
- **THEN** only those that fit are fired and the cooldown starts

#### Scenario: No room at all
- **WHEN** the projectile cap is reached when the shot ring takes effect
- **THEN** no projectile is created

#### Scenario: A hit
- **WHEN** a projectile of the ring touches a survivor who can be hurt
- **THEN** that survivor takes the projectile's damage under the survivor-health rules and the projectile ends there

### Requirement: The shockwave hurts the survivors around the boss
When the warning of the shockwave has passed, it SHALL hurt every living survivor within its radius of the place where it was used, once each, under the survivor-health rules, and SHALL NOT push anyone. A survivor who cannot be hurt at that moment (fallen, or within the protection time) SHALL take no damage. The shockwave SHALL NOT hurt mobs or the boss.

#### Scenario: Survivors in range
- **WHEN** the boss uses the shockwave with two survivors inside its radius and one outside
- **THEN** the two inside each take the damage once and the one outside takes none

#### Scenario: Protected or fallen
- **WHEN** a survivor inside the radius is within the protection time or has fallen
- **THEN** that survivor takes no damage

#### Scenario: Nobody is moved
- **WHEN** the shockwave hurts a survivor
- **THEN** the survivor's position is unchanged by the skill

#### Scenario: Mobs and boss
- **WHEN** a mob stands inside the radius
- **THEN** it takes no damage

### Requirement: A weaker boss uses its skills harder and sooner
The damage of each skill SHALL grow as the boss loses health and its cooldown SHALL shrink, by the same rules as the boss's own shots: the damage rises from the skill's normal damage at full health up to the same multiple as the boss's shots at zero health, rounded and never below one, and the cooldown falls from the normal one at full health to the same fraction as the boss's shots at zero health. Both SHALL be decided on the server from the boss's health at the moment the skill is used (not when the warning ends), so a running cooldown is not changed by later damage. No extra value for this SHALL be sent to clients.

#### Scenario: Full health
- **WHEN** the boss at full health uses a skill
- **THEN** the skill deals its normal damage and its cooldown is the normal one

#### Scenario: Weakened boss
- **WHEN** the boss at low health uses a skill
- **THEN** the skill deals more than its normal damage and its cooldown is shorter

#### Scenario: A running cooldown
- **WHEN** the boss's health falls while a skill is cooling down
- **THEN** that cooldown is not shortened, and the next use gets the shorter one

### Requirement: Everyone sees the skills
Every player SHALL see the warning when a skill is used, the projectiles of the shot ring like the boss's other projectiles (the boss's color, drawn from the fire events) and the shockwave as a short placeholder effect of its size at the place where it was used at the moment it takes effect. The effects SHALL NOT affect the simulation. No new kind of network message and no new kind of combat event SHALL be needed for this.

#### Scenario: A ring is seen
- **WHEN** the shot ring takes effect
- **THEN** every client shows the projectiles leaving the boss in its color

#### Scenario: A shockwave is seen
- **WHEN** the shockwave takes effect
- **THEN** every client shows a short effect of the shockwave radius where it was used

### Requirement: The boss sees its skills and their cooldowns
The boss player's screen SHALL show both skills with their keys and names and how long until each is ready, and the key hint SHALL name Q and E. The server SHALL send the cooldowns only to the boss player, and survivors SHALL NOT receive or show them.

#### Scenario: Boss screen
- **WHEN** a match is running and the boss player looks at the screen
- **THEN** both skills are shown with their keys, names and either "ready" or the remaining seconds

#### Scenario: Survivor screen
- **WHEN** a survivor looks at the screen
- **THEN** no boss skill display is shown and no cooldown value reached the survivor's client

### Requirement: The beam hurts the survivors on a line
When the warning of the beam has passed, it SHALL hurt every living survivor whose body touches a straight line that starts where the beam was used and runs along the aim the boss had then, for the beam's length and half width, once each, under the survivor-health rules. The beam SHALL NOT hurt mobs and SHALL NOT be stopped by anything. The place, the aim and the strengthened damage of the moment it was used SHALL be the ones that count.

#### Scenario: A survivor on the line
- **WHEN** the beam is released with one survivor on the line and one beside it
- **THEN** only the survivor on the line takes the beam's damage

#### Scenario: Beyond the length or behind the boss
- **WHEN** a survivor stands farther along the aim than the beam's length, or behind the boss
- **THEN** the survivor takes no damage

#### Scenario: Protected or fallen
- **WHEN** a survivor on the line is within the protection time or has fallen
- **THEN** that survivor takes no damage

#### Scenario: The boss turns
- **WHEN** the boss changes its aim after using the beam and before the warning has passed
- **THEN** the beam still follows the aim it had when it was used

#### Scenario: Mobs
- **WHEN** a mob stands on the line
- **THEN** the mob is not hurt

### Requirement: The roar summons mobs around the boss
When the warning of the roar has passed, it SHALL create a fixed number of mobs of the summoned kind around the place where it was used, within the same hard cap on mobs as every other way a mob is created. If the cap leaves room for only some of them, those SHALL be created, and if it leaves room for none, none SHALL be. The roar SHALL NOT cost summon energy and SHALL NOT be counted against the boss's summoning. The mobs SHALL behave and be shown like any other summoned mobs.

#### Scenario: A full roar
- **WHEN** the boss uses the roar with room for all of the mobs
- **THEN** that many mobs appear around the boss when the warning has passed

#### Scenario: Mob cap
- **WHEN** the mob cap leaves room for fewer mobs than the roar creates
- **THEN** only those that fit are created and the cooldown starts

#### Scenario: No energy used
- **WHEN** the boss uses the roar
- **THEN** the boss's summon energy is unchanged

### Requirement: Everyone sees the warnings of every effect
Every player SHALL see the warning of each skill effect when it is used: a ring for the shot ring, the shockwave (of the shockwave's radius) and the roar, and a line along the aim, of the beam's length and width, for the beam. When the beam takes effect every client SHALL show a short flash along the same line. The warnings SHALL be drawn from the skill event and the playing boss kind alone, with no extra message.

#### Scenario: A beam is seen
- **WHEN** the boss uses the beam
- **THEN** every client shows a line from the boss along its aim, of the beam's length, for the warning time, and a flash along it when the beam takes effect

#### Scenario: A roar is seen
- **WHEN** the boss uses the roar
- **THEN** every client shows a warning ring around the boss for the warning time
