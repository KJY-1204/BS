# Spec Delta

## MODIFIED Requirements

### Requirement: A level up opens a choice of three skills
When a survivor gains a level, the server SHALL pick up to three different skills at random from those that are available (owned by that survivor's character or common to all, prerequisites met, not at maximum rank, and not excluded by a skill the survivor has learned) and offer them to that survivor. If fewer than three are available, the server SHALL offer what exists, and if none are available no choice SHALL open.

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

#### Scenario: An excluded skill
- **WHEN** a skill excludes another and the survivor has learned the first
- **THEN** the other is never offered, whatever its prerequisites
