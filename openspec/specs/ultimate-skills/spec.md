# ultimate-skills Specification

## Purpose

Defines the third active skill slot, the R key, and the four R skills, one per character: how a survivor earns one, what each does when the server fires it, and how it is shown, so that the growth has a rare, strong skill to build toward.

## Requirements

### Requirement: Each character owns an R skill on a third slot
Every character SHALL have exactly one active skill for the R slot besides its Q and E skills, and the R key SHALL fire the R skill of the survivor's own character. A skill of the R slot SHALL only be learnable and usable by the character that owns it, and SHALL have three ranks like the other active skills.

#### Scenario: Slots follow the character
- **WHEN** a survivor of one character and a survivor of another press R after learning their R skills
- **THEN** each fires the R skill of their own character

#### Scenario: Another character's R skill
- **WHEN** a request names an R skill that belongs to another character
- **THEN** the server ignores it and nothing happens

#### Scenario: Unlearned slot
- **WHEN** a survivor who has not learned the R skill presses R
- **THEN** nothing happens and no mana is spent

### Requirement: An R skill is earned once the Q skill has reached rank two
An R skill SHALL NOT be offered on a level up until the survivor's own Q skill has reached rank 2, and from then on it SHALL be offered like any other skill, one of up to three choices, until it reaches its maximum rank. It SHALL NOT be given automatically.

#### Scenario: Before the Q skill is strong enough
- **WHEN** a survivor whose Q skill is at rank 1 levels up
- **THEN** the offered choices never include the R skill

#### Scenario: After the prerequisite
- **WHEN** a survivor whose Q skill is at rank 2 levels up with enough other skills available
- **THEN** the R skill can be among the offered choices, and choosing it gives rank 1

#### Scenario: Maximum rank
- **WHEN** the R skill has reached rank 3
- **THEN** it is not offered again

### Requirement: The R skills are stronger and rarer than the Q and E skills
Each R skill SHALL cost more mana and have a longer cooldown than either of the base Q and E skills of its character, and SHALL reach farther or hit harder than the base Q or E skill with the same kind of effect at the same rank. The evolutions of the Q, E and R skills are not part of this comparison. All of their values SHALL be data a designer can change.

#### Scenario: Cost and cooldown
- **WHEN** a character's three base active skills are compared
- **THEN** the R skill has the highest mana cost and the longest cooldown of the three

#### Scenario: Data
- **WHEN** the four R skills are listed
- **THEN** each has a cost, a cooldown and values for each of its three ranks, with identifiers different from every other skill

### Requirement: Reimu's R skill is a large area burst
Reimu's R skill SHALL damage every mob within a radius of her, and the boss when its body overlaps that radius, with a radius and damage that grow with rank and are larger than her Q skill's at the same rank. Mobs it kills SHALL give experience.

#### Scenario: Mobs inside and outside
- **WHEN** the skill fires with mobs inside a distance only this skill reaches and mobs beyond it
- **THEN** the mobs inside take damage and the ones beyond do not

#### Scenario: Harder than the Q skill
- **WHEN** a mob stands inside the reach of both her Q skill and her R skill
- **THEN** it takes more damage from the R skill

### Requirement: Marisa's R skill is a long, wide beam
Marisa's R skill SHALL damage every mob on a straight line along the aim she had when the server fired it, and the boss when its body overlaps the line, up to a length and within a width that grow with rank and are larger than her Q skill's. It SHALL NOT stop at the first mob.

#### Scenario: Wider than the Q beam
- **WHEN** a mob stands beside the line at a distance inside this beam's width and outside her Q beam's width
- **THEN** it takes damage from the R skill only

### Requirement: Sakuya's R skill is a large ring of knives
Sakuya's R skill SHALL fire a ring of knives from her, evenly spread from her aim, in the shape of her basic shots, with more knives and more damage each than her E skill at the same rank, growing with rank. The knives SHALL fly and hit like any projectile of hers.

#### Scenario: More knives
- **WHEN** her R skill fires at rank 1
- **THEN** it fires more knives than her E skill does at rank 1, evenly spread

### Requirement: Sanae's R skill is a wide, strong heal
Sanae's R skill SHALL heal her and every living survivor within a radius of her by an amount that grows with rank, with a larger radius and amount than her Q skill at the same rank. A fallen survivor SHALL NOT be revived or healed by it.

#### Scenario: Far survivors
- **WHEN** a survivor stands inside this skill's radius and outside her Q skill's radius
- **THEN** the R skill heals them and the Q skill does not

#### Scenario: A fallen survivor
- **WHEN** a fallen survivor is inside the radius
- **THEN** they are not healed and stay fallen

### Requirement: An R skill follows the rules of the other skills
The server SHALL fire an R skill only when the survivor is alive, has learned it, it is off cooldown and the survivor has the mana its cost needs as the character's passives change it, and SHALL tell the owner the result and the cooldown. The character's own passives for cost, power, cooldown, range and duration SHALL apply to it like to the Q and E skills, and the owner's skill display SHALL show the cost and cooldown that apply.

#### Scenario: Refused
- **WHEN** the survivor presses R without enough mana, or during the cooldown
- **THEN** the server refuses, the owner sees why, and neither mana nor cooldown changes

#### Scenario: Passives apply
- **WHEN** Marisa has learned her power and range passives and fires her R skill
- **THEN** the damage is raised by the power passive and the beam is longer and wider by the range scale, the same as for her Q skill

#### Scenario: Cooldowns are separate
- **WHEN** a survivor fires R and then presses Q and E during the R cooldown
- **THEN** the Q and E skills fire if they are off their own cooldowns

### Requirement: Everyone sees an R skill and the skill bar shows it
Every client SHALL show an R skill's effect at the place and moment the server fired it, as the same kind of effect as the Q and E skills show, only larger. The owner's skill bar SHALL show three slots, Q, E and R, each with the skill's name, rank, cooldown and whether the mana is short, and an unlearned R slot SHALL show as not learned. Firing an R skill SHALL NOT take the combat traffic above its budget.

#### Scenario: Seen by others
- **WHEN** a survivor fires an R skill
- **THEN** every other client shows its effect at the same place and time

#### Scenario: Skill bar
- **WHEN** a survivor who has learned the R skill looks at the skill bar
- **THEN** a third slot labelled R shows its name and rank, and the cooldown counting down after use

#### Scenario: The biggest burst of projectiles
- **WHEN** four survivors fire their R skills at the same moment, Sakuya's ring of knives included
- **THEN** the combat events fit in the traffic budget and no skill event is lost
