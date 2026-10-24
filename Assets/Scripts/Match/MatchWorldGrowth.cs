using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>A survivor's request to use the skill in a slot (Q is 0, E is 1).</summary>
    public struct SkillRequest
    {
        public ulong ClientId;
        public int Slot;
    }

    /// <summary>What happened to one skill request in the growth phase: the answer for its owner and, when the skill fired, what it did.</summary>
    public struct SkillOutcome
    {
        public ulong ClientId;
        public int Slot;
        public SkillResult Result;
        /// <summary>Set when <see cref="Result"/> is <see cref="SkillResult.Fired"/>.</summary>
        public SkillActivation Activation;
        public Vector2 Where;
        public ushort Aim;
        public int Touched;
    }

    /// <summary>What the growth phase did, for the server's statistics and for the mob events it must flush.</summary>
    public struct GrowthStepResult
    {
        /// <summary>Mob kills and destroyed devices that gave experience.</summary>
        public int KillsHandled;
        /// <summary>A skill damaged or removed mobs, so their removal events should go out now.</summary>
        public bool AreaDamage;
    }

    /// <summary>The growth part of a match: experience, levels, skill choices, mana, key skills and the stats that follow from them.</summary>
    public sealed partial class MatchWorld
    {
        /// <summary>The most skill requests one step takes (the server refuses more).</summary>
        public const int MaxSkillRequestsPerStep = 64;

        readonly Dictionary<ulong, SurvivorStats> stats = new Dictionary<ulong, SurvivorStats>();
        readonly List<ulong> growthIds = new List<ulong>();
        readonly List<SkillRequest> skillRequests = new List<SkillRequest>();
        readonly List<Vector2> kills = new List<Vector2>();
        readonly List<Vector2> deviceKills = new List<Vector2>();
        readonly List<GrowthBody> growthBodies = new List<GrowthBody>();
        readonly List<GrowthBody> skillBodies = new List<GrowthBody>();
        readonly List<GrowthEvent> drained = new List<GrowthEvent>();

        /// <summary>The rank of the Q skill every survivor starts with (the harness override startQRank), 0 for none.</summary>
        public int StartQRank { get; set; }

        /// <summary>The evolution of the Q skill every survivor starts with, 1 or 2 (the harness override startEvolution), 0 for none.</summary>
        public int StartEvolution { get; set; }

        /// <summary>When above zero, only the survivor with this role (1 to 4) starts with the evolution (the harness override startEvolutionRole); zero gives it to every survivor.</summary>
        public int StartEvolutionRole { get; set; }

        /// <summary>The rank of the E and R skills every survivor starts with (the harness overrides startERank and startRRank), 0 for none.</summary>
        public int StartERank { get; set; }
        public int StartRRank { get; set; }

        /// <summary>The slot whose skill <see cref="StartEvolution"/> evolves: 0 the Q skill (the default), 1 the E skill, 2 the R skill, 3 all three (the harness override startEvolutionSlot).</summary>
        public int StartEvolutionSlot { get; set; }

        /// <summary>Gives a survivor the Q, E and R skill ranks and the evolution this world starts them with.</summary>
        public void GrantStartSkills(ulong clientId, byte character)
        {
            int[] starts = { StartQRank, StartERank, StartRRank };
            for (int slot = 0; slot < SkillCatalog.SkillSlots; slot++)
            {
                SkillDef baseSkill = SkillCatalog.ForSlot(slot, character);
                if (baseSkill != null && starts[slot] > 0) Leveling.GrantRank(clientId, baseSkill.Id, System.Math.Min(starts[slot], baseSkill.MaxRank));
            }
            if (StartEvolutionRole > 0 && SlotOf(clientId) != StartEvolutionRole) return;
            for (int slot = 0; slot < SkillCatalog.SkillSlots; slot++)
            {
                if (StartEvolutionSlot != SkillCatalog.SkillSlots && StartEvolutionSlot != slot) continue;
                SkillDef evolution = SkillCatalog.EvolutionOf(SkillCatalog.ForSlot(slot, character), StartEvolution);
                if (evolution != null) Leveling.GrantRank(clientId, evolution.Id, 1);
            }
        }

        /// <summary>Added to every survivor's maximum health (the development argument that replaces the starting health). Zero in a real match.</summary>
        public int SurvivorHealthBonus { get; set; }

        /// <summary>The skill requests of the latest growth phase and what came of them, in order.</summary>
        public List<SkillOutcome> SkillOutcomes { get; } = new List<SkillOutcome>();

        /// <summary>The skill events of the latest growth phase (one per skill that fired). The server publishes them.</summary>
        public List<CombatEvent> SkillEvents { get; } = new List<CombatEvent>();

        /// <summary>The level ups and skill choices of the latest growth phase, for survivors that have growth. The server tells their owners.</summary>
        public List<GrowthEvent> GrowthEvents { get; } = new List<GrowthEvent>();

        public GrowthStepResult LastGrowth { get; private set; }

        public IReadOnlyList<ulong> GrowthIds => growthIds;

        public bool HasGrowth(ulong clientId) => stats.ContainsKey(clientId);

        /// <summary>The stats of a survivor from its level and skills (the default values for one without growth).</summary>
        public SurvivorStats StatsOf(ulong clientId) => stats.TryGetValue(clientId, out SurvivorStats s) ? s : default;

        /// <summary>
        /// Starts a survivor's growth at the first level. <paramref name="beforeStats"/> runs once the survivor is known to the leveling and
        /// before its stats are worked out (the development grant of skills).
        /// </summary>
        public void StartGrowth(ulong clientId, byte character, System.Action<ulong> beforeStats = null)
        {
            Leveling.Register(clientId, character);
            beforeStats?.Invoke(clientId);

            SurvivorStats first = StatCalc.Compute(1, Leveling.Ranks(clientId), CharacterCatalog.Get(character));
            stats[clientId] = first;
            if (!growthIds.Contains(clientId)) growthIds.Add(clientId);
            Skills.Register(clientId, first.MaxMana, first.ManaRegenPerSecond);
            ApplyStats(clientId);
        }

        public void StopGrowth(ulong clientId)
        {
            stats.Remove(clientId);
            growthIds.Remove(clientId);
            Leveling.Unregister(clientId);
            Skills.Unregister(clientId);
        }

        /// <summary>Forgets every survivor's growth: a new room starts everyone from the beginning.</summary>
        public void ClearGrowth()
        {
            stats.Clear();
            growthIds.Clear();
            Leveling.Clear();
            Skills.Clear();
            skillRequests.Clear();
            kills.Clear();
            SkillOutcomes.Clear();
            SkillEvents.Clear();
            GrowthEvents.Clear();
        }

        /// <summary>A key skill request. Returns false when this step already holds the most it takes.</summary>
        public bool RequestSkill(ulong clientId, int slot)
        {
            if (skillRequests.Count >= MaxSkillRequestsPerStep) return false;
            skillRequests.Add(new SkillRequest { ClientId = clientId, Slot = slot });
            return true;
        }

        /// <summary>A survivor's choice of a skill from its offer.</summary>
        public bool Pick(ulong clientId, byte offerId, int index) => Leveling.Pick(clientId, offerId, index);

        /// <summary>
        /// Kills turn into experience, key skills are used, then levels, mana and skill timers advance and the stats follow. The kills
        /// of this step are the shots' (from the combat phase) and those of the skills used here.
        /// </summary>
        public void StepGrowth(float dt)
        {
            kills.Clear();
            kills.AddRange(ShotKills);

            SkillOutcomes.Clear();
            SkillEvents.Clear();
            GrowthEvents.Clear();

            var result = new GrowthStepResult();
            result.AreaDamage = ProcessSkillRequests();
            result.KillsHandled = GiveExperience();

            Leveling.Step(dt);
            Skills.Step(dt);
            EndEffectsOfTheDead();

            ProcessGrowthEvents();
            KeepAttackModifiersCurrent();

            LastGrowth = result;
        }

        // ---- Skills ----

        bool TryPositionOf(ulong clientId, out Vector2 position)
        {
            for (int i = 0; i < Bodies.Count; i++)
            {
                if (Bodies[i].ClientId != clientId || Bodies[i].Role == RoleTable.BossRole) continue;
                position = Bodies[i].Position;
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>Where every survivor with growth stands and whether it is alive.</summary>
        void CollectGrowthBodies(List<GrowthBody> into)
        {
            into.Clear();
            for (int i = 0; i < growthIds.Count; i++)
            {
                ulong id = growthIds[i];
                if (!TryPositionOf(id, out Vector2 position)) continue;
                into.Add(new GrowthBody { ClientId = id, Position = position, Alive = Survivors.IsAlive(id) });
            }
        }

        bool ProcessSkillRequests()
        {
            bool areaDamage = false;

            skillBodies.Clear();
            if (skillRequests.Count > 0) CollectGrowthBodies(skillBodies);

            for (int i = 0; i < skillRequests.Count; i++)
            {
                SkillRequest request = skillRequests[i];
                if (!stats.ContainsKey(request.ClientId) || !TryPositionOf(request.ClientId, out Vector2 where)) continue;

                byte characterId = Leveling.CharacterOf(request.ClientId);
                bool alive = Survivors.IsAlive(request.ClientId);
                SkillResult result = Skills.TryActivate(request.ClientId, characterId, request.Slot, alive, Leveling.Ranks(request.ClientId), out SkillActivation activation);

                var outcome = new SkillOutcome { ClientId = request.ClientId, Slot = request.Slot, Result = result };
                if (result != SkillResult.Fired)
                {
                    SkillOutcomes.Add(outcome);
                    continue;
                }

                ushort aim = Attacks.AimOf(request.ClientId);
                CharacterDef character = CharacterCatalog.Get(characterId);

                var context = new SkillContext
                {
                    CasterId = request.ClientId,
                    Origin = where,
                    AimAngle = aim,
                    Mobs = Mobs,
                    Projectiles = Projectiles,
                    Survivors = Survivors,
                    Boss = Boss.Exists ? Boss : null,
                    Bodies = skillBodies,
                    Shape = character != null ? character.Attack.Shape : ProjectileShape.Default,
                    Deaths = kills,
                };
                int touched = SkillExecutor.Apply(activation, context);
                if (touched > 0) areaDamage = true;

                SkillEvents.Add(new CombatEvent
                {
                    Kind = CombatEventKind.Skill,
                    Owner = request.ClientId,
                    SkillId = activation.SkillId,
                    Rank = (byte)activation.Rank,
                    Position = where,
                    Angle = aim,
                    RangeScale = (byte)activation.RangeScale,
                    DurationScale = (byte)activation.DurationScale,
                });

                outcome.Activation = activation;
                outcome.Where = where;
                outcome.Aim = aim;
                outcome.Touched = touched;
                SkillOutcomes.Add(outcome);
            }

            skillRequests.Clear();
            return areaDamage;
        }

        // ---- Experience, levels, stats ----

        int GiveExperience()
        {
            deviceKills.Clear();
            Mobs.DrainDeviceKills(deviceKills);
            if (kills.Count == 0 && deviceKills.Count == 0) return 0;

            CollectGrowthBodies(growthBodies);
            for (int i = 0; i < kills.Count; i++) Leveling.AwardKill(kills[i], growthBodies);
            for (int i = 0; i < deviceKills.Count; i++) Leveling.AwardDeviceKill(deviceKills[i], growthBodies);
            return kills.Count + deviceKills.Count;
        }

        void EndEffectsOfTheDead()
        {
            for (int i = 0; i < growthIds.Count; i++)
                if (!Survivors.IsAlive(growthIds[i])) Skills.EndBuff(growthIds[i]);
        }

        void ProcessGrowthEvents()
        {
            drained.Clear();
            Leveling.DrainEvents(drained);

            for (int i = 0; i < drained.Count; i++)
            {
                GrowthEvent e = drained[i];
                if (!stats.ContainsKey(e.ClientId)) continue;

                // A level up and a closed offer change what the survivor can do: its stats are worked out again.
                if (e.Kind == GrowthEventKind.LevelUp || e.Kind == GrowthEventKind.OfferClosed) ApplyStats(e.ClientId);
                GrowthEvents.Add(e);
            }
        }

        /// <summary>Works out a survivor's stats and hands them to the simulations that use them.</summary>
        void ApplyStats(ulong clientId)
        {
            SurvivorStats s = StatCalc.Compute(Leveling.Level(clientId), Leveling.Ranks(clientId), CharacterCatalog.Get(Leveling.CharacterOf(clientId)));
            stats[clientId] = s;

            Survivors.SetMaxHealth(clientId, s.MaxHealth + SurvivorHealthBonus);
            Survivors.SetDamageTakenMultiplier(clientId, s.DamageTakenMultiplier);
            Skills.SetStats(clientId, s.MaxMana, s.ManaRegenPerSecond);
            Attacks.SetModifiers(clientId, s.ProjectileDamage, s.CooldownMultiplier * Skills.AttackMultiplier(clientId));
        }

        /// <summary>A running Rapid Fire changes the attack cooldown, so the modifiers are refreshed every step.</summary>
        void KeepAttackModifiersCurrent()
        {
            for (int i = 0; i < growthIds.Count; i++)
            {
                ulong id = growthIds[i];
                SurvivorStats s = stats[id];
                Attacks.SetModifiers(id, s.ProjectileDamage, s.CooldownMultiplier * Skills.AttackMultiplier(id));
            }
        }
    }
}
