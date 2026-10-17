# character-attacks Specification

## Purpose

Defines the basic attack of each playable character as data: how often it fires, how many shots one attack makes and how they spread, what the shots look like in flight, how hard they hit and how many mobs one shot can pass through. The four characters use these to attack in four different ways with the same straight-flying projectiles.

## Requirements

### Requirement: Every character has its own basic attack
Each character SHALL define its basic attack in data: the time between attacks, the number of shots in one attack, the total spread angle of those shots, the projectile's speed, lifetime and radius, a damage multiplier, and how many mobs one projectile can hit. The server SHALL fire each survivor's attack from its character's data, and the data SHALL be changeable without changing the rules that use it.

#### Scenario: Different attacks
- **WHEN** survivors of two different characters hold fire for the same time
- **THEN** each fires according to its own character's interval, shot count, spread, speed and projectile size

#### Scenario: Changing a value
- **WHEN** a character's attack interval is changed in the data
- **THEN** that character fires at the new interval and no rule needs to change

### Requirement: The four characters attack in four ways
The characters SHALL differ in how they attack:
- Reimu fires one weak shot at a very short interval (rapid fire), with no tracking.
- Marisa fires one shot at a longer interval that passes through several mobs and hurts each.
- Sakuya fires several shots at once spread in a fan, with a short range.
- Sanae fires one large, slow shot that deals a heavy hit.
All shots SHALL fly in straight lines.

#### Scenario: Reimu
- **WHEN** a survivor playing Reimu holds fire for one second
- **THEN** Reimu fires more shots than any other character, each weaker than a common shot

#### Scenario: Marisa piercing
- **WHEN** a survivor playing Marisa fires toward three mobs standing in a line within range
- **THEN** the one shot damages each of the three mobs, nearest first

#### Scenario: Sakuya fan
- **WHEN** a survivor playing Sakuya fires once
- **THEN** several shots leave at once at different angles inside the spread, and their range is shorter than the other characters' shots

#### Scenario: Sanae large shot
- **WHEN** a survivor playing Sanae fires
- **THEN** the shot is larger and slower than the others and deals a larger hit, so it can touch a mob a small shot would miss

### Requirement: One attack makes all its shots together
One attack SHALL fire all of its shots at the same moment, spread evenly across the character's spread angle and centred on the aim. The cooldown SHALL start when the attack fires, whatever the number of shots. If the shot cap leaves room for only some of the shots, the shots that fit SHALL fire.

#### Scenario: Even spread
- **WHEN** a character fires five shots over a spread of 40 degrees aiming at 90 degrees
- **THEN** the shots leave at 70, 80, 90, 100 and 110 degrees

#### Scenario: Single shot ignores spread
- **WHEN** a character fires one shot per attack
- **THEN** the shot leaves exactly along the aim

#### Scenario: Cap reached
- **WHEN** an attack would exceed the projectile cap
- **THEN** only the shots that fit fire, and the cooldown starts if at least one fired

### Requirement: Damage uses the character's multiplier
A projectile's damage SHALL be the survivor's current projectile damage (including growth and passive skills) multiplied by the character's damage multiplier and rounded, with a minimum of 1. The projectile SHALL keep that damage for its whole flight.

#### Scenario: Multiplier applied
- **WHEN** a survivor with projectile damage 10 attacks with a multiplier of 1.4
- **THEN** each shot deals 14

#### Scenario: Growth still counts
- **WHEN** a survivor of any character gains damage from growth or a passive skill
- **THEN** that character's shots deal more, scaled by its multiplier

#### Scenario: Minimum damage
- **WHEN** the multiplied damage would round to zero
- **THEN** the shot deals 1

### Requirement: Passives work on every attack
The cooldown reduction from passive skills and from the attack speed skill SHALL shorten each character's attack interval in the same way, and the damage from growth and passive skills SHALL raise each character's shots, without changing the number of shots or the spread.

#### Scenario: A shorter interval
- **WHEN** a passive skill reduces a survivor's cooldown by 20 percent
- **THEN** that survivor attacks 25 percent more often with the same number of shots each time

### Requirement: Piercing shots hit each mob once
A projectile that can hit several mobs SHALL damage each mob it passes at most once, SHALL stop after its last allowed hit, and SHALL otherwise continue to the end of its lifetime or the arena edge. A projectile that can hit one mob SHALL stop at the first mob it touches.

#### Scenario: Not twice
- **WHEN** a piercing shot overlaps the same mob over several simulation steps
- **THEN** that mob takes damage from the shot once

#### Scenario: Last allowed hit
- **WHEN** a shot that can hit three mobs has hit three mobs
- **THEN** it stops at the third and cannot hit a fourth

#### Scenario: Fewer mobs than hits
- **WHEN** a shot that can hit three mobs passes only one
- **THEN** it continues flying past that mob until its lifetime or the arena edge

### Requirement: Basic attacks keep within the projectile cap and traffic budget
The total number of projectiles SHALL stay within the cap with four survivors attacking, and the combat traffic to each client SHALL stay within its budget for any mix of the four characters.

#### Scenario: Four rapid attackers
- **WHEN** four survivors of the four characters hold fire for one minute with 100 mobs alive
- **THEN** the projectile count never exceeds the cap and combat traffic to each client averages no more than 10 KB per second
