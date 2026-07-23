using System.IO;
using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    /// <summary>
    /// Per-project preferences for the Level Design window. Stored in EditorPrefs under a key
    /// derived from the project path, so two projects on the same machine keep separate folders
    /// and nothing is written into the package (packages installed from a registry are read-only).
    /// </summary>
    internal static class LevelDesignSettings
    {
        public const string DefaultLevelsFolder = "Assets/LevelDesign/Levels";
        public const string DefaultScreenshotsFolder = "Assets/LevelDesign/Screenshots";
        public const string DefaultPaletteResourcePath = "Config/ColorConfig";
        public const string DefaultPaletteTypeName = "ColorConfigDataScriptableObject";

        // Folders used by the tool before it became a package. Picked up automatically when the
        // project still has them and no preference has been saved yet, so existing projects keep
        // reading and writing the levels they already have.
        const string LegacyLevelsFolder = "Assets/_TheGame/Levels";
        const string LegacyScreenshotsFolder = "Assets/_TheGame/Screenshots";

        const string KeyPrefix = "Huycv.LevelDesign.";

        static string s_projectId;

        static string ProjectId
        {
            get
            {
                if (string.IsNullOrEmpty(s_projectId))
                    s_projectId = StableHash(Application.dataPath);
                return s_projectId;
            }
        }

        // FNV-1a. string.GetHashCode() is randomised per process on modern runtimes, which would
        // hand out a different EditorPrefs key on every Editor launch.
        static string StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return hash.ToString("x8");
            }
        }

        static string Key(string name)
        {
            return string.Concat(KeyPrefix, ProjectId, ".", name);
        }

        static string Get(string name, string fallback)
        {
            string v = EditorPrefs.GetString(Key(name), null);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }

        static void Set(string name, string value, string fallback)
        {
            string normalized = Normalize(value);
            if (string.IsNullOrEmpty(normalized))
                normalized = fallback;
            EditorPrefs.SetString(Key(name), normalized);
        }

        public static string LevelsFolder
        {
            get => Get("LevelsFolder", DefaultFor(LegacyLevelsFolder, DefaultLevelsFolder));
            set => Set("LevelsFolder", value, DefaultLevelsFolder);
        }

        public static string ScreenshotsFolder
        {
            get => Get("ScreenshotsFolder", DefaultFor(LegacyScreenshotsFolder, DefaultScreenshotsFolder));
            set => Set("ScreenshotsFolder", value, DefaultScreenshotsFolder);
        }

        public static string PaletteResourcePath
        {
            get => Get("PaletteResourcePath", DefaultPaletteResourcePath);
            set => Set("PaletteResourcePath", value, DefaultPaletteResourcePath);
        }

        public static string PaletteTypeName
        {
            get => Get("PaletteTypeName", DefaultPaletteTypeName);
            set => Set("PaletteTypeName", value, DefaultPaletteTypeName);
        }

        public static void ResetToDefaults()
        {
            EditorPrefs.DeleteKey(Key("LevelsFolder"));
            EditorPrefs.DeleteKey(Key("ScreenshotsFolder"));
            EditorPrefs.DeleteKey(Key("PaletteResourcePath"));
            EditorPrefs.DeleteKey(Key("PaletteTypeName"));
        }

        static string DefaultFor(string legacy, string fresh)
        {
            return Directory.Exists(ToAbsolutePath(legacy)) ? legacy : fresh;
        }

        public static string Normalize(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;
            return assetPath.Replace('\\', '/').TrimEnd('/').Trim();
        }

        public static string ProjectRoot
        {
            // Application.dataPath ends with "/Assets"; strip it to get the folder above it.
            get => Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
        }

        public static string ToAbsolutePath(string assetPath)
        {
            string normalized = Normalize(assetPath);
            if (string.IsNullOrEmpty(normalized))
                return null;
            return Path.Combine(ProjectRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// True when the path is project-relative and lands under Assets/, which is what the
        /// import/export code and AssetDatabase.Refresh() assume.
        /// </summary>
        public static bool IsValidFolder(string assetPath)
        {
            string normalized = Normalize(assetPath);
            if (string.IsNullOrEmpty(normalized))
                return false;
            if (Path.IsPathRooted(normalized))
                return false;
            return normalized == "Assets" || normalized.StartsWith("Assets/");
        }

        /// <summary>
        /// Converts an absolute folder path to a project-relative one, or returns null when the
        /// folder sits outside this project's Assets/ tree.
        /// </summary>
        public static string ToAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return null;
            string abs = absolutePath.Replace('\\', '/').TrimEnd('/');
            string root = ProjectRoot.Replace('\\', '/');
            // OpenFolderPanel and Application.dataPath can disagree on drive-letter casing on
            // Windows, so compare case-insensitively rather than dropping a valid folder.
            if (!abs.StartsWith(root, System.StringComparison.OrdinalIgnoreCase))
                return null;
            string relative = abs.Substring(root.Length).TrimStart('/');
            return IsValidFolder(relative) ? relative : null;
        }
    }
}
