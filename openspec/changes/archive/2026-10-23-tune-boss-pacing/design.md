# Design

## Context

The boss kinds are data in `BossCatalog` (`MaxHealth`, `MoveSpeed`, `ContactDamage`, a basic attack and two skills each); the standard kind builds its attack and skills from the shared constants in `BossAttack` and `BossSkills`, the giant and the sniper write their own. The boss's health reaches the clients through the survivor-health component `PlayerCombat`, whose replicated health and maximum health are 16-bit (`NetworkVariable<ushort>`), so a boss above 65535 shows as 65535 (a known limit). The match harness runs whole matches with bots and takes value overrides by name (`bossHealth`, `bossMoveSpeed`, `bossShotDamage`, `bossSkillDamage`, ...); `bossSkillDamage` replaces the damage of both skills with one number.

What the harness measured (normal bots, 20 matches per row, see proposal.md and the exploration): speed is the master lever; with the boss at the survivors' speed every setting collapses in about 30 s, and with it a little slower the matches last 11 to 14 minutes. Health around 150000 to 250000 puts the end of the match at 10 to 14 minutes. The sniper collapses at any damage once it is faster than about 5.

## Goals / Non-Goals

**Goals:**
- New health, speed and (for the sniper) damage values that give each kind long matches decided late under the harness's normal bots, and a rule in the data that keeps every boss slower than every survivor.
- A health value that reaches the clients exactly for any health the data can give.
- The values and how they were reached recorded, so a later change can compare with them.

**Non-Goals:**
- Changing the damage, cooldowns or skills of the standard and giant bosses, mobs, summons, devices, survivor growth or experience.
- Making the bots better (kiting, avoiding skill warnings). The values are for the bots that exist; hand play decides the rest.
- A per-skill damage in the harness beyond what tuning the sniper needs.

## Decisions

**1. The values are found with the harness before they are written into the data.**
Each candidate is tried as a settings file of overrides (no code change), with 40 matches per skill profile and the three profiles, starting from the exploration's values:

| kind | speed | health | other |
|---|---|---|---|
| standard | 6 to 4.5 | 6000 to 190000 | none |
| giant | 4.5 to 4.0 | 9000 to 220000 | none |
| sniper | 8 to 5.0 | 4500 to 130000 | basic attack 12 to 4, beam 45 to 25 |

A kind is accepted when, with the normal profile, the survivors win 35% to 70% of the matches, the median length is 480 s to 840 s and the boss reaches half health in at least half of the matches. The weak and strong profiles are reported, not gated (they bracket the result). Because the matches are bimodal near the edge (either a wipe within two minutes or a long match), each accepted value is also run at 10% faster and 20% more health to see how far the plateau reaches, and the chosen value sits in the middle of it. The runs are kept as summaries (`tools/harness/pacing-*.json`) with the settings that made them.
Alternative: pick the numbers by hand play only. Rejected: the cliff between the two kinds of match is narrow and a hand play of a few matches cannot find it; the harness can.

**2. The data change is small and stays in `BossCatalog`.**
`MaxHealth` and `MoveSpeed` of the three kinds, and the sniper's `BaseDamage` and beam `Damage`. The standard kind keeps taking its attack and skills from the shared constants, which do not change. The comments that call the standard kind "the boss before kinds existed" are rewritten. `-bsBossHealth` (the development argument that replaces the health) keeps working and the README no longer calls values above 65535 a limit.

**3. The speed rule is a test on the data, and the outrun is a test on the simulation.**
A data test checks that the speed of each kind is below the speed of every survivor character and that giant < standard < sniper, so a later change that makes a boss as fast as a survivor fails. A second test plays the survivor and the boss with the harness session (the survivor walks straight away, the boss straight after it) and checks that the distance grows for every kind and every character. The health scenario of the spec ("at least a thousand times the damage of a basic attack in a second") is a data test with the damage taken from the characters' attack data.

**4. The health is replicated as a 32-bit value.**
`PlayerCombat`'s `health` and `maxHealth` become `NetworkVariable<int>` and `ServerWrite` clamps to the `int` range instead of `ushort`. The survivors use the same component, so their health value also grows from 2 to 4 bytes; a network variable sends only a change, so the traffic stays far under the budget (checked in the measurements). The boss's health bar and the number in the HUD read the same properties and need no change.
Alternative: replicate the boss's health as a percentage. Rejected: the HUD shows the number, and a late joiner needs the exact value.
Alternative: a separate variable only for the boss. Rejected: two paths for one value; the widening costs nothing.

**5. The harness gets two overrides for the skills one at a time.**
`bossSkillQDamage` and `bossSkillEDamage` set the damage of the first and the second skill of the boss's data copy, so the sniper's beam (E) can be tuned without changing its ring (Q). `bossSkillDamage` stays as it is.

## Risks / Trade-offs

- [The numbers are right for the bots and wrong for people, who dodge and kite better, so the real game is easier] → say so in the README and the measurements; hand play of a few matches after this change decides a follow-up; the baseline summaries make that follow-up a comparison instead of a restart.
- [The result is bimodal, so a small change in speed or damage flips a kind from balanced to a wipe or a walkover] → the plateau check of decision 1 and a data test that keeps the speeds below the survivors; a note in the README that speed is the lever that moves everything.
- [Survivors fall and are revived 15 to 20 times a match under the bots] → accepted as the intended tension (decided with the user); reported in the measurements.
- [The sniper's basic attack at about 4 damage may feel like nothing] → the beam carries the damage, its warning time stays, and the number is the first thing to adjust after hand play.
- [Widening the replicated health changes the wire format of the survivors' health] → host and clients are built from the same project, so there is no mixed-version play; the late-joiner and the multi-process comparisons check it.

## Open Questions

- Whether the experience from summoned mobs (the boss's own summons feed the survivors' growth) should change is left to the hand play; the harness shows it matters (without summons a 200000-health boss is not beaten in 15 minutes).
