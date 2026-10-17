# Spec Delta

## Purpose

Defines the kinds of mob: that a mob has a kind apart from its origin, how each kind differs in speed, health, size and contact damage, which kinds appear by themselves, how every client learns a mob's kind and origin, and how kinds look on screen.

## ADDED Requirements

### Requirement: Mob kinds are data
The game SHALL define a small fixed set of mob kinds as data: for each kind a name, a speed, a maximum health, a body size and a contact damage, each given as a multiple of the basic kind's value, and the energy cost and group size the boss's summon uses for it. The set SHALL contain a basic kind that behaves exactly as mobs did before kinds existed, a fast kind (quicker, with less health and a slightly smaller body) and a tough kind (slower, with much more health, a bigger body and a harder touch). Every value SHALL be adjustable without changing rules elsewhere, and rules SHALL read these values and never a kind's identity.

#### Scenario: The basic kind is unchanged
- **WHEN** a basic mob is spawned
- **THEN** its speed, health, size and contact damage are the same as a mob's were before kinds existed

#### Scenario: Different kinds differ
- **WHEN** a fast mob and a tough mob are compared with a basic one
- **THEN** the fast one is quicker with less health and a smaller body, and the tough one is slower with more health, a bigger body and a higher contact damage

#### Scenario: Changing the basic value
- **WHEN** the basic mob health is changed for a test
- **THEN** every other kind's health changes with it, because each kind is a multiple of the basic value

### Requirement: Every mob has a kind and an origin that do not depend on each other
The server SHALL know for each mob both its kind and its origin (automatic or summoned), SHALL keep both for the mob's whole life, and SHALL NOT infer one from the other. Any kind SHALL be possible for a summoned mob. An automatically spawned mob SHALL always be of the basic kind.

#### Scenario: Summoned tough mob
- **WHEN** the boss summons the tough kind
- **THEN** each created mob is of the tough kind and has the summoned origin

#### Scenario: Automatic mobs are basic
- **WHEN** the server spawns mobs by itself
- **THEN** every one of them is of the basic kind and has the automatic origin

#### Scenario: Counting by origin
- **WHEN** summoned mobs of several kinds and automatic mobs are on the field
- **THEN** the summoned count includes every kind and the automatic count only the automatic ones

### Requirement: A kind's stats apply to every mob rule
A mob SHALL move at its kind's speed, SHALL start with its kind's maximum health and report its health percentage against that maximum, SHALL keep apart from other mobs by the sizes of both bodies, SHALL stay inside the arena by its own body size, SHALL be touched by projectiles and by survivors by its own body size, and SHALL hurt a survivor it touches with its kind's contact damage.

#### Scenario: Speed
- **WHEN** a fast mob and a basic mob chase the same survivor from the same distance
- **THEN** the fast mob arrives first

#### Scenario: Health
- **WHEN** a tough mob takes damage equal to a basic mob's whole health
- **THEN** it survives and reports the health it has left as a percentage of its own maximum

#### Scenario: Size and projectiles
- **WHEN** a projectile passes close beside a tough mob and a basic mob at the same distance from their centers
- **THEN** it can touch the tough mob, which is bigger, without touching the basic one

#### Scenario: Size and separation
- **WHEN** a tough mob and a basic mob overlap
- **THEN** they are pushed apart until their bodies, whose sizes differ, no longer overlap

#### Scenario: Touching a survivor
- **WHEN** a tough mob touches a survivor who can be hurt
- **THEN** the survivor takes the tough kind's contact damage

#### Scenario: A crowd of different kinds
- **WHEN** a tough mob and a basic mob touch the same survivor at once
- **THEN** the survivor takes one hit for that protection time, with the highest contact damage among the mobs touching

### Requirement: Clients learn a mob's kind and origin when it appears
When a mob appears, every client SHALL learn its kind and its origin from the appearance message the mob already has, without any new kind of network message and without a larger message. A player who joins a room in progress SHALL learn the kind and origin of every current mob the same way. Snapshots SHALL NOT carry them.

#### Scenario: A summoned tough mob appears
- **WHEN** the server spawns a summoned tough mob
- **THEN** every client draws it as a tough mob and knows it is summoned

#### Scenario: Late joiner
- **WHEN** a player joins while mobs of several kinds are on the field
- **THEN** that player sees each mob with its own kind

#### Scenario: Message size
- **WHEN** mobs of any kind and origin appear
- **THEN** each appearance entry has the same size as before kinds existed

### Requirement: Kinds look different on every screen
Every client SHALL draw a mob in the color of its kind and at the size of its kind's body, using only placeholder shapes. Basic mobs SHALL keep the existing difference of color between the automatic and the summoned origin. The kinds SHALL be told apart at a glance.

#### Scenario: Seeing the kinds
- **WHEN** a basic, a fast and a tough mob are on the field
- **THEN** every client draws three different colors, and the tough mob is the largest

#### Scenario: Basic origins
- **WHEN** an automatic basic mob and a summoned basic mob are on the field
- **THEN** every client draws them in different colors, as before kinds existed

### Requirement: Kinds have no effect on rewards
Killing a mob SHALL give the same experience whatever its kind, and kinds SHALL NOT change the rules for orders, the hard cap or automatic spawning.

#### Scenario: Experience
- **WHEN** a survivor kills a tough mob and then a fast mob
- **THEN** the experience given for each is the same as for a basic mob

#### Scenario: Orders
- **WHEN** the boss orders summoned mobs to a survivor or a point
- **THEN** summoned mobs of every kind obey and automatic mobs do not

#### Scenario: Hard cap
- **WHEN** the field holds mobs of several kinds
- **THEN** each counts as one toward the hard cap, whatever its kind
