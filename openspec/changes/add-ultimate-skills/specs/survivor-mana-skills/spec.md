# Spec Delta

## RENAMED Requirements

- FROM: `### Requirement: Two skill slots are triggered by keys`
- TO: `### Requirement: Skill slots are triggered by keys`

## MODIFIED Requirements

### Requirement: Skill slots are triggered by keys
A survivor SHALL have three skill slots, triggered by the keys Q, E and R. A slot SHALL be usable only after a skill has been learned for it, and the owner SHALL send only the slot to the server.

#### Scenario: Unlearned slot
- **WHEN** a player presses a key for a slot with no learned skill
- **THEN** nothing happens

#### Scenario: Learned slot
- **WHEN** a player presses the key of a slot whose skill is learned
- **THEN** the request reaches the server
