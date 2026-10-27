#pragma warning disable CS0618 // the sprite sheet slicing API is the simplest way to cut a sheet from a script
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BS.Editor
{
    /// <summary>
    /// Brings the Aseprite exports in ArtSource into the project: copies each sheet, cuts it into frames with the foot pivot the
    /// export recorded, and builds the animation set and the library the views read. Run from the BS menu; running it again
    /// refreshes everything in place.
    /// </summary>
    public static class SpriteImporter
    {
        struct Source
        {
            public string Key;
            public string Folder;
            public string Name;
            public float Ppu;
            /// <summary>The standing height in pixels; the pivot sits at the middle of the body, so the object's position is the body's center.</summary>
            public float BodyHeight;
            public bool FacesRight;
        }

        const float SurvivorPpu = 83f;
        const float BossPpu = 57.5f;
        const float MobPpu = 64f;
        const float DevicePpu = 68f;

        static Source[] Sources()
        {
            var list = new List<Source>
            {
                new Source { Key = "reimu", Folder = "Survivors/Reimu", Name = "reimu", Ppu = SurvivorPpu, BodyHeight = 125f, FacesRight = true },
                new Source { Key = "marisa", Folder = "Survivors/Marisa", Name = "marisa", Ppu = SurvivorPpu, BodyHeight = 125f, FacesRight = true },
                new Source { Key = "sakuya", Folder = "Survivors/Sakuya", Name = "sakuya", Ppu = SurvivorPpu, BodyHeight = 125f, FacesRight = true },
                new Source { Key = "sanae", Folder = "Survivors/Sanae", Name = "sanae", Ppu = SurvivorPpu, BodyHeight = 125f, FacesRight = true },
                new Source { Key = "standard", Folder = "Bosses/Standard", Name = "standard", Ppu = BossPpu, BodyHeight = 115f, FacesRight = true },
                new Source { Key = "giant", Folder = "Bosses/Giant", Name = "giant", Ppu = BossPpu, BodyHeight = 115f, FacesRight = true },
                new Source { Key = "sniper", Folder = "Bosses/Sniper", Name = "sniper", Ppu = BossPpu, BodyHeight = 115f, FacesRight = true },
                new Source { Key = "device", Folder = "Devices/SummonDevice", Name = "device", Ppu = DevicePpu, BodyHeight = 164f, FacesRight = true },
            };
            foreach (string mob in new[] { "fairy_red", "fairy_blue", "fairy_green", "ghost", "ufo", "umbrella" })
                list.Add(new Source { Key = "mob_" + mob, Folder = "Mobs/" + mob, Name = mob, Ppu = MobPpu, BodyHeight = 0f, FacesRight = false });
            return list.ToArray();
        }

        [MenuItem("BS/Import Sprites")]
        public static void ImportAll()
        {
            string artRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtSource"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Art", "Sprites"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources"));

            var library = new List<SpriteLibrary.Entry>();
            foreach (Source source in Sources())
            {
                SpriteAnimSet set = Import(artRoot, source);
                if (set != null) library.Add(new SpriteLibrary.Entry { key = source.Key, set = set });
            }

            foreach (Shot shot in Shots())
            {
                SpriteAnimSet set = ImportShot(artRoot, shot);
                if (set != null) library.Add(new SpriteLibrary.Entry { key = shot.Key, set = set });
            }

            foreach (Effect effect in Effects())
            {
                SpriteAnimSet set = ImportEffect(artRoot, effect);
                if (set != null) library.Add(new SpriteLibrary.Entry { key = effect.Key, set = set });
            }

            const string libraryPath = "Assets/Resources/SpriteLibrary.asset";
            var lib = AssetDatabase.LoadAssetAtPath<SpriteLibrary>(libraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<SpriteLibrary>();
                AssetDatabase.CreateAsset(lib, libraryPath);
            }
            lib.entries = library.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BS] imported {library.Count} sprite sets");
        }

        struct Effect
        {
            public string Key;
            public string Name;
            /// <summary>How big the picture is in the arena along its long side (the views scale it to the skill's size).</summary>
            public float WorldLength;
        }

        static Effect[] Effects() => new[]
        {
            new Effect { Key = "fx_reimu_barrier", Name = "reimu_barrier", WorldLength = 1f },
            new Effect { Key = "fx_reimu_q_burst", Name = "reimu_q_burst", WorldLength = 1f },
            new Effect { Key = "fx_reimu_r_ring", Name = "reimu_r_ring", WorldLength = 1f },
        };

        /// <summary>An effect strip (frames side by side, listed in a json): cut into frames, pivot in the middle, one "fx" clip.</summary>
        static SpriteAnimSet ImportEffect(string artRoot, Effect e)
        {
            string png = Path.Combine(artRoot, "Effects", e.Name + ".png");
            string json = Path.Combine(artRoot, "Effects", e.Name + ".json");
            if (!File.Exists(png) || !File.Exists(json))
            {
                Debug.LogWarning($"[BS] effect source missing for {e.Key}: {png}");
                return null;
            }

            string assetPng = $"Assets/Art/Sprites/{e.Key}.png";
            File.Copy(png, Path.GetFullPath(assetPng), true);
            AssetDatabase.ImportAsset(assetPng, ImportAssetOptions.ForceUpdate);

            AsepriteSheet data = JsonUtility.FromJson<AsepriteSheet>(File.ReadAllText(json));
            int texHeight = data.meta.size.h;
            var metas = new SpriteMetaData[data.frames.Length];
            for (int i = 0; i < metas.Length; i++)
            {
                AsepriteRect f = data.frames[i].frame;
                metas[i] = new SpriteMetaData
                {
                    name = $"{e.Key}_{i:00}",
                    rect = new Rect(f.x, texHeight - f.y - f.h, f.w, f.h),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                };
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPng);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Mathf.Max(data.frames[0].frame.w, data.frames[0].frame.h) / e.WorldLength;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 16384;
            importer.spritesheet = metas;
            importer.SaveAndReimport();

            var sprites = new Dictionary<string, Sprite>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(assetPng))
                if (o is Sprite sprite) sprites[sprite.name] = sprite;

            var clip = new SpriteAnimSet.Clip { name = "fx", frames = new Sprite[metas.Length], seconds = new float[metas.Length] };
            for (int i = 0; i < metas.Length; i++)
            {
                clip.frames[i] = sprites[$"{e.Key}_{i:00}"];
                clip.seconds[i] = data.frames[i].duration / 1000f;
            }

            string assetPath = $"Assets/Art/Sprites/{e.Key}.asset";
            var set = AssetDatabase.LoadAssetAtPath<SpriteAnimSet>(assetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SpriteAnimSet>();
                AssetDatabase.CreateAsset(set, assetPath);
            }
            set.clips = new[] { clip };
            EditorUtility.SetDirty(set);
            return set;
        }

        struct Shot
        {
            public string Key;
            /// <summary>How long the shot looks in the arena, in world units, along its long side.</summary>
            public float WorldLength;
            public bool Spins;
        }

        static Shot[] Shots() => new[]
        {
            new Shot { Key = "shot_reimu", WorldLength = 0.6f },
            new Shot { Key = "shot_marisa", WorldLength = 1.0f, Spins = true },
            new Shot { Key = "shot_sakuya", WorldLength = 0.8f },
            new Shot { Key = "shot_sanae", WorldLength = 1.2f },
            new Shot { Key = "shot_standard", WorldLength = 1.0f },
            new Shot { Key = "shot_giant", WorldLength = 1.6f },
            new Shot { Key = "shot_sniper", WorldLength = 0.6f },
            new Shot { Key = "shot_mob_ghost", WorldLength = 0.6f },
            new Shot { Key = "fx_blast", WorldLength = 1f },
        };

        /// <summary>A single-image shot: one sprite, pivot in the middle, sized so its long side is <see cref="Shot.WorldLength"/>.</summary>
        static SpriteAnimSet ImportShot(string artRoot, Shot s)
        {
            string source = Path.Combine(artRoot, "Projectiles", s.Key + ".png");
            if (!File.Exists(source))
            {
                Debug.LogWarning($"[BS] shot source missing for {s.Key}: {source}");
                return null;
            }

            string assetPng = $"Assets/Art/Sprites/{s.Key}.png";
            File.Copy(source, Path.GetFullPath(assetPng), true);
            AssetDatabase.ImportAsset(assetPng, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPng);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPng);
            float longSide = Mathf.Max(sprite.rect.width, sprite.rect.height);
            importer.spritePixelsPerUnit = longSide / s.WorldLength;
            importer.SaveAndReimport();
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPng);

            string assetPath = $"Assets/Art/Sprites/{s.Key}.asset";
            var set = AssetDatabase.LoadAssetAtPath<SpriteAnimSet>(assetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SpriteAnimSet>();
                AssetDatabase.CreateAsset(set, assetPath);
            }
            set.clips = new[] { new SpriteAnimSet.Clip { name = "fly", frames = new[] { sprite }, seconds = new[] { 1f } } };
            set.facesRight = true;
            set.spins = s.Spins;
            EditorUtility.SetDirty(set);
            return set;
        }

        static SpriteAnimSet Import(string artRoot, Source s)
        {
            string exports = Path.Combine(artRoot, s.Folder, "Exports");
            string sheet = Path.Combine(exports, s.Name + "_sheet.png");
            string json = Path.Combine(exports, s.Name + "_sheet.json");
            if (!File.Exists(sheet) || !File.Exists(json))
            {
                Debug.LogWarning($"[BS] sprite source missing for {s.Key}: {sheet}");
                return null;
            }

            string assetPng = $"Assets/Art/Sprites/{s.Key}.png";
            File.Copy(sheet, Path.GetFullPath(assetPng), true);
            AssetDatabase.ImportAsset(assetPng, ImportAssetOptions.ForceUpdate);

            AsepriteSheet data = JsonUtility.FromJson<AsepriteSheet>(File.ReadAllText(json));
            var pivotPx = new Vector2(data.frames[0].frame.w * 0.5f, data.frames[0].frame.h * 0.5f);
            string manifestPath = Path.Combine(exports, s.Name + "_manifest.json");
            if (File.Exists(manifestPath))
            {
                Manifest m = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
                // The recorded pivot is the feet; lift it to the middle of the body.
                pivotPx = new Vector2(m.pivot[0], m.pivot[1] - s.BodyHeight * 0.45f);
            }

            int texHeight = data.meta.size.h;
            var metas = new SpriteMetaData[data.frames.Length];
            for (int i = 0; i < metas.Length; i++)
            {
                AsepriteRect f = data.frames[i].frame;
                metas[i] = new SpriteMetaData
                {
                    name = $"{s.Key}_{i:00}",
                    rect = new Rect(f.x, texHeight - f.y - f.h, f.w, f.h),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(pivotPx.x / f.w, 1f - pivotPx.y / f.h),
                };
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPng);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = s.Ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 16384;
            importer.spritesheet = metas;
            importer.SaveAndReimport();

            var sprites = new Dictionary<string, Sprite>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(assetPng))
                if (o is Sprite sprite) sprites[sprite.name] = sprite;

            var clips = new List<SpriteAnimSet.Clip>();
            foreach (AsepriteTag tag in data.meta.frameTags)
            {
                int count = tag.to - tag.from + 1;
                var clip = new SpriteAnimSet.Clip { name = tag.name, frames = new Sprite[count], seconds = new float[count] };
                for (int i = 0; i < count; i++)
                {
                    clip.frames[i] = sprites[$"{s.Key}_{tag.from + i:00}"];
                    clip.seconds[i] = data.frames[tag.from + i].duration / 1000f;
                }
                clips.Add(clip);
            }

            string assetPath = $"Assets/Art/Sprites/{s.Key}.asset";
            var set = AssetDatabase.LoadAssetAtPath<SpriteAnimSet>(assetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SpriteAnimSet>();
                AssetDatabase.CreateAsset(set, assetPath);
            }
            set.clips = clips.ToArray();
            set.facesRight = s.FacesRight;
            EditorUtility.SetDirty(set);
            return set;
        }

        [System.Serializable] class Manifest { public int[] pivot; }
        [System.Serializable] class AsepriteRect { public int x, y, w, h; }
        [System.Serializable] class AsepriteSize { public int w, h; }
        [System.Serializable] class AsepriteFrame { public AsepriteRect frame; public int duration; }
        [System.Serializable] class AsepriteTag { public string name; public int from, to; }
        [System.Serializable] class AsepriteMeta { public AsepriteSize size; public AsepriteTag[] frameTags; }
        [System.Serializable] class AsepriteSheet { public AsepriteFrame[] frames; public AsepriteMeta meta; }
    }
}
