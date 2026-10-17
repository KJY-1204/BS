# Spec Delta

## Purpose

Defines how the boss becomes more dangerous as it loses health: the faster fire rate and stronger projectiles that follow its remaining health, decided by the server.

## ADDED Requirements

### Requirement: A weaker boss fires faster
The boss's fire cooldown SHALL shrink continuously as its health falls, from the normal cooldown at full health to a fixed fraction of it at zero health. The server SHALL compute this from the boss's current health at the time each shot's cooldown starts, and the shot that started the cooldown SHALL NOT be affected by a later change.

#### Scenario: Full health
- **WHEN** the boss is at full health
- **THEN** its fire cooldown is the normal value

#### Scenario: Half health
- **WHEN** the boss is at half health
- **THEN** its fire cooldown is the value halfway between the normal one and the lowest one

#### Scenario: Nearly fallen
- **WHEN** the boss is at one health point
- **THEN** its fire cooldown is close to the lowest value and never below it

### Requirement: A weaker boss hits harder
The damage of the boss's projectiles SHALL grow continuously as its health falls, from the normal damage at full health to a fixed multiple of it at zero health, rounded to a whole number and never below one. A projectile SHALL keep the damage it was fired with, so a later change in the boss's health does not change shots already in flight.

#### Scenario: Full health
- **WHEN** the boss at full health fires
- **THEN** the projectile deals the normal damage

#### Scenario: Weakened boss
- **WHEN** the boss at low health fires
- **THEN** the projectile deals more than the normal damage, up to the strongest value

#### Scenario: Shot in flight
- **WHEN** the boss's health falls after a projectile left
- **THEN** that projectile still deals the damage it was fired with

### Requirement: Enrage is decided by the server and needs no extra traffic
The strengthening SHALL be computed on the server only. Clients SHALL NOT receive any extra value for it, because the effects (shots that leave faster and hit harder) are seen through the normal shot events and the survivors' replicated health.

#### Scenario: A client's view
- **WHEN** the boss grows stronger
- **THEN** clients only see more frequent shots and larger health losses, and no new message type or value is sent for it

### Requirement: Enrage values are adjustable and a match restarts clean
The lowest cooldown fraction and the strongest damage multiple SHALL be adjustable values. Enrage SHALL depend only on the boss's current health, so a new match with a boss at full health starts at the normal values.

#### Scenario: A second match
- **WHEN** a new match starts after a match in which the boss grew strong
- **THEN** the boss fires at the normal cooldown and damage
