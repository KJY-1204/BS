# Spec Delta

## MODIFIED Requirements

### Requirement: Skills may belong to one character
A skill SHALL be definable as belonging to one character or as common to all. The four basic passives SHALL be common, each character SHALL have two passives of its own, and each active skill SHALL belong to the character that owns it. The skill numbers of every skill SHALL fit in the packed ranks sent to the owner, which hold at least twenty-one skills.

#### Scenario: Offers differ by character
- **WHEN** two survivors of different characters level up with no skills learned
- **THEN** both can be offered the common passives, but only each one's own active skills

#### Scenario: Own passive
- **WHEN** two survivors of different characters have both learned the skills their passives need and level up
- **THEN** each can be offered its own character's passives and never the other's

#### Scenario: Packed ranks
- **WHEN** a survivor's ranks for every skill number from one to twenty-one are packed and unpacked
- **THEN** every rank comes back unchanged and no number affects another
