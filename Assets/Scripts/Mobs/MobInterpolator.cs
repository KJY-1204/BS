using System.Collections.Generic;
using UnityEngine;

namespace BS
{
    /// <summary>
    /// Client-side view of the server's mobs. Keeps a few timestamped positions per mob and shows them
    /// slightly in the past (about 130 ms) by blending between the two samples around the render clock.
    /// Plain C#, driven by Advance(dt), so it is testable without a scene.
    /// </summary>
    public sealed class MobInterpolator : IMobSource
    {
        const int SampleCount = 4;

        sealed class Track
        {
            public ushort Id;
            public byte Type;
            public readonly float[] Time = new float[SampleCount];
            public readonly Vector2[] Position = new Vector2[SampleCount];
            public int Samples;
            public float LastReceived;
            /// <summary>Display-clock time at which the server removed this mob (infinity while it is alive).</summary>
            public float RemoveAtServerTime = float.PositiveInfinity;
            /// <summary>Local time after which a pending removal happens even if the display clock stalls.</summary>
            public float RemoveAtLocalTime = float.PositiveInfinity;
            public bool RemovalPending => !float.IsPositiveInfinity(RemoveAtServerTime);
        }

        readonly List<Track> tracks = new List<Track>();
        readonly Dictionary<ushort, int> indexOf = new Dictionary<ushort, int>();
        readonly Dictionary<ushort, float> tombstones = new Dictionary<ushort, float>();
        readonly List<ushort> expired = new List<ushort>();

        public float Delay = MobConfig.InterpolationDelay;
        public float StaleSeconds = MobConfig.StaleSeconds;
        public float TombstoneSeconds = 1f;

        float localTime;
        float latestServerTime;
        float renderTime;
        bool hasClock;
        float nextTombstoneSweep;

        /// <summary>How many mobs were dropped because no position arrived for too long.</summary>
        public int StaleRemovals { get; private set; }

        /// <summary>How many mobs were first seen in a snapshot, before their spawn event arrived.</summary>
        public int ProvisionalCreations { get; private set; }

        public int Count => tracks.Count;
        public ushort IdAt(int index) => tracks[index].Id;
        public byte TypeAt(int index) => tracks[index].Type;
        public Vector2 PositionAt(int index) => Sample(tracks[index]);

        public float RenderTime => renderTime;

        public void Clear()
        {
            tracks.Clear();
            indexOf.Clear();
            tombstones.Clear();
            hasClock = false;
            StaleRemovals = 0;
            ProvisionalCreations = 0;
        }

        /// <summary>
        /// A snapshot with this server time arrived, whether or not it listed any mob. The display clock
        /// follows these times, so shots and effects keep moving even while there are no mobs.
        /// </summary>
        public void OnSnapshotTime(float serverTime) => ObserveServerTime(serverTime);

        public void OnSnapshotEntry(ushort id, Vector2 position, float serverTime)
        {
            ObserveServerTime(serverTime);
            if (IsTombstoned(id)) return;

            if (!indexOf.ContainsKey(id)) ProvisionalCreations++;
            Track track = GetOrCreate(id);
            AddSample(track, serverTime, position);
        }

        public void OnSpawn(ushort id, byte type, Vector2 position, float serverTime)
        {
            ObserveServerTime(serverTime);
            tombstones.Remove(id);

            Track track = GetOrCreate(id);
            track.Type = type;
            AddSample(track, serverTime, position);
        }

        /// <summary>Removes the mob right now.</summary>
        public void OnRemove(ushort id)
        {
            if (indexOf.TryGetValue(id, out int i)) RemoveAt(i);
            tombstones[id] = localTime + TombstoneSeconds;
        }

        /// <summary>
        /// Removes the mob when the display clock reaches the time the server removed it, so a mob that was
        /// killed stays visible until the shot that killed it arrives. A removal that is already due happens now.
        /// </summary>
        public void OnRemove(ushort id, float serverTime)
        {
            if (!hasClock || serverTime <= renderTime || !indexOf.TryGetValue(id, out int i))
            {
                OnRemove(id);
                return;
            }

            Track track = tracks[i];
            track.RemoveAtServerTime = serverTime;
            // The display clock normally advances one to one with local time, which gives a fallback deadline
            // for the case where no further snapshots arrive and the clock stops.
            track.RemoveAtLocalTime = localTime + (serverTime - renderTime) + 0.05f;
        }

        public void Advance(float dt)
        {
            localTime += dt;

            if (hasClock)
            {
                renderTime += dt;
                float target = latestServerTime - Delay;
                float error = target - renderTime;
                if (Mathf.Abs(error) > 0.25f) renderTime = target;
                else renderTime += error * Mathf.Min(1f, 4f * dt);
                if (renderTime > latestServerTime) renderTime = latestServerTime;
            }

            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                Track track = tracks[i];

                if (track.RemovalPending)
                {
                    if (renderTime >= track.RemoveAtServerTime || localTime >= track.RemoveAtLocalTime)
                        OnRemove(track.Id);
                    continue;
                }

                if (localTime - track.LastReceived > StaleSeconds)
                {
                    RemoveAt(i);
                    StaleRemovals++;
                }
            }

            if (localTime >= nextTombstoneSweep)
            {
                nextTombstoneSweep = localTime + 1f;
                SweepTombstones();
            }
        }

        // ---- Internals ----

        void ObserveServerTime(float serverTime)
        {
            if (!hasClock)
            {
                hasClock = true;
                latestServerTime = serverTime;
                renderTime = serverTime - Delay;
            }
            else if (serverTime > latestServerTime)
            {
                latestServerTime = serverTime;
            }
        }

        bool IsTombstoned(ushort id) => tombstones.TryGetValue(id, out float until) && localTime < until;

        Track GetOrCreate(ushort id)
        {
            if (indexOf.TryGetValue(id, out int i)) return tracks[i];

            // A position can arrive before its spawn event; show the mob now and learn its type later.
            var track = new Track { Id = id };
            indexOf[id] = tracks.Count;
            tracks.Add(track);
            track.LastReceived = localTime;
            return track;
        }

        void AddSample(Track track, float time, Vector2 position)
        {
            track.LastReceived = localTime;

            if (track.Samples > 0 && time <= track.Time[track.Samples - 1]) return;

            if (track.Samples == SampleCount)
            {
                for (int k = 1; k < SampleCount; k++)
                {
                    track.Time[k - 1] = track.Time[k];
                    track.Position[k - 1] = track.Position[k];
                }
                track.Samples--;
            }

            track.Time[track.Samples] = time;
            track.Position[track.Samples] = position;
            track.Samples++;
        }

        Vector2 Sample(Track track)
        {
            if (track.Samples == 0) return Vector2.zero;
            if (renderTime <= track.Time[0]) return track.Position[0];

            for (int k = 1; k < track.Samples; k++)
            {
                if (renderTime <= track.Time[k])
                {
                    float span = track.Time[k] - track.Time[k - 1];
                    float t = span > 1e-5f ? (renderTime - track.Time[k - 1]) / span : 1f;
                    return Vector2.Lerp(track.Position[k - 1], track.Position[k], t);
                }
            }
            return track.Position[track.Samples - 1];
        }

        void RemoveAt(int i)
        {
            Track removed = tracks[i];
            int last = tracks.Count - 1;
            if (i != last)
            {
                tracks[i] = tracks[last];
                indexOf[tracks[i].Id] = i;
            }
            tracks.RemoveAt(last);
            indexOf.Remove(removed.Id);
        }

        void SweepTombstones()
        {
            expired.Clear();
            foreach (KeyValuePair<ushort, float> pair in tombstones)
                if (localTime >= pair.Value) expired.Add(pair.Key);
            for (int i = 0; i < expired.Count; i++) tombstones.Remove(expired[i]);
        }
    }
}
