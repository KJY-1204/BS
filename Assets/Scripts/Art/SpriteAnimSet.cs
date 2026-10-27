using System;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// The frames of one character, boss, mob or device, grouped by animation name (idle, walk, attack and so on).
    /// Built by the sprite importer from the Aseprite exports; views pick a frame from it by time.
    /// </summary>
    public sealed class SpriteAnimSet : ScriptableObject
    {
        [Serializable]
        public sealed class Clip
        {
            public string name;
            public Sprite[] frames;
            /// <summary>How long each frame is shown.</summary>
            public float[] seconds;

            public float TotalSeconds
            {
                get
                {
                    float total = 0f;
                    for (int i = 0; i < seconds.Length; i++) total += seconds[i];
                    return total;
                }
            }
        }

        public Clip[] clips = Array.Empty<Clip>();

        /// <summary>The art looks to the right; views flip it when the figure moves the other way.</summary>
        public bool facesRight = true;

        public Clip Find(string clipName)
        {
            for (int i = 0; i < clips.Length; i++)
                if (clips[i].name == clipName) return clips[i];
            return null;
        }

        /// <summary>The clip with the first of these names that exists, or null.</summary>
        public Clip FindAny(string first, string second)
        {
            Clip clip = Find(first);
            return clip ?? Find(second);
        }

        /// <summary>The frame to show <paramref name="time"/> seconds into the clip; a clip that does not loop stays on its last frame.</summary>
        public static Sprite SpriteAt(Clip clip, float time, bool loop)
        {
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return null;

            float total = clip.TotalSeconds;
            if (total <= 0f) return clip.frames[0];

            if (loop) time %= total;
            else if (time >= total) return clip.frames[clip.frames.Length - 1];
            if (time < 0f) time = 0f;

            for (int i = 0; i < clip.frames.Length; i++)
            {
                time -= clip.seconds[i];
                if (time < 0f) return clip.frames[i];
            }
            return clip.frames[clip.frames.Length - 1];
        }
    }
}
