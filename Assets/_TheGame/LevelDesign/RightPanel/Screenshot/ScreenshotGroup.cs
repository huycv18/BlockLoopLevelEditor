using System;
using UnityEditor;
using UnityEngine;

namespace BlockLoop.LevelDesign
{
    internal sealed class ScreenshotGroup : IRightPanelGroup
    {
        static readonly Color s_accent = new Color(0.85f, 0.55f, 0.85f, 1f);
        static readonly GUIContent s_enableToggleLabel = new GUIContent("Enable Auto Screenshot",
            "When on, Capture Range sweeps the Level ID range and saves a board screenshot per level.");
        static readonly GUIContent s_hintId = new GUIContent("id");
        static readonly GUIContent s_captureBtn = new GUIContent("Capture Range");
        static readonly GUIContent s_cancelBtn = new GUIContent("Cancel");
        const float FromLabelWidth = 36f;
        const float ToLabelWidth = 22f;
        const float ColumnGap = 6f;

        readonly Func<bool> _getEnabled;
        readonly Action<bool> _setEnabled;
        readonly Func<bool> _isRunning;
        readonly Func<string> _getStatusText;
        readonly Action<int, int> _onCaptureRange;
        readonly Action _onCancel;

        string _fieldFromId = "";
        string _fieldToId = "";

        public ScreenshotGroup(Func<bool> getEnabled, Action<bool> setEnabled, Func<bool> isRunning,
            Func<string> getStatusText, Action<int, int> onCaptureRange, Action onCancel)
        {
            _getEnabled = getEnabled;
            _setEnabled = setEnabled;
            _isRunning = isRunning;
            _getStatusText = getStatusText;
            _onCaptureRange = onCaptureRange;
            _onCancel = onCancel;
        }

        static float ContentHeight(bool showStatus)
        {
            float h = RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing   // enable toggle
                + RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing       // from/to row
                + RightPanelHelpers.ButtonHeight;                                  // capture/cancel button
            if (showStatus)
                h += RightPanelHelpers.RowSpacing + RightPanelHelpers.RowHeight;
            return h;
        }

        public float MeasureHeight(float panelWidth)
        {
            return RightPanelHelpers.MeasureRightGroup(ContentHeight(_isRunning()));
        }

        public float Draw(float startY, float panelWidth)
        {
            RightPanelHelpers.EnsureStyles();
            bool running = _isRunning();

            float nextY = RightPanelHelpers.BeginRightGroup(startY, panelWidth,
                "Screenshot", s_accent, ContentHeight(running), out var content);

            float cx = content.x + LevelEditorStyles.GroupInnerPadding;
            float cw = content.width - LevelEditorStyles.GroupInnerPadding * 2f;
            float cy = content.y + LevelEditorStyles.GroupInnerPadding;

            bool enabled = _getEnabled();
            using (new EditorGUI.DisabledScope(running))
            {
                bool newEnabled = GUI.Toggle(new Rect(cx, cy, cw, RightPanelHelpers.RowHeight), enabled, s_enableToggleLabel);
                if (newEnabled != enabled)
                    _setEnabled(newEnabled);
            }
            cy += RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing;

            float half = (cw - ColumnGap) * 0.5f;
            GUI.Label(new Rect(cx, cy, FromLabelWidth, RightPanelHelpers.RowHeight), "From", LevelEditorStyles.PanelLabelStyle);
            var fromRect = new Rect(cx + FromLabelWidth, cy, half - FromLabelWidth, RightPanelHelpers.RowHeight);
            int fromId = RightPanelHelpers.IntFieldWithHint(fromRect, ref _fieldFromId, 0, 0, int.MaxValue, s_hintId);

            float rightX = cx + half + ColumnGap;
            GUI.Label(new Rect(rightX, cy, ToLabelWidth, RightPanelHelpers.RowHeight), "To", LevelEditorStyles.PanelLabelStyle);
            var toRect = new Rect(rightX + ToLabelWidth, cy, half - ToLabelWidth, RightPanelHelpers.RowHeight);
            int toId = RightPanelHelpers.IntFieldWithHint(toRect, ref _fieldToId, 0, 0, int.MaxValue, s_hintId);
            cy += RightPanelHelpers.RowHeight + RightPanelHelpers.RowSpacing;

            if (running)
            {
                if (GUI.Button(new Rect(cx, cy, cw, RightPanelHelpers.ButtonHeight), s_cancelBtn, RightPanelHelpers.ButtonStyle))
                    _onCancel?.Invoke();
            }
            else
            {
                using (new EditorGUI.DisabledScope(!enabled))
                {
                    if (GUI.Button(new Rect(cx, cy, cw, RightPanelHelpers.ButtonHeight), s_captureBtn, RightPanelHelpers.ButtonStyle))
                        _onCaptureRange?.Invoke(fromId, toId);
                }
            }
            cy += RightPanelHelpers.ButtonHeight;

            if (running)
            {
                cy += RightPanelHelpers.RowSpacing;
                GUI.Label(new Rect(cx, cy, cw, RightPanelHelpers.RowHeight), _getStatusText(), LevelEditorStyles.PanelLabelStyle);
            }

            return nextY;
        }
    }
}
