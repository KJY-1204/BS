# Spec Delta

## REMOVED Requirements

### Requirement: There are three boss kinds
**Reason**: It defined the standard kind as the boss the game had before kinds existed. The kinds are now tuned for long matches and no kind keeps those values, so the requirement is replaced by one that says how the kinds differ and one that bounds their speed.
**Migration**: See "There are three boss kinds that last for minutes" and "No boss is as fast as a survivor".

## ADDED Requirements

### Requirement: There are three boss kinds that last for minutes
There SHALL be three kinds: a standard kind, a giant that has more health, moves slower, is larger and hits harder on contact than the standard kind, and a sniper that has less health, moves faster, is smaller and fires its basic attack faster than the standard kind. Every kind SHALL have a great deal more health than a survivor, so that a match with a boss lasts for minutes and not seconds.

#### Scenario: The kinds differ
- **WHEN** the three kinds are listed
- **THEN** each has its own name, color and numbers, and the giant and the sniper differ from the standard kind in the ways above

#### Scenario: Health for a long match
- **WHEN** the health of each kind is compared with the damage one survivor deals with a basic attack in one second
- **THEN** it is at least a thousand times as large for every kind

#### Scenario: Changing a value
- **WHEN** one value of one kind is changed in the data
- **THEN** the next match with that kind uses it and no rule needs to change

### Requirement: No boss is as fast as a survivor
Every boss kind SHALL move slower than the slowest survivor character, so that a survivor who runs from the boss is never caught by the boss moving after it. The order of the kinds by speed SHALL stay giant, standard, sniper.

#### Scenario: Slower than every survivor
- **WHEN** the speed of each boss kind is compared with the speed of each survivor character
- **THEN** every boss kind is slower than every survivor character

#### Scenario: The order of the kinds
- **WHEN** the three kinds are compared by speed
- **THEN** the giant is slower than the standard kind and the standard kind is slower than the sniper

#### Scenario: A survivor outruns the boss
- **WHEN** a survivor walks straight away from a boss that walks straight after it
- **THEN** the distance between them grows
