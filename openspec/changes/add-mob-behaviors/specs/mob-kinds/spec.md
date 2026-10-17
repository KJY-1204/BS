# Spec Delta

## MODIFIED Requirements

### Requirement: Mob kinds are data
The game SHALL define a small fixed set of mob kinds as data: for each kind a name, a speed, a maximum health, a body size and a contact damage, each given as a multiple of the basic kind's value, the energy cost and group size the boss's summon uses for it, and its behavior (see the mob-behaviors capability). The set SHALL contain a basic kind that behaves exactly as mobs did before kinds existed, a fast kind (quicker, with less health and a slightly smaller body), a tough kind (slower, with much more health, a bigger body and a harder touch), a ranged kind and an exploding kind. Every value SHALL be adjustable without changing rules elsewhere, and rules SHALL read these values and never a kind's identity.

#### Scenario: The basic kind is unchanged
- **WHEN** a basic mob is spawned
- **THEN** its speed, health, size and contact damage are the same as a mob's were before kinds existed

#### Scenario: Different kinds differ
- **WHEN** a fast mob and a tough mob are compared with a basic one
- **THEN** the fast one is quicker with less health and a smaller body, and the tough one is slower with more health, a bigger body and a higher contact damage

#### Scenario: Changing the basic value
- **WHEN** the basic mob health is changed for a test
- **THEN** every other kind's health changes with it, because each kind is a multiple of the basic value

#### Scenario: Five kinds
- **WHEN** the set of kinds is listed
- **THEN** it holds the basic, fast, tough, ranged and exploding kinds, each with its own cost and group size

### Requirement: Kinds look different on every screen
Every client SHALL draw a mob in the color of its kind and at the size of its kind's body, using only placeholder shapes. Basic mobs SHALL keep the existing difference of color between the automatic and the summoned origin. The kinds SHALL be told apart at a glance.

#### Scenario: Seeing the kinds
- **WHEN** a basic, a fast, a tough, a ranged and an exploding mob are on the field
- **THEN** every client draws five different colors, and the tough mob is the largest

#### Scenario: Basic origins
- **WHEN** an automatic basic mob and a summoned basic mob are on the field
- **THEN** every client draws them in different colors, as before kinds existed
