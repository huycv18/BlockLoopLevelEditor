using System;
using UnityEngine;

namespace Huycv.LevelDesign
{
    internal sealed class LevelIOGroup : IRightPanelGroup
    {
        static readonly Color s_accent = new Color(0.50f, 0.85f, 0.50f, 1f);
        static readonly GUIContent s_hintLevelId = new GUIContent("e.g. 1");
        static readonly GUIContent s_quickSaveBtn = new GUIContent("Quick Save");
        static readonly GUIContent s_quickLoadBtn = new GUIContent("Quick Load");
        static readonly GUIContent s_importFileBtn = new GUIContent("Import File");
        static readonly GUIContent s_importClipboardBtn = new GUIContent("Import Clipboard");
        static readonly GUIContent s_exportFileBtn = new GUIContent("Export File");
        static readonly GUIContent s_exportClipboardBtn = new GUIContent("Export Clipboard");
        static readonly GUIContent s_clearBoardBtn = new GUIContent("Clear Board", "Clear colors, obstacles, garages, connections, and hidden cubes. Grid size, Level ID, and Generate settings are untouched.");
        static readonly GUIContent s_clearAllBtn = new GUIContent("Clear All Level Data");
        static readonly GUIContent s_foldersBtn = new GUIContent("Output Folders…",
            "Choose where Quick Save writes levels and where screenshots are captured to.");

        const float ColumnGap = 6f;

        readonly LevelEditorContext _ctx;
        readonly Action _onImportFile;
        readonly Action<string> _onImportString;
        readonly Action _onExport;
        readonly Action _onExportClipboard;
        readonly Action _onClearAll;
        readonly Action _onClearBoard;
        readonly Action _onQuickSave;
        readonly Action _onQuickLoad;

        string _fieldLevelId = "";

        public LevelIOGroup(LevelEditorContext ctx, Action onImportFile, Action<string> onImportString,
            Action onExport, Action onExportClipboard, Action onClearAll, Action onClearBoard,
            Action onQuickSave, Action onQuickLoad)
        {
            _ctx = ctx;
            _onImportFile = onImportFile;
            _onImportString = onImportString;
            _onExport = onExport;
            _onExportClipboard = onExportClipboard;
            _onClearAll = onClearAll;
            _onClearBoard = onClearBoard;
            _onQuickSave = onQuickSave;
            _onQuickLoad = onQuickLoad;
        }

        public string FieldLevelId
        {
            get => _fieldLevelId;
            set => _fieldLevelId = value;
        }

        static float ContentHeight()
        {
            return RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing
                + RightPanelHelpers.ButtonHeight * 5
                + RightPanelHelpers.ButtonSpacing * 4;
        }

        public float MeasureHeight(float panelWidth)
        {
            return RightPanelHelpers.MeasureRightGroup(ContentHeight());
        }

        public float Draw(float startY, float panelWidth)
        {
            RightPanelHelpers.EnsureStyles();

            float nextY = RightPanelHelpers.BeginRightGroup(startY, panelWidth,
                "Level I/O", s_accent, ContentHeight(), out var content);

            float cx = content.x + LevelEditorStyles.GroupInnerPadding;
            float cw = content.width - LevelEditorStyles.GroupInnerPadding * 2f;
            float cy = content.y + LevelEditorStyles.GroupInnerPadding;
            float half = (cw - ColumnGap) * 0.5f;

            // Level ID (live-bound directly onto ctx.LevelId — no destructive "apply" step needed)
            GUI.Label(new Rect(cx, cy, RightPanelHelpers.LabelWidth, RightPanelHelpers.RowHeight),
                "Level ID", LevelEditorStyles.PanelLabelStyle);
            var levelIdRect = new Rect(cx + RightPanelHelpers.LabelWidth + 4f, cy,
                cw - RightPanelHelpers.LabelWidth - 4f, RightPanelHelpers.RowHeight);
            _ctx.LevelId = RightPanelHelpers.IntFieldWithHint(levelIdRect, ref _fieldLevelId, -1,
                -1, int.MaxValue, s_hintLevelId);
            cy += RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing;

            DrawButtonPair(cx, cy, half, s_quickSaveBtn, _onQuickSave, s_quickLoadBtn, _onQuickLoad);
            cy += RightPanelHelpers.ButtonHeight + RightPanelHelpers.ButtonSpacing;

            DrawButtonPair(cx, cy, half, s_importFileBtn, _onImportFile, s_importClipboardBtn, DoImportClipboard);
            cy += RightPanelHelpers.ButtonHeight + RightPanelHelpers.ButtonSpacing;

            DrawButtonPair(cx, cy, half, s_exportFileBtn, _onExport, s_exportClipboardBtn, _onExportClipboard);
            cy += RightPanelHelpers.ButtonHeight + RightPanelHelpers.ButtonSpacing;

            DrawButtonPair(cx, cy, half, s_clearBoardBtn, _onClearBoard, s_clearAllBtn, _onClearAll);
            cy += RightPanelHelpers.ButtonHeight + RightPanelHelpers.ButtonSpacing;

            if (GUI.Button(new Rect(cx, cy, cw, RightPanelHelpers.ButtonHeight),
                s_foldersBtn, RightPanelHelpers.ButtonStyle))
                LevelDesignSettingsProvider.Open();

            return nextY;
        }

        void DrawButtonPair(float x, float y, float half, GUIContent leftContent, Action leftAction,
            GUIContent rightContent, Action rightAction)
        {
            if (GUI.Button(new Rect(x, y, half, RightPanelHelpers.ButtonHeight),
                leftContent, RightPanelHelpers.ButtonStyle))
                leftAction?.Invoke();

            if (GUI.Button(new Rect(x + half + ColumnGap, y, half, RightPanelHelpers.ButtonHeight),
                rightContent, RightPanelHelpers.ButtonStyle))
                rightAction?.Invoke();
        }

        void DoImportClipboard()
        {
            string clipboard = GUIUtility.systemCopyBuffer;
            if (!string.IsNullOrEmpty(clipboard))
                _onImportString?.Invoke(clipboard);
        }
    }
}
