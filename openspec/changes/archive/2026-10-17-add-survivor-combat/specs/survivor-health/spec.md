# Spec Delta

## Purpose

Defines survivor health: how much each survivor has, how damage and the short protection after a hit work, when a survivor dies, what a dead survivor can no longer do, and how health is shown to players.

## ADDED Requirements

### Requirement: Survivors have health
Every survivor SHALL have a health value with a fixed maximum. A survivor SHALL start a room with full health, and every player SHALL be able to see every survivor's current health.

#### Scenario: Room start
- **WHEN** a survivor's character is created
- **THEN** the survivor has maximum health and all clients show that value

#### Scenario: Health is visible to everyone
- **WHEN** a survivor's health changes
- **THEN** every client shows the new value for that survivor

### Requirement: Health is controlled by the server
Only the server SHALL change a survivor's health. A client SHALL NOT be able to raise or lower any survivor's health.

#### Scenario: Client tries to set health
- **WHEN** a client attempts to change a survivor's health
- **THEN** the server ignores the attempt and the health value is unchanged

### Requirement: Damage reduces health and never goes below zero
Damage dealt to a survivor SHALL reduce health by the damage amount, and health SHALL NOT go below zero.

#### Scenario: Normal hit
- **WHEN** a survivor with health 100 takes damage 10
- **THEN** the survivor has health 90

#### Scenario: Overkill
- **WHEN** a survivor with health 5 takes damage 10
- **THEN** the survivor has health 0

### Requirement: A hit protects the survivor briefly
After a survivor takes damage, the survivor SHALL take no damage for a short protection time, and the same protection SHALL apply after being revived.

#### Scenario: Repeated contact
- **WHEN** a survivor is touched by a mob again during the protection time
- **THEN** no further damage is dealt until the protection time has passed

#### Scenario: Protection ends
- **WHEN** the protection time has passed and a mob still touches the survivor
- **THEN** the survivor takes damage again

### Requirement: A survivor dies at zero health
A survivor whose health reaches zero SHALL die. Death SHALL be visible to every player.

#### Scenario: Last hit
- **WHEN** a survivor's health reaches zero
- **THEN** the survivor is dead on every client and is shown as fallen

### Requirement: A dead survivor cannot act
A dead survivor SHALL NOT move, SHALL NOT fire, and SHALL NOT be chosen as a target by mobs. The dead survivor's character SHALL stay where it fell.

#### Scenario: Input while dead
- **WHEN** a dead survivor's player presses move or fire
- **THEN** the character does not move and no projectile is fired

#### Scenario: Mobs ignore the dead
- **WHEN** a survivor dies
- **THEN** mobs stop chasing that survivor and chase the nearest living survivor instead

### Requirement: Own health is shown clearly
Each player SHALL see their own survivor's current and maximum health on screen, and every survivor SHALL have a health bar next to its character.

#### Scenario: Taking damage
- **WHEN** the local survivor takes damage
- **THEN** the on-screen value and the bar over the character both show the lower health

#### Scenario: Dead local survivor
- **WHEN** the local survivor is dead
- **THEN** the screen shows that the survivor is dead
