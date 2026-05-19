using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;

namespace GSWEngine
{
    public partial class StatsForm : Form
    {
        private void FinalizeSort()
        {
            var unstamped = _ctx.trackers.Except(_sortQueue).ToList();
            _ctx.trackers.Clear();
            _ctx.trackers.AddRange(_sortQueue);
            _ctx.trackers.AddRange(unstamped);
            for (int i = 0; i < _ctx.trackers.Count; i++) _ctx.trackers[i].DisplayIndex = i;

            _sortQueue.Clear();
            _isSortMode = false;
            _ctx.SaveSettings();
            UpdateUI();
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (_isFinalizingSort) return;

            if (e.Y >= 0 && e.Y <= 25)
            {
                if (e.X >= 65 && e.X <= 82) { GridPainter.PressedIcon = 1; UpdateUI(); }
                else if (e.X >= 83 && e.X <= 100) { GridPainter.PressedIcon = 2; UpdateUI(); }
            }
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isFinalizingSort) return;

            if (GridPainter.PressedIcon != 0)
            {
                int clicked = GridPainter.PressedIcon;
                GridPainter.PressedIcon = 0; // Instantly scale down

                if (e.Y >= 0 && e.Y <= 25)
                {
                    if (clicked == 1 && e.X >= 65 && e.X <= 82)
                    {
                        _ctx.trackers.Sort((a, b) => a.DisplayName.CompareTo(b.DisplayName));
                        for (int i = 0; i < _ctx.trackers.Count; i++) _ctx.trackers[i].DisplayIndex = i;
                        _ctx.SaveSettings();
                    }
                    else if (clicked == 2 && e.X >= 83 && e.X <= 100)
                    {
                        _isSortMode = !_isSortMode;
                        if (_isSortMode)
                        {
                            _sortQueue.Clear();
                            _ctx.ExecutionHistory.Add("[System] Custom Sort: Click stores in the desired order.");
                        }
                        else { FinalizeSort(); return; }
                    }
                }
                UpdateUI();
            }
        }

        private async void Canvas_MouseClick(object sender, MouseEventArgs e)
        {
            if (_isFinalizingSort) return;
            if (e.Y >= 0 && e.Y <= 25) return;

            int rowHeight = 25;
            int startY = 30;
            var visibleCount = Math.Min(_ctx.trackers.Count, 12);

            for (int i = 0; i < visibleCount; i++)
            {
                int y = startY + (i * rowHeight);
                var store = _ctx.trackers[i];

                // 4. SORT STAMP LOGIC
                if (_isSortMode && e.X >= 10 && e.X <= 100 && e.Y >= y && e.Y <= y + rowHeight)
                {
                    if (_sortQueue.Contains(store))
                    {
                        _sortQueue.Remove(store);
                    }
                    else
                    {
                        _sortQueue.Add(store);
                        if (_sortQueue.Count == visibleCount)
                        {
                            _isFinalizingSort = true;
                            UpdateUI();
                            await System.Threading.Tasks.Task.Delay(500);
                            FinalizeSort();
                            _isFinalizingSort = false;
                            return;
                        }
                    }
                    UpdateUI();
                    return;
                }

                // 1. DROP (Delete)
                if (e.X >= 105 && e.X <= 160 && e.Y >= y - 4 && e.Y <= y + 21)
                {
                    string removedStoreName = store.DisplayName;
                    _ctx.trackers.RemoveAt(i);

                    // --- XBOX DEPENDENCY REMOVAL FIX ---
                    if (removedStoreName.Equals("EA Desktop", StringComparison.OrdinalIgnoreCase) ||
                        removedStoreName.Equals("Ubisoft", StringComparison.OrdinalIgnoreCase) ||
                        removedStoreName.Equals("Battle.net", StringComparison.OrdinalIgnoreCase))
                    {
                        var xbox = _ctx.trackers.FirstOrDefault(t => t.DisplayName.Equals("Xbox", StringComparison.OrdinalIgnoreCase));
                        if (xbox != null)
                        {
                            _ctx.trackers.Remove(xbox);
                            _ctx.AppendToHistory("[System] Xbox removed:Dependency requirement.");
                        }
                    }

                    _ctx.SaveSettings();
                    UpdateUI();
                    return;
                }

                // 2. KEEP (Shield)
                if (e.X >= 355 && e.X <= 400 && e.Y >= y - 4 && e.Y <= y + 21)
                {
                    store.ManualOverride = !store.ManualOverride;
                    _ctx.SaveSettings();
                    UpdateUI();
                    return;
                }

                // 3. STOP (Action)
                if (e.X >= 445 && e.X <= 495 && e.Y >= y - 4 && e.Y <= y + 21)
                {
                    if (store.ManualOverride) return;

                    // Only block the click if it is actually OFFLINE
                    if (string.Equals(store.CurrentStatus, "OFFLINE", StringComparison.OrdinalIgnoreCase)) return;

                    var cache = new Dictionary<int, System.Diagnostics.Process>();
                    foreach (var p in System.Diagnostics.Process.GetProcesses())
                    {
                        if (!cache.ContainsKey(p.Id)) cache[p.Id] = p;
                        else p.Dispose();
                    }

                    var family = StoreDetector.GetFamilyPids(store.DisplayName, store.ProcessName, cache);
                    foreach (var p in cache.Values) p.Dispose();

                    Executioner.TerminateStore(store, family, (msg) => _ctx.ExecutionHistory.Add(msg));

                    store.CurrentStatus = "OFFLINE";
                    store.CpuUsage = 0;
                    store.TimeLeft = store.MaxTime;

                    UpdateUI();
                    return;
                }
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_ctx.trackers == null) return;

            bool foundHover = false;

            if (!foundHover && _lastTooltipRow != -1) { _canvasToolTip.SetToolTip(canvas, string.Empty); _lastTooltipRow = -1; }
        }
    }
}