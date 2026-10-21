# Spec Delta

## MODIFIED Requirements

### Requirement: Every character has one passive of its own
Besides the common passives, each of the four characters SHALL have at least one passive skill that only that character can be offered at a level up. The first passives SHALL be: for the first character a reduction of its skills' mana cost, for the second an increase of its skills' damage and healing, for the third a reduction of its skills' cooldown, and for the fourth an increase of its mana recovery. The first character SHALL also have a passive that lengthens the duration of its timed skill, and the fourth character SHALL also have a passive that widens the range of its skills. Each SHALL have a maximum rank of three and SHALL be defined as data with the effect it has on each rank, so its values can change without changing rules elsewhere.

#### Scenario: Offers differ by character
- **WHEN** survivors of different characters level up with the same skills learned
- **THEN** each can be offered only the common passives and its own character's passives, and never another character's

#### Scenario: A passive at its maximum
- **WHEN** a survivor's own passive has reached rank three
- **THEN** it is not offered again

#### Scenario: Every character is covered
- **WHEN** the characters are listed
- **THEN** each has at least one passive of its own and no passive belongs to two characters

### Requirement: A character passive is offered after the skill it helps
A character's own passive SHALL NOT be offered until the skill it helps has reached rank one, using the existing rule that a skill may require another skill at a rank. The first four passives SHALL need the character's Q skill. The duration passive SHALL need the character's timed E skill, and the range passive SHALL need the character's E skill that has a range.

#### Scenario: Before the Q skill
- **WHEN** a survivor has not yet learned the skill a passive helps
- **THEN** that passive is not in the offer

#### Scenario: After the Q skill
- **WHEN** a survivor has learned the skill a passive helps and has levels to spend
- **THEN** that passive can be in the offer

## ADDED Requirements

### Requirement: Duration passive lengthens the protection
The first character's duration passive SHALL raise the duration scale of the skills of that character by a fixed percentage per rank, so the protection of her barrier lasts longer, and every client SHALL see the protection mark for the longer time.

#### Scenario: Longer barrier
- **WHEN** the survivor with the passive at rank two uses the barrier skill whose defined duration is two seconds
- **THEN** the survivor takes no damage for the defined duration multiplied by the scale for rank two, and every client shows the protection for that time

#### Scenario: Not learned
- **WHEN** the survivor has not learned the passive
- **THEN** the barrier lasts its defined duration

#### Scenario: Other characters
- **WHEN** a survivor of another character uses a timed skill
- **THEN** the passive of the first character does not change it

### Requirement: Range passive widens the range of skills
The fourth character's range passive SHALL raise the range scale of the skills of that character by a fixed percentage per rank, so the heal radius, the push radius and the push distance grow, and every client SHALL see rings of the larger size.

#### Scenario: Larger heal
- **WHEN** the survivor with the passive at rank three uses its heal skill
- **THEN** allies within the defined radius multiplied by the scale for rank three are healed and every client draws a ring of that radius

#### Scenario: Farther push
- **WHEN** the survivor with the passive uses its push skill
- **THEN** mobs inside the larger radius are pushed by the defined distance multiplied by the scale

#### Scenario: Not learned
- **WHEN** the survivor has not learned the passive
- **THEN** the skills have their defined range
