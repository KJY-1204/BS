# Spec Delta

## MODIFIED Requirements

### Requirement: Skills may belong to one character
A skill SHALL be definable as belonging to one character or as common to all. The four basic passives SHALL be common, each character SHALL have one passive of its own, and each active skill SHALL belong to the character that owns it.

#### Scenario: Offers differ by character
- **WHEN** two survivors of different characters level up with no skills learned
- **THEN** both can be offered the common passives, but only each one's own active skills

#### Scenario: Own passive
- **WHEN** two survivors of different characters have both learned their Q skill and level up
- **THEN** each can be offered its own character's passive and never the other's
