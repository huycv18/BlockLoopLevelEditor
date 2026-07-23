using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    /// <summary>
    /// Project Settings page for <see cref="LevelDesignSettings"/>.
    /// Reachable from the Level I/O panel's "Output Folders…" button.
    /// </summary>
    static class LevelDesignSettingsProvider
    {
        public const string SettingsPath = "Project/Level Design (Huycv)";

        static readonly GUIContent s_levelsLabel = new GUIContent("Levels Folder",
            "Project-relative folder that Quick Save / Quick Load use for Level_<id>.json.");
        static readonly GUIContent s_screenshotsLabel = new GUIContent("Screenshots Folder",
            "Project-relative folder that screenshot capture writes Level_<id>.png into.");
        static readonly GUIContent s_paletteResourceLabel = new GUIContent("Palette Resource Path",
            "Path passed to Resources.Load, relative to any Resources folder. Leave at the default if unsure.");
        static readonly GUIContent s_paletteTypeLabel = new GUIContent("Palette Type Name",
            "ScriptableObject class name searched for when the resource path finds nothing. It must expose a serialized 'colorConfigDatas' list.");

        public static void Open()
        {
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Level Design (Huycv)",
                guiHandler = _ => DrawGui(),
                keywords = new HashSet<string>(new[]
                {
                    "level", "design", "huycv", "levels", "screenshots", "folder", "palette"
                })
            };
        }

        static void DrawGui()
        {
            EditorGUIUtility.labelWidth = 180f;
            EditorGUILayout.Space(6f);

            EditorGUILayout.LabelField("Output Folders", EditorStyles.boldLabel);
            Assign(DrawFolderField(s_levelsLabel, LevelDesignSettings.LevelsFolder, "Select Levels Folder"),
                LevelDesignSettings.LevelsFolder, v => LevelDesignSettings.LevelsFolder = v);
            Assign(DrawFolderField(s_screenshotsLabel, LevelDesignSettings.ScreenshotsFolder, "Select Screenshots Folder"),
                LevelDesignSettings.ScreenshotsFolder, v => LevelDesignSettings.ScreenshotsFolder = v);

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Color Palette Source", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The window reads its colour palette from any ScriptableObject with a serialized " +
                "'colorConfigDatas' list. It is looked up by resource path first, then by type name.",
                MessageType.None);
            Assign(EditorGUILayout.TextField(s_paletteResourceLabel, LevelDesignSettings.PaletteResourcePath),
                LevelDesignSettings.PaletteResourcePath, v => LevelDesignSettings.PaletteResourcePath = v);
            Assign(EditorGUILayout.TextField(s_paletteTypeLabel, LevelDesignSettings.PaletteTypeName),
                LevelDesignSettings.PaletteTypeName, v => LevelDesignSettings.PaletteTypeName = v);

            EditorGUILayout.Space(14f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset to Defaults", GUILayout.Width(140f)))
                {
                    LevelDesignSettings.ResetToDefaults();
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "Settings are stored per project in EditorPrefs; they are not committed to version control.",
                EditorStyles.miniLabel);
        }

        // EditorPrefs writes hit disk, and guiHandler runs every repaint, so only store real edits.
        static void Assign(string candidate, string current, System.Action<string> setter)
        {
            if (candidate != current)
                setter(candidate);
        }

        static string DrawFolderField(GUIContent label, string current, string dialogTitle)
        {
            string result;
            using (new EditorGUILayout.HorizontalScope())
            {
                result = EditorGUILayout.TextField(label, current);
                if (GUILayout.Button("Browse…", GUILayout.Width(80f)))
                {
                    string start = LevelDesignSettings.ToAbsolutePath(current);
                    if (string.IsNullOrEmpty(start) || !Directory.Exists(start))
                        start = Application.dataPath;

                    string picked = EditorUtility.OpenFolderPanel(dialogTitle, start, "");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        string asAsset = LevelDesignSettings.ToAssetPath(picked);
                        if (asAsset == null)
                            EditorUtility.DisplayDialog(dialogTitle,
                                "Pick a folder inside this project's Assets/ folder.", "OK");
                        else
                            result = asAsset;
                        GUI.FocusControl(null);
                    }
                }
            }

            if (!LevelDesignSettings.IsValidFolder(result))
                EditorGUILayout.HelpBox(
                    "Must be a project-relative path starting with 'Assets/'. The previous value is still in use.",
                    MessageType.Warning);

            // Folders are created on demand at save time, but flagging it here avoids a surprise.
            else if (!Directory.Exists(LevelDesignSettings.ToAbsolutePath(result)))
                EditorGUILayout.HelpBox("Folder does not exist yet; it will be created on first save.",
                    MessageType.Info);

            return LevelDesignSettings.IsValidFolder(result) ? result : current;
        }
    }
}
