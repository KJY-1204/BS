using System;
using System.Collections.Generic;

namespace BS
{
    /// <summary>One value to try in place of the game's own: a name from <see cref="HarnessOverrides"/> and the number.</summary>
    [Serializable]
    public class NamedValue
    {
        public string name;
        public float value;
    }

    /// <summary>
    /// What a harness run plays: which boss, how skilled the bots are, which characters the survivors are, how many matches from which
    /// seed, and which values to change. Plain public fields so it can be written as JSON.
    /// </summary>
    [Serializable]
    public class HarnessSettings
    {
        /// <summary>1 standard, 2 giant, 3 sniper (see <see cref="BossCatalog"/>).</summary>
        public int bossKind = BossCatalog.Standard;
        /// <summary>The bots' skill: weak, normal or strong.</summary>
        public string profile = "normal";
        /// <summary>The character of each survivor, one to four of Reimu 1, Marisa 2, Sakuya 3, Sanae 4, each at most once.</summary>
        public int[] characters = { CharacterIds.Reimu, CharacterIds.Marisa, CharacterIds.Sakuya, CharacterIds.Sanae };
        public int matches = 1;
        public int firstSeed = 1;
        /// <summary>The length of one step in seconds. The server's is 0.02; any other value is a coarser (or finer) run and the report says so.</summary>
        public float step = HarnessSettings.ServerStep;
        public List<NamedValue> overrides = new List<NamedValue>();
        /// <summary>An earlier summary (summary.json) to compare this run with. Empty for none.</summary>
        public string baselinePath = "";

        public const float ServerStep = 0.02f;
        public const int MaxMatches = 10000;

        /// <summary>Null when the settings can be played, otherwise what is wrong with them.</summary>
        public string Validate()
        {
            if (matches < 1 || matches > MaxMatches) return $"matches must be from 1 to {MaxMatches}, not {matches}";
            if (!BossCatalog.IsValid((byte)Math.Clamp(bossKind, 0, 255))) return $"bossKind {bossKind} does not exist";
            if (!(step >= 0.005f && step <= 0.1f)) return $"step must be from 0.005 to 0.1 seconds, not {step}";
            if (!SkillProfiles.IsKnown(profile)) return $"unknown profile '{profile}' (weak, normal or strong)";

            if (characters == null || characters.Length < 1 || characters.Length > 4) return "characters must name one to four survivors";
            var seen = new HashSet<int>();
            foreach (int c in characters)
            {
                if (c < CharacterIds.Reimu || c > CharacterIds.Sanae) return $"character {c} does not exist";
                if (!seen.Add(c)) return $"character {c} is chosen twice";
            }

            if (overrides != null)
            {
                foreach (NamedValue o in overrides)
                {
                    if (o == null || string.IsNullOrEmpty(o.name)) return "an override has no name";
                    if (!HarnessOverrides.IsKnown(o.name)) return $"unknown override '{o.name}' (known: {string.Join(", ", HarnessOverrides.Names)})";
                    if (float.IsNaN(o.value) || float.IsInfinity(o.value)) return $"override '{o.name}' is not a number";
                    if (o.name == "bossPhases" && o.value != 0f && o.value != 1f) return "override 'bossPhases' must be 0 or 1";
                    if (o.name == "bossTraits" && o.value != 0f && o.value != 1f) return "override 'bossTraits' must be 0 or 1";
                    if (o.name == "rescuePoints" && o.value != 0f && o.value != 1f) return "override 'rescuePoints' must be 0 or 1";
                    if (o.name == "rescueContest" && o.value != 0f && o.value != 1f) return "override 'rescueContest' must be 0 or 1";
                    if (o.name == "startQRank" && (o.value < 0f || o.value > 3f || o.value != Math.Floor(o.value))) return "override 'startQRank' must be 0, 1, 2 or 3";
                    if ((o.name == "startERank" || o.name == "startRRank") && (o.value < 0f || o.value > 3f || o.value != Math.Floor(o.value))) return $"override '{o.name}' must be 0, 1, 2 or 3";
                    if (o.name == "startEvolutionSlot" && (o.value < 0f || o.value > 3f || o.value != Math.Floor(o.value))) return "override 'startEvolutionSlot' must be 0 (Q), 1 (E), 2 (R) or 3 (all three)";
                    if (o.name == "startEvolutionRole" && (o.value < 0f || o.value > 4f || o.value != Math.Floor(o.value))) return "override 'startEvolutionRole' must be 0 to 4";
                    if (o.name == "startEvolution" && (o.value < 0f || o.value > 2f || o.value != Math.Floor(o.value))) return "override 'startEvolution' must be 0, 1 or 2";
                }
            }
            return null;
        }
    }

    /// <summary>The values a harness run can change, by name. A boss value changes the copy of the boss's data the match uses, never the catalog.</summary>
    public static class HarnessOverrides
    {
        sealed class Entry
        {
            public Action<BossDef, float> OnBoss;
            public Action<MatchWorld, float> OnWorld;
        }

        static readonly Dictionary<string, Entry> table = new Dictionary<string, Entry>
        {
            ["bossHealth"] = new Entry { OnBoss = (b, v) => b.MaxHealth = (int)v },
            ["bossMoveSpeed"] = new Entry { OnBoss = (b, v) => b.MoveSpeed = v },
            ["bossContactDamage"] = new Entry { OnBoss = (b, v) => b.ContactDamage = (int)v },
            ["bossShotDamage"] = new Entry { OnBoss = (b, v) => b.Attack.BaseDamage = (int)v },
            ["bossShotCooldown"] = new Entry { OnBoss = (b, v) => b.Attack.CooldownSeconds = v },
            ["matchSeconds"] = new Entry { OnBoss = (b, v) => b.MatchSeconds = v },
            // The damage of one skill of the boss's data copy (Q is the first, E the second), apart from the other.
            ["bossSkillQDamage"] = new Entry { OnBoss = (b, v) => b.Skills[0].Damage = (int)v },
            ["bossSkillEDamage"] = new Entry { OnBoss = (b, v) => { if (b.Skills.Length > 1) b.Skills[1].Damage = (int)v; } },
            ["bossSkillDamage"] = new Entry { OnWorld = (w, v) => w.BossSkills.BaseDamageOverride = (int)v },
            ["mobHealth"] = new Entry { OnWorld = (w, v) => w.Mobs.MaxHealth = (int)v },
            ["mobSpeed"] = new Entry { OnWorld = (w, v) => w.Mobs.Speed = v },
            ["mobAutoTarget"] = new Entry { OnWorld = (w, v) => w.Mobs.TargetCount = Math.Clamp((int)v, 0, w.Mobs.HardCap) },
            ["mobSpawnPerSecond"] = new Entry { OnWorld = (w, v) => w.Mobs.SpawnPerSecond = v },
            ["summonMaxEnergy"] = new Entry { OnWorld = (w, v) => { w.Summon.Config.MaxEnergy = v; w.Summon.Config.StartEnergy = Math.Min(w.Summon.Config.StartEnergy, v); } },
            ["summonRegen"] = new Entry { OnWorld = (w, v) => w.Summon.Config.RegenPerSecond = v },
            ["summonCostPerMob"] = new Entry { OnWorld = (w, v) => w.Summon.Config.CostPerMob = v },
            ["deviceEnergy"] = new Entry { OnWorld = (w, v) => w.Mobs.DeviceSettings.EnergyCost = v },
            ["deviceMax"] = new Entry { OnWorld = (w, v) => w.Mobs.DeviceSettings.MaxDevices = (int)v },
            ["deviceInterval"] = new Entry { OnWorld = (w, v) => w.Mobs.DeviceSettings.ProductionIntervalSeconds = v },
            ["deviceLifetime"] = new Entry { OnWorld = (w, v) => w.Mobs.DeviceSettings.LifetimeSeconds = v },
            ["deviceMobs"] = new Entry { OnWorld = (w, v) => w.Mobs.DeviceSettings.MobsPerBatch = (int)v },
            // 0 keeps the match in phase 1 (no phase burst, the boss's third skill stays locked); 1 is the default.
            ["bossPhases"] = new Entry { OnWorld = (w, v) => w.BossPhasesEnabled = v != 0f },
            // 0 turns the boss kind's trait off (Steady, Momentum); 1 is the default.
            ["bossTraits"] = new Entry { OnWorld = (w, v) => w.Boss.TraitsEnabled = v != 0f },
            // 0 opens no rescue point; 1 is the default.
            ["rescuePoints"] = new Entry { OnWorld = (w, v) => w.RescuePoint.Enabled = v != 0f },
            // 0 lets the boss stand in a rescue point without stopping its capture; 1 is the default.
            ["rescueContest"] = new Entry { OnWorld = (w, v) => w.RescuePoint.ContestEnabled = v != 0f },
            ["survivorHealthBonus"] = new Entry { OnWorld = (w, v) => w.SurvivorHealthBonus = (int)v },
            // Every survivor starts with their Q skill at this rank (0 to 3) and with this evolution of it (0 none, 1 or 2).
            ["startQRank"] = new Entry { OnWorld = (w, v) => w.StartQRank = (int)v },
            ["startEvolution"] = new Entry { OnWorld = (w, v) => w.StartEvolution = (int)v },
            // The rank of the E and R skills every survivor starts with (0 to 3), and the slot startEvolution evolves (0 Q, 1 E, 2 R, 3 all three).
            ["startERank"] = new Entry { OnWorld = (w, v) => w.StartERank = (int)v },
            ["startRRank"] = new Entry { OnWorld = (w, v) => w.StartRRank = (int)v },
            ["startEvolutionSlot"] = new Entry { OnWorld = (w, v) => w.StartEvolutionSlot = (int)v },
            // With startEvolution: only the survivor of this role (1 to 4) gets it; 0 gives it to everyone.
            ["startEvolutionRole"] = new Entry { OnWorld = (w, v) => w.StartEvolutionRole = (int)v },
            ["xpPerKill"] = new Entry { OnWorld = (w, v) => w.Leveling.XpPerKill = (int)v },
            ["xpPerDevice"] = new Entry { OnWorld = (w, v) => w.Leveling.XpPerDevice = (int)v },
        };

        public static IEnumerable<string> Names => table.Keys;

        public static bool IsKnown(string name) => name != null && table.ContainsKey(name);

        public static void ApplyToBoss(IEnumerable<NamedValue> overrides, BossDef def)
        {
            if (overrides == null) return;
            foreach (NamedValue o in overrides)
                if (table.TryGetValue(o.name, out Entry e) && e.OnBoss != null) e.OnBoss(def, o.value);
        }

        public static void ApplyToWorld(IEnumerable<NamedValue> overrides, MatchWorld world)
        {
            if (overrides == null) return;
            foreach (NamedValue o in overrides)
                if (table.TryGetValue(o.name, out Entry e) && e.OnWorld != null) e.OnWorld(world, o.value);
        }
    }
}
