# Spec Delta

## MODIFIED Requirements

### Requirement: Combat traffic stays within budget
With four survivors firing continuously, the boss firing continuously at its fastest rate, 100 mobs alive and every ranged mob shooting at its own rate, the combat events sent to each client SHALL average no more than 10 KB per second, including the blasts of exploding mobs.

#### Scenario: Full fire
- **WHEN** four survivors and the boss hold fire for one minute with 100 mobs alive, among them ranged and exploding mobs
- **THEN** the measured combat traffic to each client averages no more than 10 KB per second

## ADDED Requirements

### Requirement: Everyone sees the shots of mobs
Every player SHALL see every projectile a mob fires, drawn by the same replay rule as other projectiles from the fire event alone, with the mob shot's own shape and a color unlike a survivor's or the boss's shots. A hit of a mob's shot on a survivor SHALL end the shot on every client at the hit position with a hit effect, as a boss shot does.

#### Scenario: A mob fires
- **WHEN** a ranged mob fires
- **THEN** every client shows the shot leaving the mob and flying along its aim in the mob shot's color

#### Scenario: A mob's shot hits
- **WHEN** a mob's shot hits a survivor
- **THEN** every client removes the shot at the hit position and shows a hit effect there

### Requirement: Blasts are reliable and drawn at the display time
The server SHALL tell clients reliably, in the same stream as the other combat events, when an exploding mob blasts, with where and how far. Every client SHALL draw the blast as a short placeholder effect of that size at the position when the display time of the event comes, so the effect appears at the moment the exploding mob disappears from the screen. The effect SHALL NOT affect the simulation.

#### Scenario: A blast is seen
- **WHEN** an exploding mob blasts
- **THEN** every client shows a short effect of the blast radius at the mob's position as the mob disappears

#### Scenario: Blast message size
- **WHEN** blasts happen
- **THEN** each blast uses a small fixed number of bytes in the combat message and no new kind of network message is needed

#### Scenario: Display only
- **WHEN** a client fails to show a blast effect
- **THEN** the server's survivors, mobs and projectiles are unchanged
