using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Game
{
    [System.Serializable]
    public struct ColorConfigData
    {
        public int materialId;
        public Color color;
        public Color shadowColor;
    }
    
    [CreateAssetMenu(fileName = "ColorConfigData", menuName = "PixelBlast/Config/Color Config Data")]
    public class ColorConfigDataScriptableObject : ScriptableObject
    {
        #region Members
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material normalFlipMaterial;
        [SerializeField] private Material hiddenMaterial;
        [SerializeField] private List<ColorConfigData> colorConfigDatas;
        private Dictionary<int, ColorConfigData> levelColorOverrides;
        #endregion
        #region Properties
        public Material Normal => normalMaterial;
        public Material NormalFlip => normalFlipMaterial;
        public Material Hidden => hiddenMaterial;
        #endregion
        #region Class Methods
        public void ClearLevelColorOverrides()
        {
            levelColorOverrides = null;
        }
        public ColorConfigData GetColorConfig(int id)
        {
            if (levelColorOverrides != null && levelColorOverrides.TryGetValue(id, out var overrideData))
                return overrideData;

            if (colorConfigDatas == null || colorConfigDatas.Count == 0)
            {
                Debug.LogWarning($"[{nameof(ColorConfigDataScriptableObject)}] No color config data found on {name}.");
                return default;
            }

            foreach (ColorConfigData data in colorConfigDatas)
            {
                if (data.materialId == id)
                    return data;
            }

            Debug.LogWarning($"[{nameof(ColorConfigDataScriptableObject)}] No color config found with materialId: {id}.");
            return default;
        }
        #endregion

#if UNITY_EDITOR
        #region Editor
        [System.Serializable]
        private class ColorDataWrapper
        {
            public List<ColorDataItemJson> items;
        }

        [System.Serializable]
        private class ColorDataItemJson
        {
            public int materialId;
            public Color color;
            public Color shadowColor;
            public string materialName;
        }

        [ContextMenu("Export To JSON")]
        private void ExportToJson()
        {
            if (colorConfigDatas == null || colorConfigDatas.Count == 0)
            {
                Debug.LogWarning($"[{nameof(ColorConfigDataScriptableObject)}] No color config data to export.");
                return;
            }

            var wrapper = new ColorDataWrapper
            {
                items = new List<ColorDataItemJson>(colorConfigDatas.Count)
            };
            foreach (var data in colorConfigDatas)
            {
                wrapper.items.Add(new ColorDataItemJson
                {
                    materialId = data.materialId,
                    color = data.color,
                    shadowColor = data.shadowColor,
                });
            }

            string json = JsonUtility.ToJson(wrapper, true);
            string path = UnityEditor.EditorUtility.SaveFilePanel("Export Color Data JSON", Application.dataPath, "ColorConfigData", "json");
            if (string.IsNullOrEmpty(path))
                return;

            System.IO.File.WriteAllText(path, json);
            Debug.Log($"[{nameof(ColorConfigDataScriptableObject)}] Exported {colorConfigDatas.Count} color configs to {path}.");
        }

        [ContextMenu("Load From JSON")]
        private void LoadFromJson()
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select Color Data JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path))
                return;

            string json = System.IO.File.ReadAllText(path);
            ColorDataWrapper wrapper = JsonUtility.FromJson<ColorDataWrapper>(json);

            if (wrapper == null || wrapper.items == null || wrapper.items.Count == 0)
            {
                Debug.LogWarning($"[{nameof(ColorConfigDataScriptableObject)}] No items found in JSON.");
                return;
            }

            wrapper.items.Sort((a, b) => a.materialId.CompareTo(b.materialId));

            colorConfigDatas = new List<ColorConfigData>(wrapper.items.Count);
            foreach (ColorDataItemJson item in wrapper.items)
            {
                colorConfigDatas.Add(new ColorConfigData
                {
                    materialId = item.materialId,
                    color = item.color,
                    shadowColor = item.shadowColor,
                });
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[{nameof(ColorConfigDataScriptableObject)}] Loaded {colorConfigDatas.Count} color configs from JSON.");
        }
        #endregion
#endif
    }
}
