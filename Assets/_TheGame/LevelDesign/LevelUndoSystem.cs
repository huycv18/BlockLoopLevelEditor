using System.Collections.Generic;

namespace BlockLoop.LevelDesign
{
    // ════════════════════════════════════════════════════════
    //  LevelUndoSystem — bounded snapshot-based undo/redo
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// Tool-scoped undo/redo. Not Unity's built-in Undo system: LevelEditorContext is not a
    /// UnityEngine.Object and its Dictionary/HashSet fields are not Unity-serializable.
    /// Callers must call PushUndo(ctx) BEFORE mutating ctx, at every meaningful checkpoint.
    /// </summary>
    internal sealed class LevelUndoSystem
    {
        public const int MaxStackSize = 50;

        readonly List<LevelSnapshot> _undoStack = new List<LevelSnapshot>();
        readonly List<LevelSnapshot> _redoStack = new List<LevelSnapshot>();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>Call BEFORE mutating ctx. Captures current state and clears the redo stack.</summary>
        public void PushUndo(LevelEditorContext ctx)
        {
            _undoStack.Add(LevelSnapshotUtil.Capture(ctx));
            if (_undoStack.Count > MaxStackSize)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();
        }

        public void Undo(LevelEditorContext ctx)
        {
            if (_undoStack.Count == 0) return;
            var toRestore = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Add(LevelSnapshotUtil.Capture(ctx));
            LevelSnapshotUtil.Restore(toRestore, ctx);
        }

        public void Redo(LevelEditorContext ctx)
        {
            if (_redoStack.Count == 0) return;
            var toRestore = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            _undoStack.Add(LevelSnapshotUtil.Capture(ctx));
            LevelSnapshotUtil.Restore(toRestore, ctx);
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
