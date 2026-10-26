using UnityEngine;

namespace BS
{
    /// <summary>
    /// The camera every player sees the match through: it shows the whole arena, centred on it, whatever the shape of the window. It
    /// does not follow any character (the class keeps its name so the scene does not change).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        /// <summary>Only a fallback before the first frame: the size is set from the window's aspect ratio every frame.</summary>
        [SerializeField] float orthographicSize = 10f;

        Camera cam;

        /// <summary>
        /// The half-height of an orthographic view that shows the whole arena for a window of this aspect ratio (width over height):
        /// limited by the arena's height in a wide window and by its width in a narrow one.
        /// </summary>
        public static float SizeToFitArena(float aspect)
        {
            float safeAspect = Mathf.Max(0.01f, aspect);
            return Mathf.Max(ArenaConfig.Height * 0.5f, ArenaConfig.Width * 0.5f / safeAspect);
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
        }

        void LateUpdate()
        {
            cam.orthographicSize = SizeToFitArena(cam.aspect);
            transform.position = new Vector3(0f, 0f, transform.position.z);
        }
    }
}
