# Spec Delta

## MODIFIED Requirements

### Requirement: The barrier makes the caster invulnerable for a time
The Reimu E skill SHALL make her take no damage for its duration, which grows with rank and with her duration passive, and every client SHALL see that she is protected for that time. The protection SHALL end by itself, and SHALL end if she is already dead.

#### Scenario: Touched by mobs
- **WHEN** a survivor under the barrier is touched by mobs for the whole duration
- **THEN** her health does not drop

#### Scenario: Barrier ends
- **WHEN** the duration has passed
- **THEN** mobs can hurt her again and the protection mark is gone on every client

#### Scenario: Lengthened by the passive
- **WHEN** she uses the barrier with her duration passive learned
- **THEN** the protection and the mark last the defined duration multiplied by the duration scale

### Requirement: Miracle heals the caster and nearby survivors
The Sanae Q skill SHALL restore health to her and to every living survivor within its radius, which grows with her range passive, by an amount that grows with rank, never above a survivor's maximum health. It SHALL NOT bring a dead survivor back.

#### Scenario: Heal in range
- **WHEN** the skill fires with a wounded ally inside the radius and another outside
- **THEN** the ally inside and Sanae are healed and the one outside is not

#### Scenario: Maximum
- **WHEN** a heal would take health above the maximum
- **THEN** health stops at the maximum

#### Scenario: A fallen ally
- **WHEN** a dead survivor stands inside the radius
- **THEN** the dead survivor stays dead

#### Scenario: Widened by the passive
- **WHEN** an ally stands beyond the defined radius but inside the radius multiplied by the range scale
- **THEN** the ally is healed

### Requirement: Wind Veil pushes mobs away
The Sanae E skill SHALL push every mob within its radius directly away from her by a distance that grows with rank, without leaving the arena and without damaging the mobs. The radius and the distance SHALL grow with her range passive.

#### Scenario: Pushed out
- **WHEN** the skill fires with a mob inside the radius
- **THEN** the mob ends up farther from her along the same direction by the push distance, or at the arena edge if that is nearer

#### Scenario: Outside the radius
- **WHEN** a mob stands outside the radius
- **THEN** it does not move

#### Scenario: Widened by the passive
- **WHEN** she uses the skill with her range passive learned
- **THEN** the radius and the push distance are the defined values multiplied by the range scale
