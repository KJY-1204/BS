# Proposal

## Why

When a match ends the result screen says who won and why and how much time was left, and nothing else. After a match of up to fifteen minutes the players cannot see how it went: who grew how far, how often survivors fell and were revived, how much of the boss's health was left, whether the boss's devices mattered. The server already holds most of this (levels, the boss's health, the devices' counters), and the harness already measures the rest from outside, so a short summary on the result screen costs little and gives the end of a match something to read.

## What Changes

- When a match with a boss ends, the server builds a summary and tells every client once: how long the match lasted, the boss's health left and its maximum, the devices placed, destroyed and expired, and for each survivor still in the match the character, the role, the level reached, how many times it fell, how many times it was revived and whether it stands.
- The server counts the falls and the revivals of each survivor as they happen (two new counters in the survivors' simulation).
- The result screen shows the summary below the winner and the reason: one line per survivor (character and role, level, falls, revivals, standing or fallen) and one line for the match (length, boss health left as a percentage, devices placed and destroyed). Every client, the boss player included, sees the same numbers.
- The summary is sent as one small message when the match ends, not as a stream; it is dropped when the room goes back to waiting or a new match starts. A match without a boss has no result and no summary.
- Survivors who left during the match are not listed. Player names do not exist in the game yet, so a line names the character and the role, not a person.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `match-outcome`: a finished match also shows how it went (a summary of the match and of each survivor), the same to everyone.

## Impact

- Code: `SurvivorSim` (the two counters), a plain `MatchSummary` and its builder on `MatchWorld`, a plain byte codec for it, `RoleTable` (build and send once when the match finishes, keep the received summary on every client, clear it when the room leaves the finished state), `MatchHud` (the table on the result screen).
- Network: one new reliable message sent once per match (a few dozen bytes); no change to any stream.
- Tests: EditMode tests for the counters, the builder and the codec; a multi-process run for each way a match ends and for the second match.
- Not in scope: damage dealt, kills and other credit per player (it needs the source of every damage path), player names, a history of earlier matches, saving the summary to a file.
