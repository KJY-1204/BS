using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BS
{
    /// <summary>
    /// Placeholder character. The owner sends move input, the server computes the position and
    /// replicates it. The owner moves locally with the same rule and is corrected when it drifts.
    /// </summary>
    public class PlayerAvatar : NetworkBehaviour
    {
        static readonly Color[] RoleColors =
        {
            new Color(0.85f, 0.15f, 0.15f), // boss
            new Color(0.25f, 0.55f, 0.95f), // survivor 1
            new Color(0.3f, 0.8f, 0.4f),    // survivor 2
            new Color(0.95f, 0.8f, 0.2f),   // survivor 3
            new Color(0.7f, 0.4f, 0.9f),    // survivor 4
        };

        [Tooltip("Used only when the character has no data of its own.")]
        [SerializeField] float moveSpeed = 6f;
        [Tooltip("Owner is snapped to the server position when farther than this (world units).")]
        [SerializeField] float correctionThreshold = 2.5f;
        [Tooltip("How quickly the owner eases toward the server position while standing still.")]
        [SerializeField] float idleCorrectionRate = 10f;
        [SerializeField] float remoteSmoothing = 15f;
        [SerializeField] SpriteRenderer body;
        /// <summary>The body's scale as the prefab has it, remembered before a boss kind rescales it.</summary>
        Vector3? bodyBaseScale;

        // The art of whoever this avatar is: chosen by the sprite id of its character or boss kind, and played by time.
        SpriteAnimSet art;
        string artKey;
        string artClip;
        float artClock;
        Vector2 artLastPos;
        float artSpeed;
        bool facingRight = true;

        readonly NetworkVariable<Vector2> serverPos = new NetworkVariable<Vector2>(
            Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> role = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly NetworkVariable<byte> character = new NetworkVariable<byte>(
            CharacterIds.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        InputAction moveAction;
        Vector2 serverInput;
        Vector2 lastSentInput;
        Vector2 predicted;

        byte pendingRole;
        byte pendingCharacter;
        Vector2 pendingSpawn;

        /// <summary>Every spawned avatar on this machine (the boss chase and the screens read it).</summary>
        public static readonly List<PlayerAvatar> All = new List<PlayerAvatar>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public byte Role => role.Value;

        /// <summary>The character this player plays (0 for the boss), known to every client.</summary>
        public byte CharacterId => IsSpawned ? character.Value : pendingCharacter;

        /// <summary>Movement speed from the character's data; the server and the owner's own movement use the same value.</summary>
        public float MoveSpeed
        {
            get
            {
                if (role.Value == RoleTable.BossRole) return CharacterCatalog.BossMoveSpeed;
                CharacterDef def = CharacterCatalog.Get(character.Value);
                return def != null ? def.MoveSpeed : moveSpeed;
            }
        }

        public PlayerCombat Combat { get; private set; }

        /// <summary>False while a survivor is down. The boss is always alive.</summary>
        public bool IsAlive => Combat == null || !Combat.IsDead;

        /// <summary>Server only: whether this player is giving move input right now.</summary>
        public bool ServerMoving => IsAlive && serverInput.sqrMagnitude > 1e-4f;

        /// <summary>Called on the server before the object is spawned; applied once it is spawned.</summary>
        public void InitServer(byte assignedRole, Vector2 spawn, byte characterId = CharacterIds.None)
        {
            pendingRole = assignedRole;
            pendingSpawn = spawn;
            pendingCharacter = characterId;
        }

        void Awake()
        {
            Combat = GetComponent<PlayerCombat>();
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            if (body != null && body.sprite == null) body.sprite = CreateSquareSprite();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                role.Value = pendingRole;
                character.Value = pendingCharacter;
                serverPos.Value = pendingSpawn;
                PlayerRegistry.Register(this);
                if (pendingRole != RoleTable.BossRole) CombatServer.Instance?.RegisterSurvivor(this);
                else CombatServer.Instance?.RegisterBoss(this);
            }

            if (!All.Contains(this)) All.Add(this);

            role.OnValueChanged += (_, __) => ApplyColor();
            ApplyColor();

            predicted = serverPos.Value;
            transform.position = predicted;
            Debug.Log($"[BS] avatar spawned: owner={OwnerClientId} role={role.Value} local={IsOwner} server={IsServer} pos={predicted}");

            if (IsOwner)
            {
                moveAction = new InputAction("Move", InputActionType.Value);
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
                moveAction.AddBinding("<Gamepad>/leftStick");
                moveAction.Enable();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                PlayerRegistry.Unregister(this);
                CombatServer.Instance?.UnregisterSurvivor(this);
                CombatServer.Instance?.UnregisterBoss(this);
            }

            All.Remove(this);

            if (moveAction != null)
            {
                moveAction.Disable();
                moveAction.Dispose();
                moveAction = null;
            }
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                Vector2 dir = Vector2.ClampMagnitude(DevArgs.Move ?? moveAction.ReadValue<Vector2>(), 1f);
                if (DevArgs.BossChase && role.Value == RoleTable.BossRole) dir = ChaseDirection();
                if (!IsAlive) dir = Vector2.zero; // a fallen survivor cannot move
                if (!RoleTable.MatchRunning) dir = Vector2.zero; // nothing moves outside a running match
                SendInput(dir);
                if (!IsServer) PredictOwner(dir);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) DebugDisplaceLocal(new Vector2(5f, 0f));
                DevTick();
#endif
            }
            else if (!IsServer)
            {
                float t = 1f - Mathf.Exp(-remoteSmoothing * Time.deltaTime);
                transform.position = Vector2.Lerp(transform.position, serverPos.Value, t);
            }
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;

            if (!RoleTable.MatchRunning) return; // the match is over (or has not begun): nobody moves

            Vector2 input = IsAlive ? serverInput : Vector2.zero;
            Vector2 next = ArenaConfig.ClampCharacter(serverPos.Value + input * (MoveSpeed * Time.fixedDeltaTime));
            if (next != serverPos.Value) serverPos.Value = next;
            transform.position = next;
        }

        /// <summary>Development only (-bsBossChase): the boss walks toward the nearest living survivor.</summary>
        Vector2 ChaseDirection()
        {
            PlayerAvatar nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < All.Count; i++)
            {
                PlayerAvatar other = All[i];
                if (other == null || other == this || other.role.Value == RoleTable.BossRole || !other.IsAlive) continue;
                float d = ((Vector2)other.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = other; }
            }
            if (nearest == null) return Vector2.zero;

            Vector2 toward = (Vector2)nearest.transform.position - (Vector2)transform.position;
            return toward.sqrMagnitude < 0.01f ? Vector2.zero : toward.normalized;
        }

        /// <summary>Server only: places the character somewhere (development scenarios). Owners snap to it.</summary>
        public void ServerTeleport(Vector2 position)
        {
            if (!IsServer) return;
            Vector2 clamped = ArenaConfig.ClampCharacter(position);
            serverPos.Value = clamped;
            transform.position = clamped;
        }

        void SendInput(Vector2 dir)
        {
            if (dir == lastSentInput) return;
            lastSentInput = dir;

            if (IsServer) serverInput = dir;
            else SetInputRpc(dir);
        }

        [Rpc(SendTo.Server)]
        void SetInputRpc(Vector2 dir)
        {
            if (float.IsNaN(dir.x) || float.IsNaN(dir.y)) return;
            serverInput = Vector2.ClampMagnitude(dir, 1f);
        }

        void PredictOwner(Vector2 dir)
        {
            Vector2 server = serverPos.Value;

            if (Vector2.Distance(predicted, server) > correctionThreshold)
            {
                predicted = server;
            }
            else if (dir == Vector2.zero)
            {
                predicted = Vector2.Lerp(predicted, server, 1f - Mathf.Exp(-idleCorrectionRate * Time.deltaTime));
            }
            else
            {
                predicted = ArenaConfig.ClampCharacter(predicted + dir * (MoveSpeed * Time.deltaTime));
            }

            transform.position = predicted;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float devAge;
        float devNextLog;
        int devFramesAfterDisplace = -1;
        bool devDisplaced;

        void DevTick()
        {
            devAge += Time.deltaTime;

            if (DevArgs.DisplaceAt >= 0f && !devDisplaced && devAge >= DevArgs.DisplaceAt)
            {
                devDisplaced = true;
                Debug.Log($"[BS] displace: before local={Vector2Str(transform.position)} server={Vector2Str(serverPos.Value)}");
                DebugDisplaceLocal(new Vector2(5f, 0f));
                devFramesAfterDisplace = 0;
            }
            else if (devFramesAfterDisplace >= 0 && devFramesAfterDisplace < 4)
            {
                Debug.Log($"[BS] displace: +{++devFramesAfterDisplace} frame local={Vector2Str(transform.position)} server={Vector2Str(serverPos.Value)}");
            }

            if (DevArgs.Log && devAge >= devNextLog)
            {
                devNextLog = devAge + 1f;
                Camera cam = Camera.main;
                string camPos = cam != null ? Vector2Str(cam.transform.position) : "none";
                Debug.Log($"[BS] pos role={role.Value} local={Vector2Str(transform.position)} server={Vector2Str(serverPos.Value)} camera={camPos}");
            }
        }

        static string Vector2Str(Vector2 v) => $"({v.x:F2},{v.y:F2})";
#endif

        /// <summary>Pretend the client moved itself somewhere it should not be (verifies server correction).</summary>
        public void DebugDisplaceLocal(Vector2 offset)
        {
            if (IsServer || !IsOwner) return;
            predicted += offset;
            transform.position = predicted;
        }

        void ApplyColor()
        {
            if (body == null) return;

            bool isBoss = role.Value == RoleTable.BossRole;
            CharacterDef def = isBoss ? null : CharacterCatalog.Get(character.Value);
            Color color = def != null ? def.Color : RoleColors[Mathf.Min(role.Value, (byte)(RoleColors.Length - 1))];
            if (isBoss)
            {
                // The boss is drawn in its kind's color and as large, compared with the standard boss, as its kind's body radius is.
                BossDef kind = BossCatalog.Current;
                color = kind.Color;
                if (bodyBaseScale == null) bodyBaseScale = body.transform.localScale;
                body.transform.localScale = bodyBaseScale.Value * (kind.Radius / BossCatalog.Default.Radius);
            }
            if (!IsAlive) color = Color.Lerp(color, new Color(0.25f, 0.25f, 0.28f), 0.75f); // fallen survivors are shown greyed out
            if (art != null) color = IsAlive ? Color.white : Color.Lerp(Color.white, new Color(0.3f, 0.3f, 0.35f), 0.7f); // the art keeps its own colors
            body.color = color;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            ApplyColor();
            UpdateArt();
        }

        string ArtId()
        {
            if (role.Value == RoleTable.BossRole) return BossCatalog.Current.SpriteId;
            CharacterDef def = CharacterCatalog.Get(character.Value);
            return def != null ? def.SpriteId : null;
        }

        /// <summary>Shows the frame of the current animation: down, firing, walking or standing; and faces the way it aims or moves.</summary>
        void UpdateArt()
        {
            if (body == null) return;

            string key = ArtId();
            if (key != artKey)
            {
                artKey = key;
                art = SpriteLibrary.Get(key);
                artClip = null;
                artLastPos = transform.position;
                if (art != null && bodyBaseScale == null) bodyBaseScale = body.transform.localScale;
            }
            if (art == null) return;

            Vector2 pos = transform.position;
            float dt = Time.deltaTime;
            float dx = pos.x - artLastPos.x;
            if (dt > 0f) artSpeed = Mathf.Lerp(artSpeed, (pos - artLastPos).magnitude / dt, 1f - Mathf.Exp(-12f * dt));
            artLastPos = pos;
            artClock += dt;

            bool boss = role.Value == RoleTable.BossRole;
            bool down = !IsAlive;
            bool firing = Combat != null && !down && (Combat.IsFiring || Time.time < Combat.MuzzleFlashUntil + 0.25f);

            string clipName;
            if (down) clipName = "hit";
            else if (firing) clipName = "attack";
            else if (artSpeed > 0.4f) clipName = boss ? "move" : "walk";
            else clipName = "idle";

            SpriteAnimSet.Clip clip = art.Find(clipName) ?? art.FindAny("idle", "move");
            if (clip == null) return;
            if (clip.name != artClip) { artClip = clip.name; artClock = 0f; }
            body.sprite = SpriteAnimSet.SpriteAt(clip, artClock, !down);

            if (firing && Combat != null) facingRight = AimMath.Direction(Combat.AimAngle).x >= 0f;
            else if (Mathf.Abs(dx) > 1e-4f) facingRight = dx > 0f;
            body.flipX = art.facesRight ? !facingRight : facingRight;
        }

        static Sprite CreateSquareSprite()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
