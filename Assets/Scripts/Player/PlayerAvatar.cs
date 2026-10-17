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

        [SerializeField] float moveSpeed = 6f;
        [Tooltip("Owner is snapped to the server position when farther than this (world units).")]
        [SerializeField] float correctionThreshold = 2.5f;
        [Tooltip("How quickly the owner eases toward the server position while standing still.")]
        [SerializeField] float idleCorrectionRate = 10f;
        [SerializeField] float remoteSmoothing = 15f;
        [SerializeField] SpriteRenderer body;

        readonly NetworkVariable<Vector2> serverPos = new NetworkVariable<Vector2>(
            Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<byte> role = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        InputAction moveAction;
        Vector2 serverInput;
        Vector2 lastSentInput;
        Vector2 predicted;

        byte pendingRole;
        Vector2 pendingSpawn;

        public byte Role => role.Value;

        /// <summary>Called on the server before the object is spawned; applied once it is spawned.</summary>
        public void InitServer(byte assignedRole, Vector2 spawn)
        {
            pendingRole = assignedRole;
            pendingSpawn = spawn;
        }

        void Awake()
        {
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            if (body != null && body.sprite == null) body.sprite = CreateSquareSprite();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                role.Value = pendingRole;
                serverPos.Value = pendingSpawn;
            }

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

            Vector2 next = ArenaConfig.ClampCharacter(serverPos.Value + serverInput * (moveSpeed * Time.fixedDeltaTime));
            if (next != serverPos.Value) serverPos.Value = next;
            transform.position = next;
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
                predicted = ArenaConfig.ClampCharacter(predicted + dir * (moveSpeed * Time.deltaTime));
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
            if (body != null) body.color = RoleColors[Mathf.Min(role.Value, (byte)(RoleColors.Length - 1))];
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
