using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    public class LevelDesignWindow : EditorWindow
    {
        // ════════════════════════════════════════════════════════
        //  Constants
        // ════════════════════════════════════════════════════════

        const float GridPadding = 10f;
        const float GridBottomPadding = 300f;
        const float MaxCellSize = 80f;
        const float AxisLabelSize = 18f;

        const float MinPanelWidth = 100f;
        const float MaxPanelWidth = 700f;
        const float SplitterWidth = 4f;

        // ── Colors ──
        static readonly Color s_panelBgColor       = new Color(0.17f, 0.17f, 0.19f, 1f);
        static readonly Color s_splitterColor      = new Color(0.10f, 0.10f, 0.12f, 1f);
        static readonly Color s_splitterHoverColor = new Color(0.35f, 0.50f, 0.85f, 0.6f);


        // ════════════════════════════════════════════════════════
        //  Serialized state (must stay on EditorWindow)
        // ════════════════════════════════════════════════════════

        [SerializeField] int _gridWidth = LevelEditorContext.DefaultGridSize;
        [SerializeField] int _gridHeight = LevelEditorContext.DefaultGridSize;
        [SerializeField] float _leftPanelWidth = 180f;
        [SerializeField] float _rightPanelWidth = 190f;
        [SerializeField] string _autosaveJson = "";
        [SerializeField] bool _autoScreenshotEnabled = false;

        // ── Level save/load ──
        // Output folders are per-project preferences (Project Settings ▸ Level Design (Huycv)),
        // because the package itself has no idea where a host project keeps its levels.
        const double BatchSettleDelaySeconds = 0.3;

        // ── Batch screenshot capture state ──
        bool _batchRunning;
        bool _batchWaitingToSettle;
        double _batchActionTime;
        int _batchFromId, _batchEndId, _batchCurrentId;
        int _batchCapturedCount, _batchSkippedCount;
        string _batchStatusText = "";
        Rect _gridAreaScreenRect;

        // ════════════════════════════════════════════════════════
        //  Subsystems
        // ════════════════════════════════════════════════════════

        LevelEditorContext _ctx;
        ToolGroup[] _toolGroups;
        ConnectionToolGroup _connectionGroup;
        SelectToolGroup _selectGroup;
        System.Collections.Generic.Dictionary<ToolMode, ToolGroup> _toolDisplayInfo;
        GaragePopupController _garagePopup;
        GridRenderer _gridRenderer;
        ReceiverQueueRenderer _receiverQueueRenderer;
        LevelImportExport _importExport;
        StatisticsGroup _statsGroup;
        ColorChartWidget _colorChartWidget;

        // Right panel groups (OOP)
        IRightPanelGroup[] _rightPanelGroups;
        GridSizeGroup _gridSizeGroup;
        GenerateGroup _generateGroup;
        LevelIOGroup _levelIOGroup;
        ScreenshotGroup _screenshotGroup;

        // Undo/Redo
        LevelUndoSystem _undoSystem;

        // Left panel zones (OOP)
        ILeftPanelZone[] _leftPanelZones;

        // ════════════════════════════════════════════════════════
        //  Panel / Splitter
        // ════════════════════════════════════════════════════════

        Vector2 _leftPanelScroll, _rightPanelScroll, _centerScroll;
        bool _draggingLeftSplitter, _draggingRightSplitter;

        // ════════════════════════════════════════════════════════
        //  Layout dirty-check cache
        // ════════════════════════════════════════════════════════

        int _prevGridWidth, _prevGridHeight;
        float _prevWindowWidth, _prevWindowHeight, _prevLeftPanel, _prevRightPanel;
        ReceiverQueueResult[] _prevReceiverQueues;

        // ── Cached panel/grid rects (rebuilt only when layout dirty) ──
        Rect _leftPanelRect, _rightPanelRect, _gridAreaRect;
        Rect _leftSplitterRect, _rightSplitterRect;
        Rect _centerBgRect;
        float _centerContentHeight;

        // ── Cached delegates (avoid per-frame alloc) ──
        PanelMeasurer _measureLeft, _measureRight;
        PanelDrawer _drawLeft, _drawRight;

        // ════════════════════════════════════════════════════════
        //  Menu & Lifecycle
        // ════════════════════════════════════════════════════════

        [MenuItem("Tools/Level/Level Design (Huycv)")]
        static void Open()
        {
            var w = GetWindow<LevelDesignWindow>("Level Design (Huycv)");
            w.minSize = new Vector2(520f, 360f);
        }

        void OnEnable()
        {
            _ctx = new LevelEditorContext
            {
                GridWidth = _gridWidth,
                GridHeight = _gridHeight,
            };
            _ctx.InitCells();
            _ctx.InitVehicleImportData();
            _ctx.RequestRepaint = Repaint;
            _ctx.CellRectToWindow = ContentToWindowRect;
            _ctx.ShowToast = ShowToast;
            EditorApplication.update += OnEditorUpdate;

            _colorChartWidget = new ColorChartWidget();
            _statsGroup = new StatisticsGroup(new IStatisticsWidget[]
            {
                new SummaryStatsWidget(),
                _colorChartWidget,
            });

            _undoSystem = new LevelUndoSystem();

            _gridSizeGroup = new GridSizeGroup(_ctx, DoGenerateGrid);
            _generateGroup = new GenerateGroup(_ctx, DoGenerate, Repaint);
            var receiverQueuesGroup = new ReceiverQueuesGroup(_ctx, DoGenerateReceiverQueues);
            _levelIOGroup = new LevelIOGroup(_ctx, DoImport, DoImportString, DoExport, DoExportClipboard, DoClearAll,
                DoClearBoard, DoQuickSave, DoQuickLoad);
            _screenshotGroup = new ScreenshotGroup(
                () => _autoScreenshotEnabled,
                v => _autoScreenshotEnabled = v,
                () => _batchRunning,
                () => _batchStatusText,
                DoStartBatchCapture,
                DoCancelBatchCapture);
            _rightPanelGroups = new IRightPanelGroup[]
            {
                _gridSizeGroup,
                receiverQueuesGroup,
                _levelIOGroup,
                _screenshotGroup,
                _generateGroup,
                _statsGroup,
            };

            LoadPalette();

            _garagePopup = new GaragePopupController(_ctx, () => _undoSystem.PushUndo(_ctx));
            _connectionGroup = new ConnectionToolGroup(_ctx);
            _gridRenderer = new GridRenderer(_ctx);
            _receiverQueueRenderer = new ReceiverQueueRenderer(_ctx);
            _importExport = new LevelImportExport(_ctx);

            _selectGroup = new SelectToolGroup(_ctx, () => _undoSystem.PushUndo(_ctx));
            _toolGroups = new ToolGroup[]
            {
                new ObstacleToolGroup(_ctx),
                new HiddenCubeToolGroup(_ctx),
                new GarageToolGroup(_ctx, _garagePopup),
                _connectionGroup,
                _selectGroup,
                new ColorToolGroup(_ctx),
            };

            _toolDisplayInfo = new System.Collections.Generic.Dictionary<ToolMode, ToolGroup>();
            for (int i = 0; i < _toolGroups.Length; i++)
                _toolDisplayInfo[_toolGroups[i].AssociatedTool] = _toolGroups[i];

            var toggleTools  = Array.FindAll(_toolGroups, t => t.IsToggleTool);
            var paletteTools = Array.FindAll(_toolGroups, t => !t.IsToggleTool);
            _leftPanelZones = new ILeftPanelZone[]
            {
                new ToolsZone(_ctx, toggleTools),
                new PalettesZone(paletteTools),
            };

            _ctx.OnToolChanged += OnToolChanged;
            _ctx.OnToolChanged += _garagePopup.OnToolChanged;
            _ctx.OnGarageRemoved += _garagePopup.OnGarageRemoved;
            for (int i = 0; i < _toolGroups.Length; i++)
                _ctx.OnToolChanged += _toolGroups[i].OnToolChanged;

            _measureLeft = MeasureLeftPanel;
            _drawLeft = DrawLeftPanelContent;
            _measureRight = MeasureRightPanel;
            _drawRight = DrawRightPanelContent;

            _ctx.StatusDirty = true;
            _ctx.LayoutDirty = true;
            wantsMouseMove = true;

            // Restore last known state after a domain reload (script recompile) or a window
            // close/reopen — _autosaveJson is a [SerializeField] so it survives both. Best-effort:
            // corrupt/empty autosave leaves an empty grid rather than blocking the window.
            if (!string.IsNullOrEmpty(_autosaveJson))
            {
                try
                {
                    _importExport.ImportFromJson(_autosaveJson);
                    _gridSizeGroup.FieldWidth = _ctx.GridWidth.ToString();
                    _gridSizeGroup.FieldHeight = _ctx.GridHeight.ToString();
                    SyncLevelIdField();
                }
                catch
                {
                    // Corrupt autosave; leave grid empty rather than blocking the window from opening.
                }
            }
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;

            // Best-effort autosave so a domain reload (script recompile) or closing this window
            // does not silently discard unsaved level data. Never throw from here.
            if (_ctx != null && _importExport != null && _importExport.HasAnyData())
            {
                try
                {
                    _autosaveJson = _importExport.BuildExportJson().ToString(Newtonsoft.Json.Formatting.None);
                }
                catch
                {
                    // Keep whatever autosave we already had rather than risk a half-written value.
                }
            }
            else
            {
                _autosaveJson = "";
            }
        }

        // ════════════════════════════════════════════════════════
        //  OnGUI — orchestration
        // ════════════════════════════════════════════════════════

        void OnGUI()
        {
            GridRenderer.EnsureStyles();
            EnsureStyles();
            RightPanelHelpers.ResetControlNameCounter();
            _ctx.WindowWidth = position.width;
            _ctx.WindowHeight = position.height;
            SyncSerializedFields();

            // Unity IMGUI keeps keyboard focus on the last-clicked text field even after clicking
            // elsewhere (grid, buttons, empty panel space). Clear it proactively on every MouseDown;
            // a click that actually lands on a text field re-claims focus itself later in this same
            // event, so this only affects clicks that are NOT on a text field.
            if (Event.current.type == EventType.MouseDown)
                GUI.FocusControl(null);

            HandleUndoRedoShortcuts();
            HandleToolShortcuts();
            CheckLayoutDirty();
            if (_ctx.LayoutDirty)
                RebuildLayout();

            // Cached for the batch screenshot capture state machine (runs outside OnGUI, via
            // EditorApplication.update) — GUIToScreenPoint only works inside an OnGUI call.
            _gridAreaScreenRect = new Rect(
                GUIUtility.GUIToScreenPoint(new Vector2(_gridAreaRect.x, _gridAreaRect.y)),
                _gridAreaRect.size);

            DrawPanel(_leftPanelRect, ref _leftPanelScroll, _measureLeft, _drawLeft);
            DrawPanel(_rightPanelRect, ref _rightPanelScroll, _measureRight, _drawRight);
            DrawSplitter(_leftSplitterRect, ref _draggingLeftSplitter);
            DrawSplitter(_rightSplitterRect, ref _draggingRightSplitter);
            ProcessSplitterDrag();

            // Center area — always wrapped in ScrollView so all drawing
            // uses content-space coordinates (origin 0,0 at top-left of scroll)
            var scrollContent = new Rect(0f, 0f,
                _gridAreaRect.width - 14f,
                Mathf.Max(_centerContentHeight, _gridAreaRect.height));
            _centerScroll = GUI.BeginScrollView(
                _gridAreaRect, _centerScroll, scrollContent, false, false);

            if (_ctx.GridActive)
            {
                _gridRenderer.DrawGridBackground(_centerBgRect);

                if (_receiverQueueRenderer.HasQueues)
                    _receiverQueueRenderer.Draw();

                _gridRenderer.DrawCells();
                _gridRenderer.DrawGridLines();
                _gridRenderer.DrawConnections(_connectionGroup);
                _gridRenderer.DrawAxisLabels();

                for (int i = 0; i < _toolGroups.Length; i++)
                    _toolGroups[i].DrawGridOverlayPreHover();

                UpdateHover();
                _gridRenderer.DrawHoverHighlight();
                _gridRenderer.DrawHoverPreview();

                for (int i = 0; i < _toolGroups.Length; i++)
                    _toolGroups[i].DrawGridOverlayPostHover();

                ProcessGridEvents();
            }
            else
            {
                _gridRenderer.DrawEmptyState(_centerBgRect);
            }

            GUI.EndScrollView();

            if (_ctx.GridActive)
                DrawCurrentToolBanner();
            DrawToast();

            _garagePopup.DrawIfOpen(_ctx.WindowWidth, _ctx.WindowHeight);
        }

        // ════════════════════════════════════════════════════════
        //  Current Tool banner — pinned above the grid, window-space (does not scroll)
        // ════════════════════════════════════════════════════════

        const float ToolBannerHeight = 26f;
        const float ToolBannerMinWidth = 120f;
        const float ToolBannerMaxWidth = 260f;
        static readonly Color s_toolBannerBg = new Color(0.10f, 0.10f, 0.12f, 0.95f);
        static readonly Color s_toolBannerBorder = new Color(0f, 0f, 0f, 0.4f);
        static GUIStyle s_toolBannerStyle;

        // ════════════════════════════════════════════════════════
        //  Toast notifications — centered on the window, auto-fades out
        // ════════════════════════════════════════════════════════

        const double ToastVisibleSeconds = 2.5;
        const double ToastFadeSeconds = 0.5;
        const float ToastHeight = 56f;
        const float ToastMinWidth = 200f;
        const float ToastMaxWidth = 560f;
        static readonly Color s_toastBg = new Color(0.12f, 0.22f, 0.14f, 0.95f);
        static readonly Color s_toastBorder = new Color(0f, 0f, 0f, 0.5f);
        static GUIStyle s_toastStyle;

        string _toastMessage;
        double _toastShownAt;

        void ShowToast(string message)
        {
            _toastMessage = message;
            _toastShownAt = EditorApplication.timeSinceStartup;
            Repaint();
        }

        void OnEditorUpdate()
        {
            if (!string.IsNullOrEmpty(_toastMessage))
            {
                double elapsed = EditorApplication.timeSinceStartup - _toastShownAt;
                if (elapsed > ToastVisibleSeconds + ToastFadeSeconds)
                    _toastMessage = null;
                Repaint();
            }

            if (_batchRunning && _batchWaitingToSettle &&
                EditorApplication.timeSinceStartup >= _batchActionTime)
            {
                _batchWaitingToSettle = false;
                CaptureCurrentBoardScreenshot(_batchCurrentId);
                _batchCapturedCount++;
                _batchCurrentId++;
                AdvanceBatchLoad();
            }
        }

        void DrawToast()
        {
            if (string.IsNullOrEmpty(_toastMessage)) return;

            double elapsed = EditorApplication.timeSinceStartup - _toastShownAt;
            float alpha = elapsed < ToastVisibleSeconds
                ? 1f
                : Mathf.Clamp01(1f - (float)((elapsed - ToastVisibleSeconds) / ToastFadeSeconds));
            if (alpha <= 0f) return;

            if (s_toastStyle == null)
            {
                s_toastStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                };
            }

            float textW = s_toastStyle.CalcSize(new GUIContent(_toastMessage)).x;
            float w = Mathf.Clamp(textW + 48f, ToastMinWidth, ToastMaxWidth);
            var rect = new Rect((_ctx.WindowWidth - w) * 0.5f, (_ctx.WindowHeight - ToastHeight) * 0.5f, w, ToastHeight);

            var bg = s_toastBg; bg.a *= alpha;
            var border = s_toastBorder; border.a *= alpha;
            s_toastStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f * alpha);

            EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(rect, 2f), border);
            EditorGUI.DrawRect(rect, bg);
            GUI.Label(new Rect(rect.x + 16f, rect.y, rect.width - 32f, rect.height), _toastMessage, s_toastStyle);
        }

        void DrawCurrentToolBanner()
        {
            string label;
            Color accent;

            if (_ctx.ActiveTool == ToolMode.None)
            {
                label = "No Tool Selected";
                accent = new Color(0.5f, 0.5f, 0.5f, 1f);
            }
            else if (_ctx.ActiveTool == ToolMode.Eraser)
            {
                label = "Eraser";
                accent = new Color(0.6f, 0.6f, 0.6f, 1f);
            }
            else if (_ctx.ActiveTool == ToolMode.PaintColor)
            {
                label = "Paint Color";
                accent = _ctx.SelectedColorId >= 0 && _ctx.ColorLookup.TryGetValue(_ctx.SelectedColorId, out var pc)
                    ? pc : new Color(0.35f, 0.6f, 1f, 1f);
            }
            else if (_toolDisplayInfo.TryGetValue(_ctx.ActiveTool, out var group))
            {
                label = group.Title;
                accent = group.AccentColor;
            }
            else
            {
                label = _ctx.ActiveTool.ToString();
                accent = Color.white;
            }

            if (s_toolBannerStyle == null)
            {
                s_toolBannerStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }
                };
            }

            float textW = s_toolBannerStyle.CalcSize(new GUIContent(label)).x;
            float w = Mathf.Clamp(textW + 32f, ToolBannerMinWidth, ToolBannerMaxWidth);
            var rect = new Rect(_gridAreaRect.x + 8f, 8f, w, ToolBannerHeight);

            EditorGUI.DrawRect(LevelEditorDrawUtils.ExpandRect(rect, 1f), s_toolBannerBorder);
            EditorGUI.DrawRect(rect, s_toolBannerBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), accent);
            GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 14f, rect.height), label, s_toolBannerStyle);
        }

        // ════════════════════════════════════════════════════════
        //  Palette loading
        // ════════════════════════════════════════════════════════

        void LoadPalette()
        {
            _ctx.ColorLookup.Clear();
            // Loaded as a plain ScriptableObject and read through SerializedObject below, so the
            // package never needs a compile-time reference to the host project's config assembly.
            var cfg = Resources.Load<ScriptableObject>(LevelDesignSettings.PaletteResourcePath);
            if (cfg == null)
            {
                var guids = AssetDatabase.FindAssets("t:" + LevelDesignSettings.PaletteTypeName);
                if (guids.Length > 0)
                    cfg = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (cfg == null)
            {
                _ctx.PaletteEntries = new PaletteEntry[0];
                _ctx.PaletteCount = 0;
                return;
            }

            var so = new SerializedObject(cfg);
            var list = so.FindProperty("colorConfigDatas");
            if (list == null || !list.isArray)
            {
                _ctx.PaletteEntries = new PaletteEntry[0];
                _ctx.PaletteCount = 0;
                return;
            }

            int n = list.arraySize;
            _ctx.PaletteEntries = new PaletteEntry[n];
            _ctx.PaletteCount = n;
            _ctx.PaletteTooltips = new GUIContent[n];
            for (int i = 0; i < n; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                int id = e.FindPropertyRelative("materialId").intValue;
                var col = e.FindPropertyRelative("color").colorValue;
                col.a = 1f;
                _ctx.PaletteEntries[i] = new PaletteEntry { materialId = id, color = col };
                _ctx.ColorLookup[id] = col;
                _ctx.PaletteTooltips[i] = new GUIContent("", id.ToString());
            }
            _colorChartWidget.BuildIndex(_ctx.PaletteEntries, _ctx.PaletteCount);
        }

        void SyncSerializedFields()
        {
            _gridWidth = _ctx.GridWidth;
            _gridHeight = _ctx.GridHeight;
        }

        void OnToolChanged(ToolMode mode, int colorId)
        {

            Repaint();
        }

        // ════════════════════════════════════════════════════════
        //  Keyboard shortcuts
        // ════════════════════════════════════════════════════════

        void HandleUndoRedoShortcuts()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            bool ctrlOrCmd = e.control || e.command;
            if (!ctrlOrCmd) return;

            if (e.keyCode == KeyCode.Z && !e.shift)
            {
                if (_undoSystem.CanUndo)
                {
                    _undoSystem.Undo(_ctx);
                    _gridSizeGroup.FieldWidth = _ctx.GridWidth.ToString();
                    _gridSizeGroup.FieldHeight = _ctx.GridHeight.ToString();
                    SyncLevelIdField();
                    ShowToast("Undo");
                    e.Use();
                    Repaint();
                }
            }
            else if (e.keyCode == KeyCode.Y || (e.keyCode == KeyCode.Z && e.shift))
            {
                if (_undoSystem.CanRedo)
                {
                    _undoSystem.Redo(_ctx);
                    _gridSizeGroup.FieldWidth = _ctx.GridWidth.ToString();
                    _gridSizeGroup.FieldHeight = _ctx.GridHeight.ToString();
                    SyncLevelIdField();
                    ShowToast("Redo");
                    e.Use();
                    Repaint();
                }
            }
        }

        void HandleToolShortcuts()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.control || e.command || e.alt) return;
            // Never hijack digits/letters while a text field (Grid Width, Level ID, weights, ...) is focused.
            if (!string.IsNullOrEmpty(GUI.GetNameOfFocusedControl())) return;

            switch (e.keyCode)
            {
                case KeyCode.Alpha1:
                    int colorId = _ctx.SelectedColorId >= 0 ? _ctx.SelectedColorId
                        : (_ctx.PaletteCount > 0 ? _ctx.PaletteEntries[0].materialId : -1);
                    if (colorId >= 0) _ctx.SelectTool(ToolMode.PaintColor, colorId);
                    break;
                case KeyCode.Alpha2:
                    _ctx.SelectTool(ToolMode.PaintObstacle);
                    break;
                case KeyCode.Alpha3:
                    _ctx.SelectTool(ToolMode.ToggleHidden);
                    break;
                case KeyCode.Alpha4:
                    _ctx.SelectTool(ToolMode.PlaceGarage);
                    break;
                case KeyCode.Alpha5:
                    _ctx.SelectTool(ToolMode.LinkCube);
                    break;
                case KeyCode.D:
                    _ctx.SelectTool(ToolMode.Eraser);
                    break;
                case KeyCode.V:
                    _ctx.SelectTool(ToolMode.Select);
                    break;
                case KeyCode.Escape:
                    if (_ctx.ActiveTool == ToolMode.Select && _selectGroup.HasSelection)
                        _selectGroup.ClearSelection();
                    else
                        return;
                    break;
                default:
                    return;
            }
            e.Use();
            Repaint();
        }

        void SyncLevelIdField()
        {
            _levelIOGroup.FieldLevelId = _ctx.LevelId.ToString();
        }

        // ════════════════════════════════════════════════════════
        //  Right Panel — Grid Size, I/O, Stats, Tool, Shortcuts
        // ════════════════════════════════════════════════════════

        float MeasureRightPanel(float w)
        {
            float y = LevelEditorStyles.PanelPadding;
            for (int i = 0; i < _rightPanelGroups.Length; i++)
                y += _rightPanelGroups[i].MeasureHeight(w);
            return y + LevelEditorStyles.PanelPadding;
        }

        float DrawRightPanelContent(float w)
        {
            RebuildStatistics();
            float y = LevelEditorStyles.PanelPadding;
            for (int i = 0; i < _rightPanelGroups.Length; i++)
                y = _rightPanelGroups[i].Draw(y, w);
            return y + LevelEditorStyles.PanelPadding;
        }

        void DoGenerateGrid(int nw, int nh)
        {

            if (!_ctx.GridActive)
            {
                // No grid yet — activate with the requested size
                _ctx.GridWidth = nw;
                _ctx.GridHeight = nh;
                _ctx.GridActive = true;
                _ctx.LayoutDirty = true;
                _ctx.MarkStatusDirty();
                Repaint();
                return;
            }

            if (nw == _ctx.GridWidth && nh == _ctx.GridHeight)
                return; // no change

            bool shrinkW = nw < _ctx.GridWidth;
            bool shrinkH = nh < _ctx.GridHeight;

            const string originNote = "Origin: top-left corner (0,0) is preserved.\nRows/columns are added or removed from the right and bottom edges.";

            if (shrinkW || shrinkH)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("Grid: ");
                sb.Append(_ctx.GridWidth); sb.Append("×"); sb.Append(_ctx.GridHeight);
                sb.Append(" → "); sb.Append(nw); sb.Append("×"); sb.Append(nh); sb.AppendLine();
                sb.AppendLine(originNote);
                sb.AppendLine();

                int lostCubes = 0, lostObstacles = 0, lostGarages = 0, lostConnections = 0;

                for (int y = 0; y < _ctx.GridHeight; y++)
                for (int x = 0; x < _ctx.GridWidth; x++)
                {
                    if (x >= nw || y >= nh)
                    {
                        int idx = y * _ctx.GridWidth + x;
                        ref var cell = ref _ctx.Cells[idx];
                        if (cell.garageId >= 0 && _ctx.GarageMap.ContainsKey(cell.garageId))
                            lostGarages++;
                        else if (cell.colorId >= 0 && !cell.isObstacle)
                            lostCubes++;
                        else if (cell.isObstacle)
                            lostObstacles++;
                    }
                }

                foreach (long edge in _ctx.Connections)
                {
                    LevelEditorDrawUtils.UnpackEdge(edge, out int a, out int b);
                    int ax = a % _ctx.GridWidth, ay = a / _ctx.GridWidth;
                    int bx = b % _ctx.GridWidth, by = b / _ctx.GridWidth;
                    if (ax >= nw || ay >= nh || bx >= nw || by >= nh)
                        lostConnections++;
                }

                bool anyLost = lostCubes > 0 || lostObstacles > 0 || lostGarages > 0 || lostConnections > 0;

                if (anyLost)
                {
                    sb.AppendLine("⚠ The following data will be LOST:");
                    if (lostCubes > 0) { sb.Append("  • "); sb.Append(lostCubes); sb.AppendLine(" cube(s)"); }
                    if (lostObstacles > 0) { sb.Append("  • "); sb.Append(lostObstacles); sb.AppendLine(" obstacle(s)"); }
                    if (lostGarages > 0) { sb.Append("  • "); sb.Append(lostGarages); sb.AppendLine(" garage(s)"); }
                    if (lostConnections > 0) { sb.Append("  • "); sb.Append(lostConnections); sb.AppendLine(" connection(s)"); }
                    sb.AppendLine();
                    sb.Append("(Ctrl+Z can undo this.) Continue?");

                    if (!EditorUtility.DisplayDialog("Resize Warning", sb.ToString(), "Resize", "Cancel"))
                        return;
                }
                else
                {
                    EditorUtility.DisplayDialog("Resize Grid",
                        sb + "No data will be lost in the trimmed area.", "OK");
                }
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("Grid: ");
                sb.Append(_ctx.GridWidth); sb.Append("×"); sb.Append(_ctx.GridHeight);
                sb.Append(" → "); sb.Append(nw); sb.Append("×"); sb.Append(nh); sb.AppendLine();
                sb.AppendLine(originNote);
                sb.AppendLine();
                sb.Append("No data will be lost.");

                EditorUtility.DisplayDialog("Resize Grid", sb.ToString(), "OK");
            }

            ApplyGridResize(nw, nh);
        }

        void ApplyGridResize(int nw, int nh)
        {
            _undoSystem.PushUndo(_ctx);
            int oldW = _ctx.GridWidth;
            _ctx.ResizeGrid(nw, nh);
            for (int i = 0; i < _toolGroups.Length; i++)
                _toolGroups[i].OnGridResized(oldW, nw, nh);
            ShowToast(string.Concat("Resized grid to ", nw.ToString(), "×", nh.ToString()));
            Repaint();
        }

        void DoGenerate(GenerateMode mode)
        {
            if (!_ctx.GridActive)
            {
                EditorUtility.DisplayDialog("Generate", "Create a grid first (set Width and Height, then Generate Grid).", "OK");
                return;
            }

            bool needColors = mode != GenerateMode.ObstaclesOnly;
            if (needColors)
            {
                var cfg = _ctx.GenerateConfig;
                if (cfg.ColorWeights.Count == 0)
                {
                    EditorUtility.DisplayDialog("Generate", "Add at least one color before generating.", "OK");
                    return;
                }

                bool hasMissing = false;
                for (int i = 0; i < cfg.ColorWeights.Count; i++)
                {
                    if (cfg.ColorWeights[i].weight <= 0)
                    {
                        hasMissing = true;
                        break;
                    }
                }
                if (hasMissing)
                {
                    EditorUtility.DisplayDialog("Generate",
                        "Please enter a weight (> 0) for every color.", "OK");
                    return;
                }
            }

            _undoSystem.PushUndo(_ctx);
            LevelGenerator.Generate(_ctx, mode);
            _ctx.SelectTool(ToolMode.None);
            ShowToast(mode == GenerateMode.All ? "Generated obstacles + colors"
                : mode == GenerateMode.ObstaclesOnly ? "Generated obstacles" : "Generated colors");
            Repaint();
        }

        // ════════════════════════════════════════════════════════
        //  Generate Receiver Queues
        // ════════════════════════════════════════════════════════

        void DoGenerateReceiverQueues(int queueCount, int clearRatio)
        {
            var result = ReceiverQueueGenerator.Generate(_ctx, queueCount, clearRatio, out string error);
            if (result == null)
            {
                EditorUtility.DisplayDialog("Generate Receiver Queues", error, "OK");
                return;
            }

            int totalReceivers = 0;
            for (int i = 0; i < result.Length; i++)
                totalReceivers += result[i].colorTypesQueue.Length;

            _undoSystem.PushUndo(_ctx);
            _ctx.GeneratedReceiverQueues = result;
            ShowToast(string.Concat("Generated ", queueCount.ToString(), " queues, ",
                totalReceivers.ToString(), " receivers"));
            Repaint();
        }

        // ════════════════════════════════════════════════════════
        //  Import / Export / Clear actions
        // ════════════════════════════════════════════════════════

        void DoImport()
        {
            string path = EditorUtility.OpenFilePanel("Import Level JSON", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            bool proceed = !_importExport.HasAnyData() || EditorUtility.DisplayDialog(
                "Import Level", "Import will replace current level data.\nContinue?", "Import", "Cancel");
            if (!proceed) return;

            try
            {
                string json = File.ReadAllText(path);
                _undoSystem.PushUndo(_ctx);
                _importExport.ImportFromJson(json);
                _ctx.LastImportPath = path;
                _gridSizeGroup.FieldWidth = _ctx.GridWidth.ToString();
                _gridSizeGroup.FieldHeight = _ctx.GridHeight.ToString();
                SyncLevelIdField();
                ShowToast("Imported " + Path.GetFileName(path));

                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Error", "Failed to read file:\n" + ex.Message, "OK");
            }
        }

        void DoImportString(string json)
        {
            bool proceed = !_importExport.HasAnyData() || EditorUtility.DisplayDialog(
                "Import Level", "Import will replace current level data.\nContinue?", "Import", "Cancel");
            if (!proceed) return;

            try
            {
                _undoSystem.PushUndo(_ctx);
                _importExport.ImportFromJson(json);
                _gridSizeGroup.FieldWidth = _ctx.GridWidth.ToString();
                _gridSizeGroup.FieldHeight = _ctx.GridHeight.ToString();
                SyncLevelIdField();
                ShowToast("Imported from clipboard");
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Error", "Invalid JSON:\n" + ex.Message, "OK");
            }
        }

        void DoExport()
        {
            if (!_importExport.HasAnyData())
            {
                EditorUtility.DisplayDialog("Export", "No data to export.", "OK");
                return;
            }

            string dir = !string.IsNullOrEmpty(_ctx.LastImportPath)
                ? Path.GetDirectoryName(_ctx.LastImportPath) : "";
            string name = _ctx.ImportedJson != null
                ? string.Concat("Level_", _ctx.ImportedJson.Value<int>("levelIndex").ToString(), ".json")
                : "Level_New.json";
            string savePath = EditorUtility.SaveFilePanel("Export Level JSON", dir, name, "json");
            if (string.IsNullOrEmpty(savePath)) return;

            try
            {
                var jo = _importExport.BuildExportJson();
                File.WriteAllText(savePath, jo.ToString(Newtonsoft.Json.Formatting.Indented));
                ShowToast("Exported " + Path.GetFileName(savePath));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Error", "Failed to write file:\n" + ex.Message, "OK");
            }
        }

        void DoExportClipboard()
        {
            if (!_importExport.HasAnyData())
            {
                EditorUtility.DisplayDialog("Export", "No data to export.", "OK");
                return;
            }

            try
            {
                var jo = _importExport.BuildExportJson();
                GUIUtility.systemCopyBuffer = jo.ToString(Newtonsoft.Json.Formatting.Indented);
                ShowToast("Copied level data to clipboard");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Error", "Failed to export:\n" + ex.Message, "OK");
            }
        }

        // ════════════════════════════════════════════════════════
        //  Quick Save / Quick Load (fixed-folder, no file dialog)
        // ════════════════════════════════════════════════════════

        static string GetLevelsFolderAbsolutePath()
        {
            return LevelDesignSettings.ToAbsolutePath(LevelDesignSettings.LevelsFolder);
        }

        static void EnsureLevelsFolderExists()
        {
            string abs = GetLevelsFolderAbsolutePath();
            if (!Directory.Exists(abs))
                Directory.CreateDirectory(abs);
        }

        static string GetLevelFilePath(int levelId)
        {
            return Path.Combine(GetLevelsFolderAbsolutePath(), string.Concat("Level_", levelId.ToString(), ".json"));
        }

        void DoQuickSave()
        {
            if (_ctx.LevelId < 0)
            {
                EditorUtility.DisplayDialog("Quick Save", "Enter a Level ID (0 or greater) before Quick Save.", "OK");
                return;
            }
            if (!_importExport.HasAnyData())
            {
                EditorUtility.DisplayDialog("Quick Save", "No data to export.", "OK");
                return;
            }

            EnsureLevelsFolderExists();
            string path = GetLevelFilePath(_ctx.LevelId);

            if (File.Exists(path))
            {
                bool overwrite = EditorUtility.DisplayDialog("Quick Save",
                    string.Concat("Level_", _ctx.LevelId.ToString(), ".json already exists.\nOverwrite?"),
                    "Overwrite", "Cancel");
                if (!overwrite) return;
            }

            try
            {
                var jo = _importExport.BuildExportJson();
                File.WriteAllText(path, jo.ToString(Newtonsoft.Json.Formatting.Indented));
                AssetDatabase.Refresh();
                // No blocking dialog here — Quick Save is the fast path; a toast is enough
                // confirmation without interrupting the user like a modal "OK" would.
                ShowToast(string.Concat("Quick Saved Level_", _ctx.LevelId.ToString(), ".json"));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Quick Save Error", "Failed to write file:\n" + ex.Message, "OK");
            }
        }

        void DoQuickLoad()
        {
            if (_ctx.LevelId < 0)
            {
                EditorUtility.DisplayDialog("Quick Load", "Enter a Level ID (0 or greater) before Quick Load.", "OK");
                return;
            }

            string path = GetLevelFilePath(_ctx.LevelId);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Quick Load",
                    string.Concat("No saved level found for ID ", _ctx.LevelId.ToString(),
                        ".\nExpected file:\n", path), "OK");
                return;
            }

            bool proceed = !_importExport.HasAnyData() || EditorUtility.DisplayDialog(
                "Import Level", "Import will replace current level data.\nContinue?", "Import", "Cancel");
            if (!proceed) return;

            try
            {
                string json = File.ReadAllText(path);
                _undoSystem.PushUndo(_ctx);
                _importExport.ImportFromJson(json);
                _ctx.LastImportPath = path;
                _gridSizeGroup.FieldWidth = _ctx.GridWidth.ToString();
                _gridSizeGroup.FieldHeight = _ctx.GridHeight.ToString();
                SyncLevelIdField();
                ShowToast(string.Concat("Quick Loaded Level_", _ctx.LevelId.ToString(), ".json"));
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Quick Load Error", "Failed to read file:\n" + ex.Message, "OK");
            }
        }

        // ════════════════════════════════════════════════════════
        //  Batch screenshot capture (sweeps a Level ID range, OS-level screen capture)
        // ════════════════════════════════════════════════════════

        static string GetScreenshotsFolderAbsolutePath()
        {
            return LevelDesignSettings.ToAbsolutePath(LevelDesignSettings.ScreenshotsFolder);
        }

        static void EnsureScreenshotsFolderExists()
        {
            string abs = GetScreenshotsFolderAbsolutePath();
            if (!Directory.Exists(abs))
                Directory.CreateDirectory(abs);
        }

        static string GetScreenshotFilePath(int levelId)
        {
            return Path.Combine(GetScreenshotsFolderAbsolutePath(), string.Concat("Level_", levelId.ToString(), ".png"));
        }

        void DoStartBatchCapture(int fromId, int toId)
        {
            if (!_autoScreenshotEnabled)
            {
                EditorUtility.DisplayDialog("Screenshot", "Enable Auto Screenshot first.", "OK");
                return;
            }
            if (fromId > toId)
            {
                EditorUtility.DisplayDialog("Screenshot", "\"From\" must be less than or equal to \"To\".", "OK");
                return;
            }
            if (_importExport.HasAnyData())
            {
                bool proceed = EditorUtility.DisplayDialog("Capture Range",
                    string.Concat("This will replace your current unsaved board content while sweeping ",
                        "through Level_", fromId.ToString(), ".json to Level_", toId.ToString(), ".json.\n",
                        "Make sure you've saved/exported anything you want to keep first.\nContinue?"),
                    "Continue", "Cancel");
                if (!proceed) return;
            }

            EnsureScreenshotsFolderExists();
            _batchRunning = true;
            _batchWaitingToSettle = false;
            _batchFromId = fromId;
            _batchEndId = toId;
            _batchCurrentId = fromId;
            _batchCapturedCount = 0;
            _batchSkippedCount = 0;
            AdvanceBatchLoad();
        }

        void DoCancelBatchCapture()
        {
            if (!_batchRunning) return;
            _batchRunning = false;
            _batchWaitingToSettle = false;
            ShowToast(string.Concat("Capture cancelled (", _batchCapturedCount.ToString(), " captured)"));
            Repaint();
        }

        void AdvanceBatchLoad()
        {
            while (_batchRunning && _batchCurrentId <= _batchEndId)
            {
                int total = _batchEndId - _batchFromId + 1;
                int progress = _batchCurrentId - _batchFromId + 1;
                string path = GetLevelFilePath(_batchCurrentId);

                if (!File.Exists(path))
                {
                    _batchSkippedCount++;
                    _batchStatusText = string.Concat("Skipped Level_", _batchCurrentId.ToString(),
                        ".json (not found) — ", progress.ToString(), "/", total.ToString());
                    _batchCurrentId++;
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(path);
                    _ctx.SelectTool(ToolMode.None);
                    _importExport.ImportFromJson(json);
                    _ctx.LastImportPath = path;
                    _gridSizeGroup.FieldWidth = _ctx.GridWidth.ToString();
                    _gridSizeGroup.FieldHeight = _ctx.GridHeight.ToString();
                    SyncLevelIdField();
                }
                catch
                {
                    _batchSkippedCount++;
                    _batchCurrentId++;
                    continue;
                }

                _batchStatusText = string.Concat("Capturing Level_", _batchCurrentId.ToString(),
                    ".json — ", progress.ToString(), "/", total.ToString());
                _batchWaitingToSettle = true;
                _batchActionTime = EditorApplication.timeSinceStartup + BatchSettleDelaySeconds;
                Repaint();
                return;
            }

            FinishBatchCapture();
        }

        void FinishBatchCapture()
        {
            _batchRunning = false;
            _batchWaitingToSettle = false;
            _batchStatusText = "";
            AssetDatabase.Refresh();
            ShowToast(string.Concat("Captured ", _batchCapturedCount.ToString(), " screenshot(s)",
                _batchSkippedCount > 0 ? string.Concat(" (", _batchSkippedCount.ToString(), " skipped)") : ""));
            Repaint();
        }

        void CaptureCurrentBoardScreenshot(int id)
        {
            try
            {
                EnsureScreenshotsFolderExists();
                CaptureScreenRegionToPng(_gridAreaScreenRect, GetScreenshotFilePath(id));
            }
            catch (Exception ex)
            {
                Debug.LogError(string.Concat("Screenshot capture failed for Level_", id.ToString(), ".json: ", ex.Message));
            }
        }

        // OS-level screen capture (not a GUI render-to-texture): the Unity Editor window must be
        // visible on-screen and not occluded during capture, since this reads real screen pixels.
        static void CaptureScreenRegionToPng(Rect screenRectPoints, string filePath)
        {
            float scale = EditorGUIUtility.pixelsPerPoint;
            int x = Mathf.RoundToInt(screenRectPoints.x * scale);
            int y = Mathf.RoundToInt(screenRectPoints.y * scale);
            int w = Mathf.RoundToInt(screenRectPoints.width * scale);
            int h = Mathf.RoundToInt(screenRectPoints.height * scale);
            if (w <= 0 || h <= 0) return;

            using (var bmp = new System.Drawing.Bitmap(w, h))
            {
                using (var gfx = System.Drawing.Graphics.FromImage(bmp))
                {
                    gfx.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));
                }
                bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        void DoClearBoard()
        {
            if (!_ctx.HasAnyBoardData())
            {
                EditorUtility.DisplayDialog("Clear Board", "Nothing to clear.", "OK");
                return;
            }

            _undoSystem.PushUndo(_ctx);
            _ctx.ClearBoard();
            ShowToast("Cleared board");
            Repaint();
        }

        void DoClearAll()
        {
            if (!EditorUtility.DisplayDialog("Clear Level Data",
                "Are you sure you want to clear ALL level data?\n(Ctrl+Z can undo this.)",
                "Clear", "Cancel")) return;

            _undoSystem.PushUndo(_ctx);
            _ctx.ClearAll(LevelEditorContext.DefaultGridSize);
            _gridSizeGroup.FieldWidth = "";
            _gridSizeGroup.FieldHeight = "";
            SyncLevelIdField();
            ShowToast("Cleared all level data");

            Repaint();
        }

        // ════════════════════════════════════════════════════════
        //  Left Panel — delegates to ILeftPanelZone[]
        // ════════════════════════════════════════════════════════

        float MeasureLeftPanel(float w)
        {
            float y = LevelEditorStyles.PanelPadding;
            for (int i = 0; i < _leftPanelZones.Length; i++)
                y += _leftPanelZones[i].MeasureHeight(w);
            return y + LevelEditorStyles.PanelPadding;
        }

        float DrawLeftPanelContent(float w)
        {
            float y = LevelEditorStyles.PanelPadding;
            for (int i = 0; i < _leftPanelZones.Length; i++)
                y = _leftPanelZones[i].Draw(y, w);
            return y + LevelEditorStyles.PanelPadding;
        }

        // ════════════════════════════════════════════════════════
        //  Panels framework
        // ════════════════════════════════════════════════════════

        delegate float PanelMeasurer(float width);
        delegate float PanelDrawer(float width);

        void DrawPanel(Rect r, ref Vector2 scroll, PanelMeasurer measure, PanelDrawer draw)
        {
            EditorGUI.DrawRect(r, s_panelBgColor);
            float cw = r.width - 14f;
            float ch = measure(cw);
            scroll = GUI.BeginScrollView(r, scroll, new Rect(0, 0, cw, ch), false, false);
            draw(cw);
            GUI.EndScrollView();
        }

        // ════════════════════════════════════════════════════════
        //  Splitter
        // ════════════════════════════════════════════════════════

        void DrawSplitter(Rect r, ref bool dragging)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(r, hover || dragging ? s_splitterHoverColor : s_splitterColor);
            EditorGUIUtility.AddCursorRect(r, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
            {
                dragging = true;
                Event.current.Use();
            }
        }

        void ProcessSplitterDrag()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDrag)
            {
                if (_draggingLeftSplitter)
                {
                    _leftPanelWidth = Mathf.Clamp(_leftPanelWidth + e.delta.x, MinPanelWidth, MaxPanelWidth);
                    _ctx.LayoutDirty = true;
                    e.Use(); Repaint();
                }
                else if (_draggingRightSplitter)
                {
                    _rightPanelWidth = Mathf.Clamp(_rightPanelWidth - e.delta.x, MinPanelWidth, MaxPanelWidth);
                    _ctx.LayoutDirty = true;
                    e.Use(); Repaint();
                }
            }
            if (e.type == EventType.MouseUp && (_draggingLeftSplitter || _draggingRightSplitter))
            {
                _draggingLeftSplitter = _draggingRightSplitter = false;
                e.Use();
            }
        }

        // ════════════════════════════════════════════════════════
        //  Layout
        // ════════════════════════════════════════════════════════

        void CheckLayoutDirty()
        {
            float w = _ctx.WindowWidth, h = _ctx.WindowHeight;
            if (_prevWindowWidth != w || _prevWindowHeight != h ||
                _prevGridWidth != _ctx.GridWidth || _prevGridHeight != _ctx.GridHeight ||
                _prevLeftPanel != _leftPanelWidth || _prevRightPanel != _rightPanelWidth ||
                _prevReceiverQueues != _ctx.GeneratedReceiverQueues)
            {
                _ctx.LayoutDirty = true;
            }
        }

        void RebuildLayout()
        {
            _ctx.LayoutDirty = false;
            float winW = _ctx.WindowWidth;
            float winH = _ctx.WindowHeight;
            _prevGridWidth = _ctx.GridWidth;
            _prevGridHeight = _ctx.GridHeight;
            _prevWindowWidth = winW;
            _prevWindowHeight = winH;
            _prevLeftPanel = _leftPanelWidth;
            _prevRightPanel = _rightPanelWidth;
            _prevReceiverQueues = _ctx.GeneratedReceiverQueues;

            // Center zone between panels (window-space)
            float gridL = _leftPanelWidth + SplitterWidth;
            float gridR = winW - _rightPanelWidth - SplitterWidth;
            float centerW = gridR - gridL;
            float viewportH = winH;

            // Content-space available width (inside scroll / center zone)
            float contentW = centerW - 14f;          // reserve scrollbar
            float aW = contentW - GridPadding * 2f - AxisLabelSize;

            if (aW <= 0f || viewportH <= 0f)
            {
                _ctx.CachedCellCount = 0;
                return;
            }

            // Measure queues height (if any)
            float queuesH = 0f;
            if (_receiverQueueRenderer.HasQueues)
                queuesH = _receiverQueueRenderer.MeasureQueuesHeight(aW);

            // CellSize: fit by width always; also fit by height when no queues
            // to preserve original behavior. When queues present, allow scroll.
            bool hasQueues = _receiverQueueRenderer.HasQueues;
            float bottomPad = hasQueues ? GridBottomPadding : GridPadding;

            if (hasQueues)
                _ctx.CellSize = Mathf.Floor(Mathf.Min(aW / _ctx.GridWidth, MaxCellSize));
            else
            {
                float gridAvailH = viewportH - AxisLabelSize - GridPadding * 2f;
                _ctx.CellSize = Mathf.Floor(Mathf.Min(aW / _ctx.GridWidth, gridAvailH / _ctx.GridHeight, MaxCellSize));
            }
            if (_ctx.CellSize < 1f) _ctx.CellSize = 1f;

            _ctx.TotalGridWidth = _ctx.CellSize * _ctx.GridWidth;
            _ctx.TotalGridHeight = _ctx.CellSize * _ctx.GridHeight;

            // Total content height (queues + axis labels + grid + padding)
            float totalContentH = GridPadding + queuesH + AxisLabelSize
                + _ctx.TotalGridHeight + bottomPad;

            // Content-space Y origin: center if fits, else top (scroll)
            float contentStartY;
            if (totalContentH <= viewportH)
                contentStartY = Mathf.Floor((viewportH - totalContentH) * 0.5f);
            else
                contentStartY = GridPadding;

            // All coordinates below are CONTENT-SPACE (origin 0,0 = top-left of scroll content)
            _ctx.GridOriginX = Mathf.Floor(GridPadding + (contentW - GridPadding * 2f - AxisLabelSize - _ctx.TotalGridWidth) * 0.5f + AxisLabelSize);
            _ctx.GridOriginY = Mathf.Floor(contentStartY + queuesH + AxisLabelSize);

            // Rebuild queues layout (above grid)
            if (_receiverQueueRenderer.HasQueues)
            {
                float queuesBottomY = contentStartY + queuesH;
                _receiverQueueRenderer.RebuildLayout(_ctx.GridOriginX, queuesBottomY,
                    _ctx.TotalGridWidth, aW);
            }

            // Store for OnGUI
            _centerContentHeight = totalContentH;

            int cc = _ctx.GridWidth * _ctx.GridHeight;
            if (_ctx.CellRects == null || _ctx.CellRects.Length < cc)
                _ctx.CellRects = new Rect[cc];
            _ctx.CachedCellCount = cc;

            for (int y = 0; y < _ctx.GridHeight; y++)
            {
                float py = _ctx.GridOriginY + y * _ctx.CellSize;
                for (int x = 0; x < _ctx.GridWidth; x++)
                    _ctx.CellRects[y * _ctx.GridWidth + x] = new Rect(_ctx.GridOriginX + x * _ctx.CellSize, py, _ctx.CellSize, _ctx.CellSize);
            }

            _gridRenderer.UpdateAxisLabelRects(AxisLabelSize);
            _gridRenderer.UpdateCachedFontSizes();

            // Cache panel/grid rects (window-space)
            _leftPanelRect = new Rect(0f, 0f, _leftPanelWidth, winH);
            _leftSplitterRect = new Rect(_leftPanelWidth, 0f, SplitterWidth, winH);
            _gridAreaRect = new Rect(gridL, 0f, centerW, winH);
            _rightSplitterRect = new Rect(gridR, 0f, SplitterWidth, winH);
            _rightPanelRect = new Rect(gridR + SplitterWidth, 0f, _rightPanelWidth, winH);

            // Background rect (content-space for scroll)
            _centerBgRect = new Rect(0f, 0f, contentW, Mathf.Max(totalContentH, viewportH));
        }

        Rect ContentToWindowRect(Rect contentRect)
        {
            return new Rect(
                contentRect.x + _gridAreaRect.x - _centerScroll.x,
                contentRect.y + _gridAreaRect.y - _centerScroll.y,
                contentRect.width, contentRect.height);
        }

        // ════════════════════════════════════════════════════════
        //  Hover
        // ════════════════════════════════════════════════════════

        void UpdateHover()
        {
            var e = Event.current;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                int px = _ctx.HoverX, py = _ctx.HoverY;
                GetCellUnderMouse(e.mousePosition, out _ctx.HoverX, out _ctx.HoverY);
                if (px != _ctx.HoverX || py != _ctx.HoverY)
                    Repaint();
            }
            else if (e.type == EventType.MouseLeaveWindow && (_ctx.HoverX != -1 || _ctx.HoverY != -1))
            {
                _ctx.HoverX = _ctx.HoverY = -1;
                Repaint();
            }
        }

        void GetCellUnderMouse(Vector2 mp, out int cx, out int cy)
        {
            float lx = mp.x - _ctx.GridOriginX;
            float ly = mp.y - _ctx.GridOriginY;
            if (lx < 0f || ly < 0f || lx >= _ctx.TotalGridWidth || ly >= _ctx.TotalGridHeight || _ctx.CellSize <= 0f)
            {
                cx = cy = -1;
                return;
            }
            cx = Mathf.Min((int)(lx / _ctx.CellSize), _ctx.GridWidth - 1);
            cy = Mathf.Min((int)(ly / _ctx.CellSize), _ctx.GridHeight - 1);
        }

        // ════════════════════════════════════════════════════════
        //  Grid events
        // ════════════════════════════════════════════════════════

        void ProcessGridEvents()
        {
            var e = Event.current;

            if (e.type == EventType.MouseUp)
            {
                for (int i = 0; i < _toolGroups.Length; i++)
                {
                    if (_toolGroups[i].CanHandleTool(_ctx.ActiveTool))
                        _toolGroups[i].OnMouseUp();
                }
                return;
            }

            bool isClick = e.type == EventType.MouseDown;
            bool isDrag = e.type == EventType.MouseDrag;
            if (!isClick && !isDrag) return;

            var windowMouse = new Vector2(
                e.mousePosition.x + _gridAreaRect.x - _centerScroll.x,
                e.mousePosition.y + _gridAreaRect.y - _centerScroll.y);
            if (_garagePopup.ContainsMouse(windowMouse)) return;

            GetCellUnderMouse(e.mousePosition, out int cx, out int cy);
            if (cx < 0 || cy < 0) return;

            int idx = cy * _ctx.GridWidth + cx;
            ref var cell = ref _ctx.Cells[idx];
            bool hasGarage = cell.garageId >= 0 && _ctx.GarageMap.ContainsKey(cell.garageId);

            if (e.button == 0)
            {
                if (isClick && hasGarage &&
                    _ctx.ActiveTool != ToolMode.Eraser && _ctx.ActiveTool != ToolMode.LinkCube &&
                    _ctx.ActiveTool != ToolMode.Select)
                {
                    _garagePopup.Open(cell.garageId, ContentToWindowRect(_ctx.CellRects[idx]), _ctx.WindowWidth);
                    e.Use();
                    Repaint();
                    return;
                }

                if (isClick)
                    _undoSystem.PushUndo(_ctx);

                for (int i = 0; i < _toolGroups.Length; i++)
                {
                    var group = _toolGroups[i];
                    if (!group.CanHandleTool(_ctx.ActiveTool)) continue;
                    if (group.HandleCellEvent(idx, cx, cy, ref cell, isClick, isDrag, hasGarage))
                        return;
                }
            }
            else if (e.button == 1)
            {
                if (isClick)
                    _undoSystem.PushUndo(_ctx);
                HandleRightClick(idx, ref cell, isClick, hasGarage);
            }
        }

        void HandleRightClick(int idx, ref CellData cell, bool isClick, bool hasGarage)
        {
            if (_connectionGroup.HasPendingLink)
            {
                _connectionGroup.CancelPendingLink();
                Event.current.Use();
                Repaint();
                return;
            }

            if (hasGarage)
            {
                _ctx.RemoveGarage(cell.garageId);
                Event.current.Use();
                _ctx.MarkStatusDirty();
                ShowToast("Garage removed");
                Repaint();
            }
            else if (cell.colorId != -1 || cell.isObstacle || cell.isHidden)
            {
                _ctx.RemoveConnectionsForCell(idx);
                cell.colorId = -1;
                cell.isObstacle = false;
                cell.isHidden = false;
                _ctx.VehicleImportData[idx] = default;
                Event.current.Use();
                _ctx.MarkStatusDirty();
                Repaint();
            }
            else if (isClick)
            {
                Event.current.Use();
            }
        }

        void RebuildStatistics()
        {
            if (!_ctx.StatusDirty) return;
            _ctx.StatusDirty = false;
            _statsGroup.Rebuild(_ctx);
        }

        // ════════════════════════════════════════════════════════
        //  Styles
        // ════════════════════════════════════════════════════════

        static void EnsureStyles()
        {
            LevelEditorStyles.EnsureStyles();
            RightPanelHelpers.EnsureStyles();
            LeftPanelHelpers.EnsureStyles();
        }

    }
}
