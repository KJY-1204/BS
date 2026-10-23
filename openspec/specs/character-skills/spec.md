# character-skills Specification

## Purpose

Defines which two active skills each character owns on the Q and E keys, and what each of the eight skills does when the server fires it: area damage, a short invulnerable barrier, a straight beam, a firing-speed boost, a stop of all mobs, a ring of knives, healing and pushing mobs away.

## Requirements

### Requirement: Each character owns a Q skill and an E skill
Every character SHALL have exactly one active skill for the Q slot and one for the E slot, and the two keys SHALL fire the skills of the survivor's own character. A skill SHALL only be learnable and usable by the character that owns it. The passive skills SHALL stay common to every character.

#### Scenario: Slots follow the character
- **WHEN** a survivor of one character and a survivor of another press Q
- **THEN** each fires the Q skill of their own character

#### Scenario: Another character's skill
- **WHEN** a request names a skill that belongs to another character
- **THEN** the server ignores it and nothing happens

#### Scenario: Slot display
- **WHEN** a survivor looks at the skill slots
- **THEN** each slot shows the name of that character's own skill, learned or not

### Requirement: The characters' skills
The eight Q and E skills SHALL be named for their characters as listed in the scenario below. Each skill SHALL have a mana cost, a cooldown and effect values that grow with its rank, and all of them SHALL be data a designer can change. The R skills of the characters are defined apart from these eight.

#### Scenario: The assignment
- **WHEN** the skills are listed by character
- **THEN** Reimu has an area burst (Q) and a barrier (E), Marisa has a beam (Q) and an attack speed boost (E), Sakuya has a time stop (Q) and a ring of knives (E), and Sanae has a heal (Q) and a push (E)

#### Scenario: Eight skills in data
- **WHEN** the Q and E skills are listed
- **THEN** the four characters own two each, with different identifiers, and each has a cost, a cooldown and values for each rank

### Requirement: Area burst hurts mobs around the caster
The Reimu Q skill SHALL damage every mob within its radius of her, with damage and radius growing with rank, and mobs it kills SHALL give experience like any kill.

#### Scenario: Mobs inside and outside
- **WHEN** the skill fires with three mobs inside the radius and one outside
- **THEN** the three take damage and the one outside does not

### Requirement: The barrier makes the caster invulnerable for a time
The Reimu E skill SHALL make her take no damage for its duration, which grows with rank and with her duration passive, and every client SHALL see that she is protected for that time. The protection SHALL end by itself, and SHALL end if she is already dead.

#### Scenario: Touched by mobs
- **WHEN** a survivor under the barrier is touched by mobs for the whole duration
- **THEN** her health does not drop

#### Scenario: Barrier ends
- **WHEN** the duration has passed
- **THEN** mobs can hurt her again and the protection mark is gone on every client

#### Scenario: Lengthened by the passive
- **WHEN** she uses the barrier with her duration passive learned
- **THEN** the protection and the mark last the defined duration multiplied by the duration scale

### Requirement: The beam hits every mob on a line
The Marisa Q skill SHALL damage every mob on a straight line from her along the aim she had when the server fired it, up to a length and within a width that grow with rank and with her range passive, and SHALL NOT stop at the first mob. The line SHALL stop at the arena edge. Mobs it kills SHALL give experience.

#### Scenario: Mobs in a row
- **WHEN** the beam fires toward four mobs in a row inside its length and one mob to the side outside its width
- **THEN** the four take damage and the one to the side does not

#### Scenario: Beyond the length
- **WHEN** a mob stands on the line farther than the beam's length
- **THEN** it takes no damage

#### Scenario: Widened by the passive
- **WHEN** a mob stands beyond the defined length or beside the defined width but inside the length or width multiplied by the range scale
- **THEN** it takes damage

### Requirement: Stardust speeds up the attack for a time
The Marisa E skill SHALL shorten her attack interval for a time, with the speed-up and duration growing with rank, and the effect SHALL end by itself or when she dies.

#### Scenario: During and after
- **WHEN** the skill is active and then ends
- **THEN** she attacks faster during it and at the normal rate afterwards

### Requirement: Time stop freezes every mob
The Sakuya Q skill SHALL stop every mob for a time that grows with rank and with her duration passive: frozen mobs do not move, new mobs do not appear, and frozen mobs do not hurt survivors on contact. Frozen mobs SHALL still take damage from shots and skills, and SHALL move again when the time ends. Using the skill again while it is active SHALL NOT shorten it.

#### Scenario: Mobs stand still
- **WHEN** the skill fires with mobs chasing survivors
- **THEN** no mob moves and none appears for the whole duration, on every client

#### Scenario: Harmless on contact
- **WHEN** a survivor stands inside frozen mobs
- **THEN** the survivor takes no contact damage

#### Scenario: Damage still works
- **WHEN** a shot hits a frozen mob
- **THEN** the mob takes damage and can die

#### Scenario: Time resumes
- **WHEN** the duration has passed
- **THEN** mobs chase again and new mobs appear again

#### Scenario: Lengthened by the passive
- **WHEN** she uses the skill with her duration passive learned
- **THEN** mobs stay stopped for the defined duration multiplied by the duration scale

### Requirement: Knife barrage fires a ring of knives
The Sakuya E skill SHALL fire knives evenly around her in all directions, with the number and the damage per knife growing with rank. The knives SHALL be ordinary projectiles with the shape of her basic shots, so they fly straight and are shown to every client as shots.

#### Scenario: Ring
- **WHEN** the skill fires at rank one with twelve knives
- **THEN** twelve projectiles leave her at equal angle steps of 30 degrees

#### Scenario: Cap reached
- **WHEN** the projectile cap leaves room for fewer knives than the rank gives
- **THEN** the knives that fit fire and the skill still costs mana and starts its cooldown

### Requirement: Miracle heals the caster and nearby survivors
The Sanae Q skill SHALL restore health to her and to every living survivor within its radius, which grows with her range passive, by an amount that grows with rank, never above a survivor's maximum health. It SHALL NOT bring a dead survivor back.

#### Scenario: Heal in range
- **WHEN** the skill fires with a wounded ally inside the radius and another outside
- **THEN** the ally inside and Sanae are healed and the one outside is not

#### Scenario: Maximum
- **WHEN** a heal would take health above the maximum
- **THEN** health stops at the maximum

#### Scenario: A fallen ally
- **WHEN** a dead survivor stands inside the radius
- **THEN** the dead survivor stays dead

#### Scenario: Widened by the passive
- **WHEN** an ally stands beyond the defined radius but inside the radius multiplied by the range scale
- **THEN** the ally is healed

### Requirement: Wind Veil pushes mobs away
The Sanae E skill SHALL push every mob within its radius directly away from her by a distance that grows with rank, without leaving the arena and without damaging the mobs. The radius and the distance SHALL grow with her range passive.

#### Scenario: Pushed out
- **WHEN** the skill fires with a mob inside the radius
- **THEN** the mob ends up farther from her along the same direction by the push distance, or at the arena edge if that is nearer

#### Scenario: Outside the radius
- **WHEN** a mob stands outside the radius
- **THEN** it does not move

#### Scenario: Widened by the passive
- **WHEN** she uses the skill with her range passive learned
- **THEN** the radius and the push distance are the defined values multiplied by the range scale

### Requirement: Skills follow the existing rules
All eight skills SHALL be fired only when the survivor is alive, the skill is learned, it is off cooldown and the survivor has enough mana, with the owner told the result, as for every skill. Every client SHALL show each skill's effect at the place, direction and moment the server fired it, on the display clock used for mobs and shots.

#### Scenario: Refused
- **WHEN** a survivor presses a skill key without enough mana
- **THEN** the skill does not fire and no mana is spent

#### Scenario: A teammate's beam
- **WHEN** a teammate fires the beam
- **THEN** every client shows a beam from the teammate along the same direction at the display moment, and the mobs it killed disappear at that moment

### Requirement: Skills keep within the combat traffic budget
Skill events SHALL stay small. Four survivors using their skills whenever ready, together with their basic attacks, SHALL NOT take the combat traffic above its budget.

#### Scenario: Heavy use
- **WHEN** four survivors of the four characters use their skills whenever ready for one minute with 100 mobs alive
- **THEN** combat traffic to each client averages no more than 10 KB per second
