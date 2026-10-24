# Tasks

## 1. The rule (skill-evolutions)

- [x] 1.1 `LevelingSim.GuaranteeEvolutionOffer` and the replacement in `OpenNextOffer` (only when no choice is an evolution and one is available)
- [x] 1.2 `HarnessSettings` override `evolutionOfferGuarantee` (0 or 1, other values refused)

## 2. Tests (skill-evolutions)

- [x] 2.1 With an evolution available the offer holds one; the size is unchanged and no skill repeats; over many offers every available evolution appears
- [x] 2.2 With none available the offer is built as before (same choices as the off setting with the same seed); a taken evolution is not offered again; the switch and a bad value

## 3. Balance and documentation

- [x] 3.1 Harness: natural play, 120 matches per kind with the guarantee on (the earlier measurement is the off baseline), keep the gate; save settings and `results.md` in `tools/harness/evolution-offer/`, with the count of survivors that ended with a Q, E or R evolution if the summary has it
- [x] 3.2 README (the guarantee, the override, the measurement, the limit lifted), the test count, the whole EditMode suite passes
