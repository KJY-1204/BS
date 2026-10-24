# Spec Delta

## Purpose
Defines how the survivor bots of the match harness behave around the boss's announced skills, so that the harness measures matches in which survivors do what a player would: step out of an announced skill.

## ADDED Requirements

### Requirement: Survivor bots step out of an announced shockwave
When a shockwave has been announced and its circle (its radius plus the bot's body and a small margin) contains a living survivor bot, that bot SHALL choose a walking direction away from the shockwave's centre at its next decision, in preference to its other walking rules. A bot outside the circle SHALL not be moved by it, and a shockwave that is not announced SHALL not move anyone.

#### Scenario: Inside the circle
- **WHEN** a shockwave is announced and a bot stands inside its radius
- **THEN** the bot's next walking direction points away from the centre

#### Scenario: Outside the circle
- **WHEN** a shockwave is announced and a bot stands outside its radius and margin
- **THEN** the shockwave does not change the bot's walking

#### Scenario: Nothing announced
- **WHEN** no skill is announced
- **THEN** the evasion does not change any bot's walking

### Requirement: Survivor bots step out of an announced beam
When a beam has been announced and a living survivor bot stands within the beam's half width (plus the bot's body and a small margin) of its line, between its start and its end, that bot SHALL choose a walking direction sideways, away from the line, at its next decision. A bot behind the start, beyond the end or farther to the side SHALL not be moved by it.

#### Scenario: On the line
- **WHEN** a beam is announced and a bot stands on its line
- **THEN** the bot's next walking direction is across the line, to the side it is on

#### Scenario: Beyond the end
- **WHEN** a bot stands beyond the end of an announced beam
- **THEN** the beam does not change the bot's walking

### Requirement: Evasion is a bot rule only and can be switched off
The evasion SHALL change what the bots do and nothing else: no skill, damage, warning time, message or screen SHALL differ. A development switch SHALL turn the evasion off so that bots ignore announced skills as before.

#### Scenario: Switched off
- **WHEN** the evasion is switched off and a bot stands in an announced shockwave
- **THEN** the bot's walking is decided by its other rules

#### Scenario: Same game rules
- **WHEN** the evasion is on or off
- **THEN** every skill's warning time, damage and area are the same
