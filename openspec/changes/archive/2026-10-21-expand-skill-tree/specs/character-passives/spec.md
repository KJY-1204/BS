# Spec Delta

## MODIFIED Requirements

### Requirement: Every character has one passive of its own
Besides the common passives, each of the four characters SHALL have four passives that only that character can be offered at a level up. The first passives SHALL be: for the first character a reduction of its skills' mana cost, for the second an increase of its skills' damage and healing, for the third a reduction of its skills' cooldown, and for the fourth an increase of its mana recovery. Every character SHALL also have a second passive of its own that changes the size of its skills: the first character's lengthens the duration of its timed skill, the second character's widens the range of its beam, the third character's lengthens the duration of its time-stopping skill, and the fourth character's widens the range of its skills. Every character SHALL have a third and a fourth passive of its own: the first character's reduce the damage it takes and raise the damage and healing of its skills, the second character's raise the damage of its basic attack and its mana recovery, the third character's raise the damage of its skills and reduce their mana cost, and the fourth character's raise the experience it receives and the damage and healing of its skills. Each SHALL have a maximum rank of three and SHALL be defined as data with the effect it has on each rank, so its values can change without changing rules elsewhere.

#### Scenario: Offers differ by character
- **WHEN** survivors of different characters level up with the same skills learned
- **THEN** each can be offered only the common passives and its own character's passives, and never another character's

#### Scenario: A passive at its maximum
- **WHEN** a survivor's own passive has reached rank three
- **THEN** it is not offered again

#### Scenario: Every character is covered
- **WHEN** the characters are listed
- **THEN** each has exactly four passives of its own and no passive belongs to two characters

#### Scenario: Enough to choose from
- **WHEN** the ranks a character can learn are added up (common passives, its two active skills and its own passives)
- **THEN** the total is larger than the number of choices a survivor gets from the first level to the level cap

### Requirement: A character passive is offered after the skill it helps
A character's own passive SHALL NOT be offered until the skill it needs has reached the rank it requires, using the existing rule that a skill may require another skill at a rank. The first four passives SHALL need the character's Q skill at rank one. The duration passive of the first character SHALL need its timed E skill, the range passive of the fourth character SHALL need its E skill that has a range, the range passive of the second character SHALL need its Q skill (the beam), and the duration passive of the third character SHALL need its Q skill (the time stop). Each character's third passive SHALL need its E skill at rank one, and each character's fourth passive SHALL need its Q skill at rank three.

#### Scenario: Before the Q skill
- **WHEN** a survivor has not yet learned the skill a passive helps
- **THEN** that passive is not in the offer

#### Scenario: After the Q skill
- **WHEN** a survivor has learned the skill a passive helps and has levels to spend
- **THEN** that passive can be in the offer

#### Scenario: The E skill opens a passive
- **WHEN** a survivor has learned its E skill at rank one and not yet its Q skill at rank three
- **THEN** its third passive can be offered and its fourth passive is not in the offer

#### Scenario: The Q skill at rank three opens the top passive
- **WHEN** a survivor has its Q skill at rank two
- **THEN** its fourth passive is not in the offer, and at rank three it can be

## ADDED Requirements

### Requirement: Guard reduces the damage a survivor takes
The damage-reduction passive SHALL lower every hit its owner takes by a fixed fraction per rank, with a cap on the total reduction, and a hit that would deal damage SHALL still deal at least one. The server SHALL apply it where damage reaches the survivor, so contact, projectiles and blasts are all reduced the same way, and it SHALL NOT make a protected or fallen survivor take damage.

#### Scenario: A reduced hit
- **WHEN** a survivor with the passive at a rank takes a hit of a given damage
- **THEN** the health falls by the damage reduced by that rank's fraction

#### Scenario: A hit of one
- **WHEN** the reduction would take a hit of damage one down to zero
- **THEN** the survivor still loses one health

#### Scenario: Other survivors
- **WHEN** a survivor without the passive takes the same hit
- **THEN** the health falls by the full damage

### Requirement: Basic attack passive raises basic attack damage
The basic attack passive of the second character SHALL raise the damage of its owner's basic attack by a fixed fraction per rank, in the same stat calculation that levels and the common power passive use. It SHALL NOT change the damage of the active skills.

#### Scenario: A stronger basic shot
- **WHEN** a survivor with the passive at a rank fires a basic shot
- **THEN** the shot's damage is the damage from levels and common passives raised by that rank's fraction

#### Scenario: Skills are unchanged
- **WHEN** the same survivor fires an active skill
- **THEN** the skill's damage is not changed by this passive

### Requirement: Experience passive raises the experience a survivor receives
The experience passive of the fourth character SHALL raise the experience its owner receives from each kill by a fixed fraction per rank, rounded to a whole number and never below the base amount. Other recipients of the same kill SHALL receive their own amount, and the passive SHALL NOT change what a kill is worth to anyone else.

#### Scenario: More experience
- **WHEN** a mob dies near a survivor with the passive at a rank and a survivor without it
- **THEN** the first receives the base amount raised by that rank's fraction and the second receives the base amount

#### Scenario: At the level cap
- **WHEN** a survivor at the level cap receives experience
- **THEN** nothing changes, as without the passive
