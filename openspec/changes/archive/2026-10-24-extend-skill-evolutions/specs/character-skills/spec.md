# Spec Delta

## MODIFIED Requirements

### Requirement: Each character owns a Q skill and an E skill
Every character SHALL have exactly one base active skill for the Q slot and one for the E slot, and the two keys SHALL fire the skills of the survivor's own character; a learned evolution of a base skill is fired in place of it. A skill SHALL only be learnable and usable by the character that owns it. The passive skills SHALL stay common to every character.

#### Scenario: Slots follow the character
- **WHEN** a survivor of one character and a survivor of another press Q
- **THEN** each fires the Q skill of their own character

#### Scenario: Another character's skill
- **WHEN** a request names a skill that belongs to another character
- **THEN** the server ignores it and nothing happens

#### Scenario: Slot display
- **WHEN** a survivor looks at the skill slots
- **THEN** each slot shows the name of that character's own skill, learned or not
