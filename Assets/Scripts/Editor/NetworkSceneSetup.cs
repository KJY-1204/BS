using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BS.Editor
{
    /// <summary>Builds the network test scene and the placeholder player prefab. Safe to run again.</summary>
    public static class NetworkSceneSetup
    {
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const string PrefabListPath = "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("BS/Setup Network Scene")]
        public static void Run()
        {
            GameObject playerPrefab = BuildPlayerPrefab();
            RegisterNetworkPrefab(playerPrefab);

            Scene scene = SceneManager.GetActiveScene();
            ClearExisting("NetworkManager", "RoleTable", "Bootstrap", "Arena", "Mobs");

            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.PlayerPrefab = null;
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (list != null && !nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(list))
                nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);

            var tableGo = new GameObject("RoleTable");
            tableGo.AddComponent<NetworkObject>();
            var table = tableGo.AddComponent<RoleTable>();
            var tableSo = new SerializedObject(table);
            tableSo.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            tableSo.ApplyModifiedPropertiesWithoutUndo();

            var bootGo = new GameObject("Bootstrap");
            var bootstrap = bootGo.AddComponent<SessionBootstrap>();
            var menu = bootGo.AddComponent<NetworkMenu>();
            var menuSo = new SerializedObject(menu);
            menuSo.FindProperty("bootstrap").objectReferenceValue = bootstrap;
            menuSo.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("Arena").AddComponent<ArenaVisual>();

            var mobsGo = new GameObject("Mobs");
            mobsGo.AddComponent<MobServer>();
            mobsGo.AddComponent<CombatServer>();
            mobsGo.AddComponent<GrowthServer>();
            mobsGo.AddComponent<SummonServer>();
            mobsGo.AddComponent<CombatClient>();
            mobsGo.AddComponent<CombatView>();
            mobsGo.AddComponent<CombatHud>();
            mobsGo.AddComponent<GrowthHud>();
            mobsGo.AddComponent<MatchHud>();
            mobsGo.AddComponent<SummonHud>();
            mobsGo.AddComponent<MobClient>();
            mobsGo.AddComponent<MobView>();

            Camera cam = Camera.main;
            if (cam != null)
            {
                if (cam.GetComponent<CameraFollow>() == null) cam.gameObject.AddComponent<CameraFollow>();
                cam.orthographic = true;
                cam.orthographicSize = 6f;
                cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.transform.position = new Vector3(0f, 0f, -10f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[BS] Network scene setup complete.");
        }

        static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player");
            root.AddComponent<NetworkObject>();
            var avatar = root.AddComponent<PlayerAvatar>();
            root.AddComponent<PlayerCombat>();
            root.AddComponent<PlayerGrowth>();
            root.AddComponent<PlayerSummon>();

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;

            var so = new SerializedObject(avatar);
            so.FindProperty("body").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void RegisterNetworkPrefab(GameObject prefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (list == null) { Debug.LogWarning("[BS] DefaultNetworkPrefabs.asset not found."); return; }

            foreach (NetworkPrefab entry in list.PrefabList)
                if (entry.Prefab == prefab) return;

            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
        }

        static void ClearExisting(params string[] names)
        {
            foreach (string n in names)
            {
                GameObject existing = GameObject.Find(n);
                if (existing != null) Object.DestroyImmediate(existing);
            }
        }
    }
}
