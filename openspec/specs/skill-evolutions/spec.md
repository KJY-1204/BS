# skill-evolutions Specification

## Purpose

Defines the evolutions of the Q skills: a choice at the top of a skill that changes what the skill does, how it is offered and made final, how it replaces the Q skill, and how it is shown, so that survivors of one character can play differently.

## Requirements

### Requirement: Each Q skill has two evolutions
Each character's Q skill SHALL have two evolutions, each a skill of its own with a name, one rank, a mana cost, a cooldown and effect values defined as data, of the same kind of effect as the base skill. An evolution SHALL be offered on a level up only after the survivor's Q skill has reached its maximum rank, in the same choice of up to three as any other skill.

#### Scenario: Before the maximum rank
- **WHEN** a survivor whose Q skill is below its maximum rank levels up
- **THEN** the offered choices never include an evolution of it

#### Scenario: At the maximum rank
- **WHEN** a survivor whose Q skill is at its maximum rank levels up with enough other skills available
- **THEN** either evolution of it can be among the offered choices

#### Scenario: Another character
- **WHEN** a survivor levels up
- **THEN** no offered evolution belongs to a Q skill of a different character

### Requirement: The two evolutions of a skill exclude each other
Once a survivor has learned one evolution of a Q skill, the other evolution of it SHALL NOT be offered again, and the evolution SHALL NOT be offered again either. The choice SHALL be final for the rest of the match.

#### Scenario: One chosen
- **WHEN** a survivor chooses one evolution and later levels up many times
- **THEN** neither evolution of that skill is ever offered again

#### Scenario: Neither chosen
- **WHEN** a survivor has not chosen an evolution
- **THEN** both stay available whenever the Q skill is at its maximum rank

### Requirement: An evolution replaces the Q skill
A survivor who has learned an evolution SHALL fire it instead of the base skill when pressing Q, with the evolution's own mana cost, cooldown and effect values, under the same server rules as every skill (alive, learned, off cooldown, enough mana). The base skill SHALL keep its rank, so a skill that needs the Q skill at a rank still has it.

#### Scenario: Pressing Q
- **WHEN** a survivor with an evolution presses Q
- **THEN** the evolution fires with its own cost, cooldown and values and the base skill does not

#### Scenario: The R skill still opens
- **WHEN** a survivor has evolved the Q skill and levels up
- **THEN** the R skill that needs the Q skill at rank two is still available to be offered

#### Scenario: Passives
- **WHEN** the survivor has the character's cost, power, cooldown, range or duration passives
- **THEN** they apply to the evolution like to the base skill

### Requirement: An evolution is a trade, not an upgrade
An evolution SHALL be worse than the base skill at its maximum rank in at least one of its mana cost, cooldown or effect values, and the two evolutions of a skill SHALL differ from each other in at least one of them. All their values SHALL be data a designer can change.

#### Scenario: Not strictly better
- **WHEN** an evolution is compared with its base skill at the maximum rank
- **THEN** at least one of its cost, cooldown or effect values is worse

#### Scenario: Two different evolutions
- **WHEN** the two evolutions of a skill are compared
- **THEN** they differ in at least one cost, cooldown or effect value, and have different names and numbers

### Requirement: Everyone sees an evolved skill and the owner's screen shows it
Every client SHALL show an evolved skill's effect at the place and moment the server fired it, as the effect of the evolution. The owner's skill bar SHALL show the evolution's name on the Q slot with the cost and cooldown that apply, and the level-up choices SHALL show an evolution by name with a short description of what it changes.

#### Scenario: Seen by others
- **WHEN** a survivor fires an evolved Q skill
- **THEN** every other client shows its effect with the evolution's size, at the same place and time

#### Scenario: Skill bar
- **WHEN** a survivor who has learned an evolution looks at the skill bar
- **THEN** the Q slot shows the evolution's name, and the cooldown counts down after use

#### Scenario: The choice text
- **WHEN** an evolution is among the offered choices
- **THEN** it is shown by its name with a description of its trade

### Requirement: The E and R skills have evolutions too
Each character's E skill and R skill SHALL have two evolutions, each a skill of its own with a name, one rank, a mana cost, a cooldown and effect values defined as data, of the same kind of effect as its base skill. An evolution SHALL be offered on a level up only after its base skill has reached its maximum rank, in the same choice as any other skill.

#### Scenario: An E evolution is offered
- **WHEN** a survivor whose E skill is at its maximum rank levels up with enough other skills available
- **THEN** either evolution of the E skill can be among the offered choices, and none is offered before the E skill is at its maximum rank

#### Scenario: An R evolution is offered
- **WHEN** a survivor whose R skill is at its maximum rank levels up with enough other skills available
- **THEN** either evolution of the R skill can be among the offered choices, and none is offered before the R skill is at its maximum rank

#### Scenario: The slots are independent
- **WHEN** a survivor has chosen an evolution of the Q skill
- **THEN** the evolutions of its E skill and R skill are still offered when those skills are at their maximum rank

### Requirement: The rules of the Q evolutions hold for the E and R evolutions
The two evolutions of an E or R skill SHALL exclude each other and the choice SHALL be final. A learned evolution SHALL be fired in place of its base skill on the base skill's own key, with its own mana cost, cooldown and values under the same server rules as every skill, the base skill keeping its rank, and the character's passives SHALL apply to it. It SHALL be worse than its base skill at its maximum rank in at least one of its cost, cooldown or effect values.

#### Scenario: Pressing E or R
- **WHEN** a survivor with an evolution of the E skill presses E, or with an evolution of the R skill presses R
- **THEN** the evolution fires with its own cost, cooldown and values and the base skill does not

#### Scenario: One chosen
- **WHEN** a survivor chooses one evolution of its E skill and later levels up many times
- **THEN** neither evolution of the E skill is offered again

#### Scenario: Not strictly better
- **WHEN** an evolution of an E or R skill is compared with its base skill at the maximum rank
- **THEN** at least one of its cost, cooldown or effect values is worse

### Requirement: An R evolution is still an ultimate
Each evolution of an R skill SHALL cost more mana and have a longer cooldown than every base Q skill and E skill of its character, as the R skill does, and SHALL still be worse than the R skill at its maximum rank in at least one of its cost, cooldown or effect values.

#### Scenario: Cost and cooldown
- **WHEN** an evolution of the R skill is compared with the base Q and E skills of its character
- **THEN** its mana cost is higher and its cooldown longer than both

#### Scenario: Not strictly better
- **WHEN** an evolution of the R skill is compared with the R skill at its maximum rank
- **THEN** at least one of its cost, cooldown or effect values is worse

### Requirement: Everyone sees an evolved E or R skill and the owner's screen shows it
Every client SHALL show the effect of an evolved E or R skill at the place and moment the server fired it, as the effect of the evolution. The owner's skill bar SHALL show the evolution's name on the E or R slot with the cost and cooldown that apply, and the level-up choices SHALL show it by name with a short description of what it changes.

#### Scenario: Seen by others
- **WHEN** a survivor fires an evolved E or R skill
- **THEN** every other client shows its effect with the evolution's size, at the same place and time

#### Scenario: Skill bar
- **WHEN** a survivor who has learned an evolution of the E skill or of the R skill looks at the skill bar
- **THEN** the E slot or the R slot shows the evolution's name, and the cooldown counts down after use

#### Scenario: The choice text
- **WHEN** an evolution of an E or R skill is among the offered choices
- **THEN** it is shown by its name with a description of its trade
