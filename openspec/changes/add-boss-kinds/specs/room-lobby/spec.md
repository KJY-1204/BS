# Spec Delta

## MODIFIED Requirements

### Requirement: The waiting room shows who is here
While the room is waiting, every player SHALL see each connected player, that player's role (boss or survivor slot), and, for survivors, the character that player has chosen or that none is chosen yet, and, for the boss, the kind of boss that player will play.

#### Scenario: A player joins the waiting room
- **WHEN** a player joins a waiting room
- **THEN** every client shows that player with a role and no character chosen yet

#### Scenario: A player leaves the waiting room
- **WHEN** a player disconnects from a waiting room
- **THEN** every client no longer shows that player and that player's character becomes available

#### Scenario: The boss kind is shown
- **WHEN** the boss player chooses a kind in the waiting room
- **THEN** every client shows the boss player with that kind
