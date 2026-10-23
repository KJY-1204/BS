# Proposal

## Why

Every survivor has two active skills, Q and E, and each rank only makes their numbers bigger. By level 20 almost every skill is at its maximum rank, so two survivors of the same character end a match with nearly the same kit, and a fifteen-minute match has no moment the players build toward. A third, rarer skill on its own key gives the growth a peak: something to earn in the middle of the match and a button worth saving for the right moment, such as the boss's shockwave or a swarm around a fallen friend. The skills are data and the server already runs any number of slots, so the cost of one more slot is small now and will grow with every skill added later.

## What Changes

- A survivor gets a third active skill slot on the R key. Each of the four characters owns one R skill: Reimu a large area burst, Marisa a long wide beam, Sakuya a big ring of knives, Sanae a wide, strong heal. Each has a high mana cost and a long cooldown (40 to 45 seconds) and three ranks, like the other active skills.
- An R skill is earned like any skill: it joins the three-way choice on a level up once the survivor's own Q skill has reached rank 2, using the existing prerequisite rule. No new selection rule and no automatic grant.
- The server fires an R skill by the same rules as Q and E (alive, learned, off cooldown, enough mana), tells the owner the result and the cooldown the same way, and sends the same small skill event so every client sees it. The character's own passives (cost, power, cooldown, range, duration) apply to it like to Q and E.
- The skill bar shows three slots (Q, E, R) with the R skill's name, rank, cooldown and mana shortfall. The development arguments learn an `r` key and `qer` for `-bsAutoSkill`, and `-bsDevSkills` also teaches the R skill.
- The match harness bots already press every slot, so they use R skills without a new rule; the pacing is measured again with the harness and the R numbers are adjusted if the match length or the survivors' win share leaves the range the boss pacing was tuned for.

Not changed: the Q and E skills, the passives, the boss, the number of choices on a level up, the maximum level, the skill id packing (the four new skills take ids 29 to 32 of the 42 it holds).

## Capabilities

### New Capabilities
- `ultimate-skills`: the R slot and the four R skills: how one is earned, what each does, how it is fired, shown and replicated.

### Modified Capabilities
- `survivor-mana-skills`: a survivor has three skill slots (Q, E, R) instead of two.
- `character-skills`: the eight Q and E skills are listed apart from the R skills, so "the four characters own two each" stays true of Q and E only.

## Impact

- Code: `SkillCatalog` (slot count, four definitions, ids), `SkillSim` (cooldown per slot follows the slot count), `PlayerGrowth` (R key, cooldown array), `GrowthHud` (third slot, key label), `GrowthServer` (development grant loops over the slots), `DevArgs` (`-bsAutoSkill r|qer`), nothing in the combat codec: skill events carry the skill id and already fit any new id.
- Network: no new message; a fired R skill is one skill event like any other. The big knife ring is up to 60 projectile events at once, once per cooldown; a test keeps the combat traffic budget.
- Tests: EditMode tests for the data (slots, ids, prerequisite, offers), for each R effect at each rank, for the passives applying, and for the three-slot input and display values; multi-process runs where each character's R skill fires and every client sees the same event; the pacing harness run before and after.
- Not in scope: branching or evolving a skill at its maximum rank (the next change), reviving a fallen survivor with an R skill, R skills for the boss (it already has its own keys), new art or sounds (effects are the existing rings and beam, only larger).
