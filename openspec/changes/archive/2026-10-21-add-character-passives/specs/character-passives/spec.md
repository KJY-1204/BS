# Spec Delta

## Purpose

Defines the passive skill each character has for itself on top of the common passives: which passive belongs to whom, when it is offered at a level up, and what it does to the survivor's skills and mana.

## ADDED Requirements

### Requirement: Every character has one passive of its own
Besides the common passives, each of the four characters SHALL have exactly one passive skill that only that character can be offered at a level up. The four passives SHALL be: for the first character a reduction of its skills' mana cost, for the second an increase of its skills' damage and healing, for the third a reduction of its skills' cooldown, and for the fourth an increase of its mana recovery. Each SHALL have a maximum rank of three and SHALL be defined as data with the effect it has on each rank, so its values can change without changing rules elsewhere.

#### Scenario: Offers differ by character
- **WHEN** survivors of different characters level up with the same skills learned
- **THEN** each can be offered only the common passives and its own character's passive, and never another character's

#### Scenario: A passive at its maximum
- **WHEN** a survivor's own passive has reached rank three
- **THEN** it is not offered again

#### Scenario: Every character is covered
- **WHEN** the characters are listed
- **THEN** each has exactly one passive of its own and no two share one

### Requirement: A character passive is offered after the skill it helps
A character's own passive SHALL NOT be offered until that character's Q skill has reached rank one, using the existing rule that a skill may require another skill at a rank.

#### Scenario: Before the Q skill
- **WHEN** a survivor has not yet learned its Q skill
- **THEN** its own passive is not in the offer

#### Scenario: After the Q skill
- **WHEN** a survivor has learned its Q skill and has levels to spend
- **THEN** its own passive can be in the offer

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
