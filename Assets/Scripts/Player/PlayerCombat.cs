using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BS
{
    /// <summary>
    /// Survivor health and the owner's aim and fire input. The server owns health and the fire decision;
    /// the owner only reports where it is aiming and whether fire is held.
    /// </summary>
    public class PlayerCombat : NetworkBehaviour
    {
        readonly NetworkVariable<ushort> health = new NetworkVariable<ushort>(
            (ushort)CombatConfig.SurvivorMaxHealth, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ushort> maxHealth = new NetworkVariable<ushort>(
            (ushort)CombatConfig.SurvivorMaxHealth, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> reviveGauge = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly AimSender sender = new AimSender();

        const float BarWidth = 1.1f;
        const float BarHeight = 0.12f;
        // The boss is bigger, so its bar is wider and sits higher.
        const float BossBarWidth = 2.6f;
        const float BossBarHeight = 0.2f;
        const float BossBarY = 1.5f;

        PlayerAvatar avatar;
        ushort aimAngle;
        bool firing;
        float devSweepAngle;

        // Local feedback for the owner: a flash at the muzzle as soon as a shot is due, before the server answers.
        float localCooldown;

        // World-space bars and aim marker (created at runtime)
        SpriteRenderer healthBack, healthFill, reviveBack, reviveFill, aimMarker;

        /// <summary>Time until which the owner's muzzle flash is shown.</summary>
        public float MuzzleFlashUntil { get; private set; }

        public int Health => health.Value;
        /// <summary>This survivor's own maximum health, which can rise as the survivor grows.</summary>
        public int MaxHealth => maxHealth.Value;
        public bool IsDead => IsSpawned && health.Value == 0;
        /// <summary>Revive progress from 0 to 1 while this survivor is down.</summary>
        public float ReviveGauge => reviveGauge.Value / 100f;
        /// <summary>The owner's current aim and fire state, for aim indicators and effects.</summary>
        public ushort AimAngle => aimAngle;
        public bool IsFiring => firing;

        void Awake()
        {
            avatar = GetComponent<PlayerAvatar>();

            healthBack = SpriteFactory.CreateRenderer("HealthBack", transform, new Color(0f, 0f, 0f, 0.6f), 30);
            healthFill = SpriteFactory.CreateRenderer("HealthFill", transform, new Color(0.3f, 0.9f, 0.35f), 31);
            reviveBack = SpriteFactory.CreateRenderer("ReviveBack", transform, new Color(0f, 0f, 0f, 0.6f), 30);
            reviveFill = SpriteFactory.CreateRenderer("ReviveFill", transform, new Color(0.35f, 0.7f, 1f), 31);
            aimMarker = SpriteFactory.CreateRenderer("AimMarker", transform, new Color(1f, 1f, 1f, 0.8f), 25);
            aimMarker.transform.localScale = new Vector3(0.22f, 0.22f, 1f);
            aimMarker.enabled = false;

            healthBack.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            healthBack.transform.localScale = new Vector3(BarWidth + 0.06f, BarHeight + 0.06f, 1f);
            reviveBack.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            reviveBack.transform.localScale = new Vector3(BarWidth + 0.06f, BarHeight + 0.06f, 1f);
        }

        void LateUpdate()
        {
            if (!IsSpawned || avatar == null) return;

            bool survivor = avatar.Role != RoleTable.BossRole;
            bool dead = IsDead;

            healthBack.enabled = healthFill.enabled = true;
            reviveBack.enabled = reviveFill.enabled = survivor && dead;
            aimMarker.enabled = survivor && IsOwner && !dead;

            if (!survivor)
            {
                DrawBossBar();
            }
            else
            {
                float fraction = MaxHealth > 0 ? Mathf.Clamp01(health.Value / (float)MaxHealth) : 0f;
                SetBar(healthFill, 0.85f, fraction);
                healthFill.color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.3f, 0.9f, 0.35f), fraction);

                if (dead) SetBar(reviveFill, 0.62f, ReviveGauge);
            }

            if (aimMarker.enabled)
                aimMarker.transform.position = transform.position + (Vector3)(AimMath.Direction(aimAngle) * 3f);
        }

        /// <summary>The boss's health bar: wide, over its character, going from green through red as the boss weakens.</summary>
        void DrawBossBar()
        {
            float fraction = MaxHealth > 0 ? Mathf.Clamp01(health.Value / (float)MaxHealth) : 0f;

            healthBack.transform.localPosition = new Vector3(0f, BossBarY, 0f);
            healthBack.transform.localScale = new Vector3(BossBarWidth + 0.08f, BossBarHeight + 0.08f, 1f);

            float width = BossBarWidth * fraction;
            healthFill.transform.localScale = new Vector3(Mathf.Max(width, 0.0001f), BossBarHeight, 1f);
            healthFill.transform.localPosition = new Vector3(-BossBarWidth * 0.5f + width * 0.5f, BossBarY, 0f);
            healthFill.color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.95f, 0.6f, 0.2f), fraction);
        }

        void SetBar(SpriteRenderer fill, float y, float fraction)
        {
            float width = BarWidth * fraction;
            fill.transform.localScale = new Vector3(Mathf.Max(width, 0.0001f), BarHeight, 1f);
            fill.transform.localPosition = new Vector3(-BarWidth * 0.5f + width * 0.5f, y, 0f);
        }

        public override void OnNetworkSpawn()
        {
            if (DevArgs.AimDegrees.HasValue) aimAngle = AimMath.QuantizeAngle(DevArgs.AimDegrees.Value * Mathf.Deg2Rad);
            devSweepAngle = DevArgs.AimDegrees ?? 0f;

            if (DevArgs.CombatLog)
            {
                health.OnValueChanged += (previous, current) =>
                    Debug.Log($"[BS] health owner={OwnerClientId} role={avatar.Role} {previous} -> {current} (server={IsServer})");
                reviveGauge.OnValueChanged += (previous, current) =>
                    Debug.Log($"[BS] revive gauge owner={OwnerClientId} role={avatar.Role} {previous}% -> {current}% (server={IsServer})");
            }
        }

        /// <summary>Server only: copies the simulation's values to the replicated variables when they change.</summary>
        public void ServerWrite(int healthValue, float gauge01, int maxHealthValue = -1)
        {
            ushort h = (ushort)Mathf.Clamp(healthValue, 0, ushort.MaxValue);
            byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(gauge01 * 100f), 0, 100);
            if (maxHealthValue > 0)
            {
                ushort m = (ushort)Mathf.Clamp(maxHealthValue, 1, ushort.MaxValue);
                if (maxHealth.Value != m) maxHealth.Value = m;
            }
            if (health.Value != h) health.Value = h;
            if (reviveGauge.Value != g) reviveGauge.Value = g;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            if (avatar == null || avatar.Role == RoleTable.BossRole) return;
            if (!RoleTable.MatchRunning) { firing = false; return; } // the match is over: no more shooting

            ReadInput();

            // A fallen survivor cannot fire; stop sending fire while down so the server state is clean.
            bool wantFire = firing && !IsDead;

            // Cosmetic only: show the muzzle flash right away when a shot should be leaving.
            localCooldown = Mathf.Max(0f, localCooldown - Time.deltaTime);
            if (wantFire && localCooldown <= 0f)
            {
                localCooldown = CombatConfig.FireCooldownSeconds;
                MuzzleFlashUntil = Time.time + 0.06f;
            }
            if (sender.ShouldSend(Time.unscaledTime, aimAngle, wantFire))
            {
                if (IsServer) CombatServer.Instance?.SetAim(OwnerClientId, aimAngle, wantFire);
                else SetAimRpc(aimAngle, wantFire);
            }
        }

        [Rpc(SendTo.Server)]
        void SetAimRpc(ushort angle, bool fire)
        {
            CombatServer.Instance?.SetAim(OwnerClientId, angle, fire);
        }

        void ReadInput()
        {
            if (DevArgs.Fire) firing = true;

            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;

            if (DevArgs.AimSweepDegreesPerSecond != 0f)
            {
                devSweepAngle += DevArgs.AimSweepDegreesPerSecond * Time.deltaTime;
                aimAngle = AimMath.QuantizeAngle(devSweepAngle * Mathf.Deg2Rad);
            }
            else if (DevArgs.AimDegrees.HasValue)
            {
                // Development aim is fixed; the pointer and stick are ignored.
            }
            else if (pad != null && pad.rightStick.ReadValue().sqrMagnitude > 0.04f)
            {
                aimAngle = AimMath.FromDirection(pad.rightStick.ReadValue());
            }
            else if (mouse != null && Camera.main != null)
            {
                Vector3 pointer = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
                Vector2 toPointer = (Vector2)pointer - (Vector2)transform.position;
                if (toPointer.sqrMagnitude > 0.01f) aimAngle = AimMath.FromDirection(toPointer);
            }

            if (!DevArgs.Fire)
            {
                bool mouseFire = mouse != null && mouse.leftButton.isPressed;
                bool padFire = pad != null && pad.rightTrigger.ReadValue() > 0.5f;
                firing = mouseFire || padFire;
            }
        }
    }
}
