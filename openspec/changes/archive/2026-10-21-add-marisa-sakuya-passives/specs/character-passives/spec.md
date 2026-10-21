# Spec Delta

## MODIFIED Requirements

### Requirement: Every character has one passive of its own
Besides the common passives, each of the four characters SHALL have at least one passive skill that only that character can be offered at a level up. The first passives SHALL be: for the first character a reduction of its skills' mana cost, for the second an increase of its skills' damage and healing, for the third a reduction of its skills' cooldown, and for the fourth an increase of its mana recovery. Every character SHALL also have a second passive of its own that changes the size of its skills: the first character's lengthens the duration of its timed skill, the second character's widens the range of its beam, the third character's lengthens the duration of its time-stopping skill, and the fourth character's widens the range of its skills. Each SHALL have a maximum rank of three and SHALL be defined as data with the effect it has on each rank, so its values can change without changing rules elsewhere.

#### Scenario: Offers differ by character
- **WHEN** survivors of different characters level up with the same skills learned
- **THEN** each can be offered only the common passives and its own character's passives, and never another character's

#### Scenario: A passive at its maximum
- **WHEN** a survivor's own passive has reached rank three
- **THEN** it is not offered again

#### Scenario: Every character is covered
- **WHEN** the characters are listed
- **THEN** each has exactly two passives of its own and no passive belongs to two characters

### Requirement: A character passive is offered after the skill it helps
A character's own passive SHALL NOT be offered until the skill it helps has reached rank one, using the existing rule that a skill may require another skill at a rank. The first four passives SHALL need the character's Q skill. The duration passive of the first character SHALL need its timed E skill, the range passive of the fourth character SHALL need its E skill that has a range, the range passive of the second character SHALL need its Q skill (the beam), and the duration passive of the third character SHALL need its Q skill (the time stop).

#### Scenario: Before the Q skill
- **WHEN** a survivor has not yet learned the skill a passive helps
- **THEN** that passive is not in the offer

#### Scenario: After the Q skill
- **WHEN** a survivor has learned the skill a passive helps and has levels to spend
- **THEN** that passive can be in the offer

## ADDED Requirements

### Requirement: Beam passive widens the beam
The second character's range passive SHALL raise the range scale of the skills of that character by a fixed percentage per rank, so the beam reaches farther and is wider, and every client SHALL see a beam of the larger size. It SHALL NOT change the duration of the character's timed skill.

#### Scenario: Longer and wider beam
- **WHEN** the survivor with the passive at rank three uses the beam skill
- **THEN** mobs on the line within the defined length multiplied by the scale and within the defined width multiplied by the scale are hit, and every client draws a beam of that length and width

#### Scenario: Not learned
- **WHEN** the survivor has not learned the passive
- **THEN** the beam has its defined length and width

#### Scenario: The other skill is unchanged
- **WHEN** the survivor uses the attack speed skill with the passive learned
- **THEN** the attack speed boost lasts its defined time

### Requirement: Time passive lengthens the time stop
The third character's duration passive SHALL raise the duration scale of the skills of that character by a fixed percentage per rank, so the time stop lasts longer on every client. It SHALL NOT change the range or the count of the character's other skill.

#### Scenario: Longer stop
- **WHEN** the survivor with the passive at rank three uses the time-stopping skill
- **THEN** mobs stay stopped for the defined duration multiplied by the scale for rank three

#### Scenario: Not learned
- **WHEN** the survivor has not learned the passive
- **THEN** the time stop lasts its defined duration

#### Scenario: The other skill is unchanged
- **WHEN** the survivor uses the knife skill with the passive learned
- **THEN** it fires its defined count of knives
