using Unity.Netcode;
using UnityEngine;

namespace BS
{
    /// <summary>Follows only the local player's character and stays inside the arena.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] float orthographicSize = 6f;

        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
        }

        void LateUpdate()
        {
            Vector2 focus = Vector2.zero;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
                focus = nm.LocalClient.PlayerObject.transform.position;

            transform.position = new Vector3(ClampAxis(focus.x, ArenaConfig.Width, orthographicSize * cam.aspect),
                                             ClampAxis(focus.y, ArenaConfig.Height, orthographicSize),
                                             transform.position.z);
        }

        static float ClampAxis(float value, float arenaSize, float halfView)
        {
            float limit = arenaSize * 0.5f - halfView;
            return limit <= 0f ? 0f : Mathf.Clamp(value, -limit, limit);
        }
    }
}
