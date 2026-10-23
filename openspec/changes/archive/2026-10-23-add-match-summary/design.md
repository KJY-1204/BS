# Design

## Context

When the clock decides a match, `RoleTable.FinishMatch` sets the winner, the reason and the room phase (replicated values), and `MatchHud.DrawResult` draws the winner, the reason and the time left. Everything a summary needs is in the server's `MatchWorld` at that moment: the clock (`Clock.TimeLimit`, `TimeLeft`), the boss (`Boss.Health`, `MaxHealth`), each survivor in `Survivors` (alive), `Leveling` (level, character) and `Slots` (role), and the device counters of the mob simulation (`DevicesPlaced`, `DevicesDestroyed`, `DevicesExpired`, added for the harness). What is missing is a count of the falls and the revivals: `SurvivorSim` changes a survivor's health to zero in `ApplyDamage` (and in the development `Kill`) and brings it back in the revival step, and records neither. A survivor who leaves is unregistered from the survivors, the growth and the slots, so nothing is left to list for them. See proposal.md for what the change adds.

## Goals / Non-Goals

**Goals:**
- A summary built from the world's state at the moment the match is decided, sent once to every client and drawn under the result.
- Counts of falls and revivals that cannot be wrong by construction (counted where the state changes).
- A plain, tested codec so the format is checked without a network.

**Non-Goals:**
- Per-player damage, kills or other credit; player names; a history; a file.
- Changing the harness's recorder to use the new counters (it counts the same things from outside and stays as it is).

## Decisions

**1. The counters live in `SurvivorSim` and count transitions, not observations.**
`State` gets `Falls` and `Revives`. `ApplyDamage` adds a fall when the damage takes the health from above zero to zero, `Kill` (the development helper) does the same when the survivor stood, and the revival step adds a revival when the gauge completes. `Register` and `Clear` reset them. Accessors `FallsOf(id)` and `RevivesOf(id)` return zero for an unknown survivor.
Alternative: count by watching who is alive each tick, as the harness's recorder does. Rejected: a fall and a revival inside one tick would be missed, and the server can count exactly where the change is made.

**2. `MatchSummary` is plain data built by `MatchWorld`.**
`MatchWorld.BuildSummary()` returns the length in seconds (`TimeLimit - TimeLeft`, rounded), the boss's current and maximum health, the devices placed/destroyed/expired, and for each registered survivor in the order of their roles: role, character, level, falls, revivals and alive. Only survivors the world still knows are listed, so a survivor who left is absent without any special case. A match without a boss (clock disabled) returns no summary.
Alternative: build it in `RoleTable` from the replicated values. Rejected: the replicated values do not carry falls or revivals and the logic would not be testable without a scene.

**3. A small byte codec, `MatchSummaryCodec`.**
Little-endian, no version byte (host and clients are built together): length `u16` seconds, boss health `i32`, boss maximum `i32`, devices placed, destroyed and expired `u16` each, survivor count `u8`, then per survivor role `u8`, character `u8`, level `u8`, falls `u16`, revivals `u16`, alive `u8`. A reader rejects a payload that is too short or whose count does not fit it and gives no summary. 4 survivors take 17 + 4 x 8 = 49 bytes.
Alternative: replicate the numbers as network variables. Rejected: a one-time snapshot of a variable number of lines fits a message better than a list that the clients must keep in step.

**4. One reliable RPC from the server to every client, sent when the match is finished.**
`RoleTable.FinishMatch` builds the summary, writes it with the codec and calls `SummaryRpc(byte[])` with `SendTo.ClientsAndHost`. Each machine decodes it into `RoleTable.Summary` (null when there is none). A client clears it when the room phase becomes anything but finished (the phase value's change callback), so a returned room and a new match start without one. The server's own copy is cleared the same way. A log line at the receiver (`[BS] summary received`) with the numbers makes the multi-process runs checkable.
The result screen is drawn from the replicated phase and the summary when it has arrived; if the message comes a moment after the phase value, the table appears when it does.
Alternative: send the summary with the room's phase value. Rejected: a phase is one byte and the summary is not.

**5. The table is drawn by `MatchHud` under the winner and the reason.**
The result box grows to hold up to four lines of survivors and a line of the match: `character (role)  Lv N  falls n  revivals n  standing/fallen`, then `match 12:34   boss health left 38%   devices placed 4 / destroyed 3`. The boss health shown is the percentage and, in the line of the boss, the number of the maximum for readability. The host's button and the waiting text move below it. Texts are in Korean like the rest of the screen.

## Risks / Trade-offs

- [The message arrives before or after the phase change and the table flickers or is missing for a frame] → the result is drawn from the phase and the table when present; a test of the codec plus a multi-process run that finishes matches on every machine check that the table is there once the result is.
- [A fall is counted that the match then undoes (a revive in the same moment as the match decision)] → the summary takes the counters at the moment the clock decides, as the spec says, and the counters change only in the combat phase, which has run before the clock phase of the same step.
- [`Kill` in the development scenarios adds falls that no damage caused] → intended: a fallen survivor is a fall, and the scenario's use is only testing.
- [A survivor who left and came back to a waiting room is not in the table] → nobody can rejoin a running match, so a survivor listed is one that stayed.

## Open Questions

- Whether the table should also show the devices that expired (the summary carries it; the screen shows placed and destroyed only).
