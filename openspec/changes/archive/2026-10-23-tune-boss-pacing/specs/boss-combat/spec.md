# Spec Delta

## MODIFIED Requirements

### Requirement: The boss's health is decided by the server and seen by everyone
The server SHALL keep the boss's health and SHALL tell every client the current and maximum health exactly, for any health the boss's data gives, including a health of several hundred thousand. Every client SHALL show a health bar over the boss. A boss hit SHALL be shown on every client like a mob hit.

#### Scenario: Everyone sees the bar
- **WHEN** the boss loses health
- **THEN** every client shows the boss's bar lower and a hit effect at the hit position

#### Scenario: A large health
- **WHEN** the boss has a maximum health of 250000 and has lost 1
- **THEN** every client reads a maximum of 250000 and a current health of 249999, and the number shown is the same

#### Scenario: A client cannot change it
- **WHEN** a client tries to set the boss's health
- **THEN** nothing changes
