# character-passives Specification

## Purpose

Defines the passive skill each character has for itself on top of the common passives: which passive belongs to whom, when it is offered at a level up, and what it does to the survivor's skills and mana.

## Requirements

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

### Requirement: Cost reduction lowers the mana cost of skills
The mana-cost passive SHALL lower the mana cost of each of its owner's active skills by a fixed fraction per rank, with the cost never going below one. The server SHALL decide whether the survivor has enough mana using the lowered cost, and SHALL spend the lowered cost.

#### Scenario: Cheaper skill
- **WHEN** a survivor with the passive at a rank fires a skill
- **THEN** the mana spent is the skill's cost reduced by that rank's fraction

#### Scenario: Enough mana only with the passive
- **WHEN** a survivor has mana between the lowered cost and the full cost
- **THEN** the skill fires, which it would not without the passive

#### Scenario: A lower bound
- **WHEN** the reduction would take a cost below one
- **THEN** the cost is one

### Requirement: Power raises the damage and healing of skills
The power passive SHALL raise the damage and the healing amount of its owner's active skills by a fixed fraction per rank. The server SHALL apply it when the skill is fired, so a skill already released keeps its damage. A skill with neither damage nor healing SHALL be unaffected.

#### Scenario: A stronger skill
- **WHEN** a survivor with the passive fires a skill that deals damage
- **THEN** the damage is the skill's damage at its rank raised by the passive's fraction

#### Scenario: A skill with no damage
- **WHEN** the survivor fires a skill that deals no damage and heals nobody
- **THEN** the skill is unchanged

#### Scenario: Other survivors
- **WHEN** a survivor without the passive fires a skill
- **THEN** its damage is unchanged

### Requirement: Time sense shortens the cooldown of skills
The cooldown passive SHALL shorten the cooldown of each of its owner's active skills by a fixed fraction per rank, with a lower bound so a cooldown never becomes very short. The server SHALL apply it when the cooldown starts, so a running cooldown is not changed, and the owner's screen SHALL show the shortened cooldown.

#### Scenario: A shorter cooldown
- **WHEN** a survivor with the passive fires a skill
- **THEN** the cooldown that starts is the skill's cooldown shortened by that rank's fraction

#### Scenario: A running cooldown
- **WHEN** the survivor picks the passive while a skill is cooling down
- **THEN** that cooldown is not shortened, and the next one is

#### Scenario: A lower bound
- **WHEN** the shortening would make a cooldown shorter than the bound
- **THEN** the cooldown is the bound

### Requirement: Faith raises mana recovery
The mana-recovery passive SHALL raise its owner's mana recovery per second by a fixed amount per rank, in the same stat calculation that the common passives and levels use. It SHALL NOT restore current mana by itself.

#### Scenario: Faster recovery
- **WHEN** a survivor with the passive at a rank recovers mana
- **THEN** the recovery per second is higher by that rank's amount

#### Scenario: No refill
- **WHEN** the survivor picks the passive with mana below its maximum
- **THEN** current mana is unchanged at that moment

### Requirement: The owner's skill display shows the real values
The owner's skill slots SHALL show the mana cost and the cooldown that apply with the passives the owner has, so what the screen says matches what the server does. Other players SHALL NOT receive the owner's passive ranks, and the effects SHALL NOT need any new network message.

#### Scenario: A lowered cost on the slot
- **WHEN** the owner's cost passive lowers a skill's cost
- **THEN** the slot's lack-of-mana indication follows the lowered cost

#### Scenario: A shortened cooldown on the slot
- **WHEN** the owner's cooldown passive shortens a skill's cooldown
- **THEN** the slot's cooldown bar runs over the shortened time

#### Scenario: Privacy
- **WHEN** a survivor learns its own passive
- **THEN** only that survivor's client learns the rank

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
