# boss-projectiles Specification

## Purpose

Defines the boss player's own ranged attack: aiming and firing with the mouse, when the server lets the boss shoot, how a boss projectile flies and hurts survivors, how everyone sees it, and what the boss player is told about the controls.

## Requirements

### Requirement: The boss aims and fires directly
The boss player SHALL aim at the pointer (or the right stick on a gamepad) and SHALL fire by holding the left mouse button (or the right trigger). While fire is held the server SHALL fire one projectile from the boss's position in the aimed direction each time the boss's fire cooldown has passed. The cooldown SHALL keep running after the button is released, so pressing and releasing quickly cannot fire faster than holding. The aim and fire state SHALL travel to the server with the same rate limits as a survivor's.

#### Scenario: Holding fire
- **WHEN** the boss player holds fire toward a point
- **THEN** the server fires a projectile toward that point once per fire cooldown until fire is released

#### Scenario: Quick taps
- **WHEN** the boss player presses and releases fire more often than the fire cooldown allows
- **THEN** shots still leave no faster than one per fire cooldown

#### Scenario: Aim follows the pointer
- **WHEN** the boss player moves the pointer while holding fire
- **THEN** later shots leave toward the new aim

### Requirement: Only a standing boss in a running match can fire
The boss SHALL be able to fire only while a match is running and the boss has not fallen. A fire request from anyone who is not the boss player SHALL NOT make the boss fire, and a survivor SHALL NOT make the boss fire. When the match ends or the boss falls, projectiles already in flight SHALL be allowed to end normally but no new boss projectile SHALL be created.

#### Scenario: Match over
- **WHEN** the match has ended and the boss player holds fire
- **THEN** no projectile is created

#### Scenario: Fallen boss
- **WHEN** the boss has fallen
- **THEN** no further boss projectile is created

#### Scenario: Not the boss
- **WHEN** a survivor's client sends aim and fire
- **THEN** only that survivor's own attack is affected and the boss does not fire

### Requirement: A boss projectile hurts survivors only
A boss projectile SHALL fly straight at the boss projectile speed and end at the arena edge or when its lifetime ends. It SHALL stop at the first living survivor it touches and deal that projectile's damage to that survivor under the survivor-health rules. It SHALL pass through mobs and through the boss, and SHALL NOT damage them. A survivor who cannot be hurt at that moment (fallen, or still within the protection time) SHALL NOT stop the projectile; it passes through.

#### Scenario: Hit a survivor
- **WHEN** a boss projectile touches a living survivor who can be hurt
- **THEN** the survivor loses health equal to the projectile's damage and the projectile ends there

#### Scenario: Mobs and boss in the way
- **WHEN** a boss projectile passes through a mob's or the boss's position
- **THEN** nothing takes damage and the projectile continues

#### Scenario: Protected or fallen survivor
- **WHEN** a boss projectile touches a survivor who is within the protection time or has fallen
- **THEN** that survivor takes no damage and the projectile continues

#### Scenario: Fast projectile
- **WHEN** a boss projectile moves farther in one simulation step than a survivor's width
- **THEN** it still hits a survivor that lies on its path

#### Scenario: Two survivors in line
- **WHEN** a boss projectile's path crosses two survivors who can be hurt
- **THEN** only the nearer one takes damage and the projectile ends

### Requirement: Boss projectiles share the projectile cap
Boss projectiles SHALL count toward the same hard cap on simultaneous projectiles as survivor projectiles, and a shot refused by the cap SHALL NOT use up the boss's fire cooldown.

#### Scenario: Cap reached
- **WHEN** the number of projectiles has reached the cap and the boss holds fire
- **THEN** no projectile is created and the boss may fire again as soon as room appears

### Requirement: Everyone sees the boss's shots
Every player SHALL see every boss projectile, drawn by the same replay rule as survivor projectiles from the fire event alone. Boss projectiles SHALL look different from every survivor's (the boss's own color and a larger size than a survivor shot) and SHALL be as wide on screen as they really are. A hit on a survivor SHALL show a short hit effect at the hit position on every client, and SHALL NOT show a mob health bar.

#### Scenario: A survivor sees an incoming shot
- **WHEN** the boss fires
- **THEN** every survivor's screen shows the projectile leaving the boss and flying along the aim, in the boss's color

#### Scenario: Hit effect
- **WHEN** a boss projectile hits a survivor
- **THEN** every client removes the projectile at the hit position and shows a hit effect there, and the survivor's own health bar shows the loss

### Requirement: The boss player sees the controls
The boss player's screen SHALL show a hint for the controls: fire, summon, order and clear order. The hint SHALL name the left click for fire and Space for summon. Survivors SHALL NOT see this hint.

#### Scenario: Boss screen
- **WHEN** a match is running and the boss player looks at the screen
- **THEN** the hint shows left click as fire, Space as summon, right click as target and C as clear

#### Scenario: Left click no longer summons
- **WHEN** the boss player presses the left mouse button
- **THEN** a projectile is fired and no summon happens

### Requirement: Boss firing values are adjustable
The boss's fire cooldown, projectile damage, speed, lifetime and size SHALL be adjustable values that can change without changing rules elsewhere. The values in this change are starting assumptions to be tuned by playtest.

#### Scenario: Changed damage
- **WHEN** the boss projectile damage value is changed
- **THEN** boss projectiles deal the new damage without any other rule changing
