# mob-behaviors Specification

## Purpose

Defines mobs that do more than chase: that a mob kind has a behavior, how a ranged mob keeps its distance and shoots survivors, how an exploding mob runs in, waits a moment and blasts survivors around it, and how freezing, orders and the end of a match treat those behaviors.

## Requirements

### Requirement: A mob kind has a behavior
Every mob kind SHALL have one behavior with the values that behavior needs, kept as data with the rest of the kind. The behaviors SHALL be chasing (move toward the target and touch it, as every mob did before this change), ranged and exploding. The basic, fast and tough kinds SHALL keep the chasing behavior and behave exactly as before. The values SHALL be adjustable without changing rules elsewhere, and rules SHALL read the behavior and its values, never a kind's identity.

#### Scenario: Old kinds unchanged
- **WHEN** a basic, fast or tough mob is on the field
- **THEN** it chases and touches survivors exactly as before this change

#### Scenario: Two new kinds
- **WHEN** the set of kinds is listed
- **THEN** it holds a ranged kind and an exploding kind besides the three chasing kinds

### Requirement: A ranged mob stops at a distance and shoots
A ranged mob SHALL move toward its target like any mob until it is within its hold distance of that target, and SHALL stand still while it is within that distance. Whenever its shot cooldown has passed and a living survivor is within its shooting range, it SHALL fire one projectile toward the nearest living survivor and start the cooldown again. A new mob SHALL wait a varying part of the cooldown before its first shot, so mobs created together do not all fire at once. With no living survivor, or while the match is not running, it SHALL NOT fire.

#### Scenario: Stopping to shoot
- **WHEN** a ranged mob reaches its hold distance from the survivor it chases
- **THEN** it stops moving and fires a projectile at the nearest living survivor each time its cooldown has passed

#### Scenario: Out of range
- **WHEN** no living survivor is within the mob's shooting range
- **THEN** it keeps moving and does not fire

#### Scenario: Not all at once
- **WHEN** several ranged mobs are summoned together
- **THEN** their first shots leave at different times

#### Scenario: Nobody to shoot
- **WHEN** every survivor has fallen
- **THEN** ranged mobs do not fire

### Requirement: A mob's shot hurts survivors only
A projectile fired by a mob SHALL fly straight at the speed of the mob shot and end at the arena edge or when its lifetime ends. It SHALL stop at the nearest living survivor it touches who can be hurt at that moment and deal the mob shot's damage under the survivor-health rules. It SHALL pass through mobs, the boss, other projectiles, fallen survivors and survivors within the protection time. Mob shots SHALL count toward the same hard cap on projectiles as every other projectile, and a shot refused by the cap SHALL NOT use up the mob's cooldown.

#### Scenario: Hit a survivor
- **WHEN** a mob's shot touches a living survivor who can be hurt
- **THEN** that survivor takes the shot's damage and the shot ends there

#### Scenario: Protected survivor
- **WHEN** a mob's shot touches a survivor within the protection time
- **THEN** that survivor takes no damage and the shot continues

#### Scenario: Other targets
- **WHEN** a mob's shot passes through a mob, the boss or a survivor's projectile
- **THEN** nothing is affected and the shot continues

#### Scenario: Cap reached
- **WHEN** the projectile cap is reached when a ranged mob is ready to fire
- **THEN** no shot is created and the mob may fire again as soon as room appears

### Requirement: An exploding mob runs in, waits and blasts
An exploding mob SHALL chase its target like any mob. When any living survivor comes within its trigger distance it SHALL stop and start a fuse of fixed length. When the fuse ends it SHALL explode: every living survivor within the blast radius of the mob's position who can be hurt at that moment takes the blast damage under the survivor-health rules, and the mob is removed. A survivor who cannot be hurt (fallen, or within the protection time) SHALL take no damage. The blast SHALL NOT hurt the boss or any mob. If the mob is killed before the fuse ends it SHALL NOT explode.

#### Scenario: Blast
- **WHEN** an exploding mob's fuse ends with a survivor inside the blast radius
- **THEN** that survivor takes the blast damage and the mob is removed on every client

#### Scenario: Running out of the blast
- **WHEN** the survivor moves outside the blast radius before the fuse ends
- **THEN** the mob still explodes and the survivor takes no damage

#### Scenario: Killed in time
- **WHEN** a survivor kills the exploding mob during its fuse
- **THEN** it does not explode and nobody takes blast damage

#### Scenario: Several survivors
- **WHEN** two survivors who can be hurt stand inside the blast radius
- **THEN** both take the blast damage once

#### Scenario: Protected or fallen
- **WHEN** a survivor inside the blast radius is within the protection time or has fallen
- **THEN** that survivor takes no damage

#### Scenario: Mobs and boss
- **WHEN** a mob or the boss stands inside the blast radius
- **THEN** it takes no damage

### Requirement: A blast is not a kill
A mob that exploded by itself SHALL be removed like any removed mob but SHALL NOT count as killed by a survivor, so it gives no experience.

#### Scenario: No experience
- **WHEN** an exploding mob blasts and is removed
- **THEN** no survivor receives experience for it

### Requirement: Behaviors stop with time stop and with the match
While the mobs are stopped by a skill, no mob SHALL move, fire, count down a fuse or explode, and the fuse and cooldowns SHALL resume where they were when the stop ends. When the match ends or the room returns to waiting, every mob's behavior state and every mob projectile SHALL be cleared with the rest of the match, and the next match SHALL start with none.

#### Scenario: Time stop
- **WHEN** the mobs are stopped while an exploding mob's fuse is running
- **THEN** the fuse does not advance until the stop ends

#### Scenario: Match over
- **WHEN** the match ends with ranged and exploding mobs on the field
- **THEN** no further shot or blast happens, and the next match starts without them

### Requirement: Orders apply to every behavior
The boss's orders (a survivor or a point) SHALL apply to summoned mobs of the ranged and exploding kinds as they do to every summoned mob: they move toward the ordered target instead of the nearest survivor. A ranged mob SHALL still shoot the nearest living survivor within range, and an exploding mob SHALL still start its fuse when any living survivor is within its trigger distance, whatever it is ordered to chase.

#### Scenario: Ordered ranged mob
- **WHEN** a ranged mob is ordered to a point and a survivor is within its shooting range on the way
- **THEN** it keeps moving toward the point and shoots that survivor when its cooldown allows

#### Scenario: Ordered exploding mob
- **WHEN** an exploding mob is ordered to a survivor and another survivor comes within its trigger distance
- **THEN** its fuse starts
