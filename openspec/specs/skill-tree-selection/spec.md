# skill-tree-selection Specification

## Purpose

Defines the skill tree and how a survivor picks from it on level up: the server offers three choices, the player picks one without the game pausing, and a choice not made in time is made at random.

## Requirements

### Requirement: Skills are defined as data
Each skill SHALL be defined by data: an id, whether it is passive or active, a maximum rank, the skills and ranks it requires, and its effect at each rank. The set of skills SHALL be changeable without changing the selection rules.

#### Scenario: A skill with a prerequisite
- **WHEN** a skill requires another skill at rank one
- **THEN** it is not offered until the other skill has reached rank one

#### Scenario: A maxed skill
- **WHEN** a skill is at its maximum rank
- **THEN** it is not offered again

### Requirement: A level up opens a choice of three skills
When a survivor gains a level, the server SHALL pick up to three different skills at random from those that are available (owned by that survivor's character or common to all, prerequisites met and not at maximum rank) and offer them to that survivor. If fewer than three are available, the server SHALL offer what exists, and if none are available no choice SHALL open.

#### Scenario: Normal offer
- **WHEN** a survivor levels up and five skills are available
- **THEN** exactly three different ones are offered

#### Scenario: Few skills left
- **WHEN** only two skills are available
- **THEN** those two are offered

#### Scenario: Nothing left
- **WHEN** no skill is available
- **THEN** no choice opens and the level up is not held up

#### Scenario: Another character's skill
- **WHEN** a survivor levels up
- **THEN** no offered skill belongs to a different character

### Requirement: Only the owner sees the offer
The offered choices SHALL be sent only to the survivor they belong to. Other players SHALL NOT receive them.

#### Scenario: Teammates
- **WHEN** a survivor is offered three skills
- **THEN** the other players' clients do not receive the offer

### Requirement: The player picks by number without pausing the game
The player SHALL pick one offered skill by pressing 1, 2 or 3 while the game keeps running. The owner SHALL send only the number, and the server SHALL decide the result.

#### Scenario: A valid pick
- **WHEN** the player presses 2 within the time limit
- **THEN** the second offered skill gains one rank and the choice closes

#### Scenario: Game keeps running
- **WHEN** a choice is open
- **THEN** mobs, shots and other players continue as normal

### Requirement: An unmade choice is made at random
If the player does not pick within the time limit, the server SHALL pick one of the offered skills at random and apply it. The owner SHALL be told which skill was chosen.

#### Scenario: Timeout
- **WHEN** the time limit passes with no pick
- **THEN** one of the offered skills gains a rank and the choice closes

#### Scenario: Countdown is visible
- **WHEN** a choice is open
- **THEN** the owner sees the time left

### Requirement: Invalid picks are ignored
The server SHALL ignore a pick that is out of range, arrives when no choice is open, refers to a choice that has already closed, or comes from a player who is not the owner of the choice.

#### Scenario: Out of range
- **WHEN** a client sends the number 7
- **THEN** nothing changes and the choice stays open

#### Scenario: Late pick
- **WHEN** a pick arrives after the choice closed by timeout
- **THEN** it is ignored and does not change the result

#### Scenario: Double pick
- **WHEN** a client sends two picks for the same choice
- **THEN** only the first counts

### Requirement: Consecutive level ups are handled in turn
If a survivor gains several levels before choosing, the choices SHALL be offered one at a time in order, each with its own time limit.

#### Scenario: Two levels at once
- **WHEN** a survivor gains two levels from one kill
- **THEN** the first choice opens, and the second opens when the first closes

#### Scenario: Each has a limit
- **WHEN** the first choice closes by timeout
- **THEN** the second starts with a full time limit

### Requirement: Choices survive death and disconnects
A survivor who dies with a choice open SHALL keep that choice and its countdown. When a survivor disconnects, their choices SHALL be discarded without affecting anyone else.

#### Scenario: Dying with a choice open
- **WHEN** a survivor dies while a choice is open
- **THEN** the choice stays open and still closes by pick or timeout

#### Scenario: Disconnect
- **WHEN** a survivor with queued choices disconnects
- **THEN** the choices are dropped and the other survivors are unaffected

### Requirement: Learned skills and ranks are private to the owner
A survivor's learned skills and ranks SHALL be sent only to that survivor. The effects of the skills SHALL still be seen by everyone when they happen.

#### Scenario: Seeing own skills
- **WHEN** a survivor learns or ranks up a skill
- **THEN** their screen shows the new skill list and ranks

### Requirement: Skills may belong to one character
A skill SHALL be definable as belonging to one character or as common to all. The four basic passives SHALL be common, each character SHALL have four passives of its own, and each active skill SHALL belong to the character that owns it. The skill numbers of every skill SHALL fit in the packed ranks sent to the owner, which hold at least forty-two skills.

#### Scenario: Offers differ by character
- **WHEN** two survivors of different characters level up with no skills learned
- **THEN** both can be offered the common passives, but only each one's own active skills

#### Scenario: Own passive
- **WHEN** two survivors of different characters have both learned the skills their passives need and level up
- **THEN** each can be offered its own character's passives and never the other's

#### Scenario: Packed ranks
- **WHEN** a survivor's ranks for every skill number from one to forty-two are packed and unpacked
- **THEN** every rank comes back unchanged and no number affects another

#### Scenario: A rank at the edge of each packed value
- **WHEN** the ranks of skill numbers twenty-one and twenty-two, which sit on either side of the split between the two packed values, are set to different values
- **THEN** each comes back as set and neither changes the other
