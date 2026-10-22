# Spec Delta

## MODIFIED Requirements

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

## ADDED Requirements

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
