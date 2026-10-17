# Spec Delta

## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: Skills may belong to one character
A skill SHALL be definable as belonging to one character or as common to all. Passive skills SHALL be common, and each active skill SHALL belong to the character that owns it.

#### Scenario: Offers differ by character
- **WHEN** two survivors of different characters level up with no skills learned
- **THEN** both can be offered the common passives, but only each one's own active skills
