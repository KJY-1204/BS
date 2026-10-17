using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Runs the boss's summoning on the server: refills the energy while a match with a boss is running, carries out the boss
    /// player's summon and order requests, sends the new mobs' spawn events right away, and keeps the boss's own copy of the
    /// energy and the standing order up to date. Everything is cleared when a match ends.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class SummonServer : MonoBehaviour
    {
        public static SummonServer Instance { get; private set; }

        public SummonSim Sim { get; private set; }

        readonly List<SurvivorInfo> survivors = new List<SurvivorInfo>();
        NetworkManager nm;
        bool running;
        float statsTimer;

        void Awake()
        {
            Instance = this;
            Sim = new SummonSim(MakeConfig());
        }

        static SummonConfig MakeConfig()
        {
            SummonConfig config = SummonConfig.Default;
            if (DevArgs.SummonEnergy > 0)
            {
                config.MaxEnergy = DevArgs.SummonEnergy;
                config.StartEnergy = DevArgs.SummonEnergy;
            }
            return config;
        }

        void Start()
        {
            nm = NetworkManager.Singleton;
            nm.OnServerStarted += HandleServerStarted;
            nm.OnServerStopped += HandleServerStopped;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (nm == null) return;
            nm.OnServerStarted -= HandleServerStarted;
            nm.OnServerStopped -= HandleServerStopped;
            if (running) nm.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        void HandleServerStarted()
        {
            running = true;
            Sim.Reset();
            nm.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        void HandleServerStopped(bool wasHost)
        {
            if (running) nm.OnClientDisconnectCallback -= HandleClientDisconnected;
            running = false;
            Sim.Reset();
        }

        void HandleClientDisconnected(ulong clientId) => Sim.OnPlayerLeft(clientId);

        /// <summary>A match is starting: fresh energy and no order.</summary>
        public void BeginMatch()
        {
            Sim.Reset();
            if (DevArgs.MobLog) Debug.Log($"[BS] summon: match start energy={Sim.Energy:F1}/{Sim.Config.MaxEnergy:F0}");
        }

        /// <summary>A match ended but the room goes on: the summon state starts fresh next time.</summary>
        public void ResetMatch() => Sim.Reset();

        void FixedUpdate()
        {
            if (!running) return;

            CombatServer combat = CombatServer.Instance;
            bool bossThere = combat != null && combat.Boss.Exists;
            Sim.Step(Time.fixedDeltaTime, RoleTable.MatchRunning && bossThere);

            PlayerAvatar boss = FindBoss();
            PlayerSummon view = boss != null ? boss.GetComponent<PlayerSummon>() : null;
            if (view != null && view.IsSpawned) view.ServerWrite(Sim.Energy, Sim.Config.MaxEnergy, Sim.Order, Sim.OrderClient, Sim.OrderPoint);

            if (DevArgs.MobLog && RoleTable.MatchRunning && bossThere)
            {
                statsTimer += Time.fixedDeltaTime;
                if (statsTimer >= 5f) { statsTimer = 0f; LogStats(); }
            }
        }

        /// <summary>Dev log: how close summoned mobs are, on average, to each survivor (to see an order working).</summary>
        void LogStats()
        {
            MobSim mobs = MobServer.Instance != null ? MobServer.Instance.Sim : null;
            if (mobs == null) return;

            PlayerRegistry.CollectSurvivors(survivors);
            var sb = new System.Text.StringBuilder();
            int summoned = mobs.SummonedCount;
            int underOrder = 0;
            for (int i = 0; i < mobs.Count; i++)
                if (mobs.IsSummoned(i) && mobs.TryGetOverride(mobs.IdAt(i), out _, out _, out _)) underOrder++;
            for (int s = 0; s < survivors.Count; s++)
            {
                double total = 0;
                for (int i = 0; i < mobs.Count; i++)
                    if (mobs.IsSummoned(i)) total += Vector2.Distance(mobs.PositionAt(i), survivors[s].Position);
                sb.Append($" client{survivors[s].ClientId}={(summoned > 0 ? total / summoned : 0):F1}");
            }
            Debug.Log($"[BS] summon stats: order={Sim.Order} orderClient={Sim.OrderClient} energy={Sim.Energy:F1} summoned={summoned} underOrder={underOrder} meanDistance:{sb}");
        }

        // ---- Requests from the boss player ----

        public void OnSummonRequest(ulong clientId)
        {
            if (!running) return;

            PlayerAvatar boss = FindBoss();
            bool fromBoss = boss != null && boss.OwnerClientId == clientId;
            if (!TryMakeContext(boss, out SummonContext ctx)) { Tell(boss, SummonResult.Unavailable, fromBoss); return; }

            SummonResult result = Sim.Summon(ctx, fromBoss, out int created);
            if (created > 0) MobServer.Instance?.FlushEvents(); // the spawn events go out in the same tick as the mobs

            if (DevArgs.MobLog)
                Debug.Log($"[BS] summon: result={result} created={created} energy={Sim.Energy:F1}/{Sim.Config.MaxEnergy:F0} " +
                          $"mobs={ctx.Mobs.Count} auto={ctx.Mobs.AutoCount} summoned={ctx.Mobs.SummonedCount} fromBoss={fromBoss}");

            if (result != SummonResult.Created) Tell(boss, result, fromBoss);
        }

        public void OnOrderRequest(ulong clientId, Vector2 pointer)
        {
            if (!running) return;

            PlayerAvatar boss = FindBoss();
            bool fromBoss = boss != null && boss.OwnerClientId == clientId;
            if (!TryMakeContext(boss, out SummonContext ctx)) return;

            bool ok = Sim.GiveOrder(ctx, fromBoss, pointer);
            if (DevArgs.MobLog)
                Debug.Log($"[BS] order: accepted={ok} kind={Sim.Order} client={Sim.OrderClient} point=({Sim.OrderPoint.x:F1},{Sim.OrderPoint.y:F1}) fromBoss={fromBoss}");
        }

        public void OnClearRequest(ulong clientId)
        {
            if (!running) return;

            PlayerAvatar boss = FindBoss();
            bool fromBoss = boss != null && boss.OwnerClientId == clientId;
            if (!TryMakeContext(boss, out SummonContext ctx)) return;

            bool ok = Sim.ClearOrder(ctx, fromBoss);
            if (DevArgs.MobLog) Debug.Log($"[BS] order: cleared={ok} kind={Sim.Order} fromBoss={fromBoss}");
        }

        // ---- Helpers ----

        static PlayerAvatar FindBoss()
        {
            for (int i = 0; i < PlayerAvatar.All.Count; i++)
            {
                PlayerAvatar a = PlayerAvatar.All[i];
                if (a != null && a.IsSpawned && a.Role == RoleTable.BossRole) return a;
            }
            return null;
        }

        bool TryMakeContext(PlayerAvatar boss, out SummonContext ctx)
        {
            ctx = default;
            MobServer mobs = MobServer.Instance;
            CombatServer combat = CombatServer.Instance;
            if (mobs == null || combat == null || boss == null) return false;

            PlayerRegistry.CollectSurvivors(survivors);
            ctx = new SummonContext
            {
                Mobs = mobs.Sim,
                BossPosition = boss.transform.position,
                BossStanding = combat.Boss.Exists && !combat.Boss.HasFallen,
                MatchRunning = RoleTable.MatchRunning,
                Survivors = survivors,
            };
            return true;
        }

        static void Tell(PlayerAvatar boss, SummonResult result, bool fromBoss)
        {
            if (!fromBoss || boss == null) return;
            PlayerSummon view = boss.GetComponent<PlayerSummon>();
            if (view != null && view.IsSpawned) view.ServerSendResult(result);
        }
    }
}
