# Spec Delta

## ADDED Requirements

### Requirement: An available evolution is always offered
When a level-up offer is built for a survivor and at least one evolution is available to that survivor (its base skill is at the maximum rank, no evolution of that base skill has been learned, and the evolution belongs to the survivor's character), the offer SHALL contain at least one evolution. The number of choices SHALL not change and the evolution SHALL replace one of the random choices only when none of them is an evolution. An offer with no evolution available SHALL be built as before.

#### Scenario: One evolution available
- **WHEN** a survivor has a skill at its maximum rank with no evolution learned and an offer is built
- **THEN** the offer contains an evolution of that skill or of another skill that qualifies

#### Scenario: Evolutions of several skills
- **WHEN** the evolutions of two skills are available
- **THEN** the offer contains at least one of the four, and every one of them is offered over many offers

#### Scenario: Nothing available
- **WHEN** no evolution is available
- **THEN** the offer is the usual choice of random skills and holds no evolution

#### Scenario: Taken evolution
- **WHEN** a survivor has learned an evolution of a skill
- **THEN** no evolution of that skill is offered again, guarantee or not

#### Scenario: A full offer
- **WHEN** an offer is built with an evolution available
- **THEN** it has the same number of choices as an offer without one, and no skill appears twice

### Requirement: The guarantee can be turned off
The harness SHALL accept an override `evolutionOfferGuarantee`: 1 (the default) applies the guarantee, 0 builds offers as before it existed. Any other value SHALL be refused with a reason that names the override.

#### Scenario: Off
- **WHEN** a match runs with `evolutionOfferGuarantee` 0
- **THEN** an available evolution is offered only when the random choices include it

#### Scenario: Bad value
- **WHEN** `evolutionOfferGuarantee` is 2
- **THEN** the settings are refused
