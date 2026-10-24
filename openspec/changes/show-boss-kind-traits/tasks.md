# Tasks

## 1. Data and text (boss-kind-traits, boss-kinds)

- [x] 1.1 `BossDef` gets `TraitName` and `TraitText`; `BossCatalog` fills them for the giant and the sniper and copies them in `ForMatch` and `MatchSession`
- [x] 1.2 Pure helpers `SummonHud.TraitLineText` and `NetworkMenu.KindLabel`

## 2. Screens (boss-kind-traits, boss-kinds)

- [x] 2.1 The lobby kind button uses `KindLabel` (trait name beside the skills)
- [x] 2.2 `SummonHud` draws the trait line above the device bar when the kind has a trait

## 3. Tests and documentation

- [x] 3.1 Tests: names and texts exist for giant and sniper and not for standard, the texts contain the multiples, the helpers return the expected strings, the copies carry the texts
- [x] 3.2 README (traits section, test count) and the whole EditMode suite passes
