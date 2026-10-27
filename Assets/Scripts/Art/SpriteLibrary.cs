using System;
using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>Every sprite set by key. It sits in a Resources folder so views find it in the editor and in a build alike.</summary>
    public sealed class SpriteLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public SpriteAnimSet set;
        }

        public Entry[] entries = Array.Empty<Entry>();

        static Dictionary<string, SpriteAnimSet> map;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => map = null;

        /// <summary>The set for this key, or null when there is none (the view then keeps its placeholder).</summary>
        public static SpriteAnimSet Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (map == null)
            {
                map = new Dictionary<string, SpriteAnimSet>();
                SpriteLibrary library = Resources.Load<SpriteLibrary>("SpriteLibrary");
                if (library != null)
                    for (int i = 0; i < library.entries.Length; i++)
                        if (library.entries[i].set != null) map[library.entries[i].key] = library.entries[i].set;
            }

            return map.TryGetValue(key, out SpriteAnimSet set) ? set : null;
        }
    }
}
