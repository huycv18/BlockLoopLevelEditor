using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Huycv.LevelDesign
{
    internal sealed class LevelImportExport
    {
        readonly LevelEditorContext _ctx;

        public LevelImportExport(LevelEditorContext ctx)
        {
            _ctx = ctx;
        }

        // ════════════════════════════════════════════════════════
        //  Import
        // ════════════════════════════════════════════════════════

        public void ImportFromJson(string json)
        {
            JObject jo;
            try { jo = JObject.Parse(json); }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import Error", "Invalid JSON:\n" + ex.Message, "OK");
                return;
            }

            ClearEditorStateForImport();
            _ctx.ImportedJson = jo;

            _ctx.GridWidth  = Mathf.Clamp(jo.Value<int>("gridWidth"),  LevelEditorContext.MinGridSize, LevelEditorContext.MaxGridSize);
            _ctx.GridHeight = Mathf.Clamp(jo.Value<int>("gridHeight"), LevelEditorContext.MinGridSize, LevelEditorContext.MaxGridSize);
            _ctx.LevelId = Mathf.Max(-1, jo.Value<int>("levelIndex"));
            int maxIdx = _ctx.GridWidth * _ctx.GridHeight;

            // Obstacles: gridSlotsData[].gridSlotType == 3
            var slots = jo["gridSlotsData"] as JArray;
            if (slots != null)
            {
                foreach (JObject s in slots)
                {
                    if (s.Value<int>("gridSlotType") != 3) continue;
                    int idx = s.Value<int>("coordinateY") * _ctx.GridWidth + s.Value<int>("coordinateX");
                    if (idx >= 0 && idx < maxIdx)
                        _ctx.Cells[idx].isObstacle = true;
                }
            }

            // Vehicles → colored cubes + non-editable per-cell data
            var vehicles = jo["vehiclesData"] as JArray;
            if (vehicles != null)
            {
                foreach (JObject v in vehicles)
                {
                    int vx = v.Value<int>("coordinateX"), vy = v.Value<int>("coordinateY");
                    if (vx < 0 || vx >= _ctx.GridWidth || vy < 0 || vy >= _ctx.GridHeight) continue;
                    int idx = vy * _ctx.GridWidth + vx;

                    _ctx.Cells[idx].colorId    = v.Value<int>("entityColorType");
                    _ctx.Cells[idx].isHidden   = v.Value<bool>("isHidden");
                    _ctx.Cells[idx].isObstacle = false;

                    _ctx.VehicleImportData[idx] = new VehicleImportData
                    {
                        hasData       = true,
                        hasIce        = v.Value<bool>("hasIce"),
                        iceCount      = v.Value<int>("iceCount"),
                        directionMode = v.Value<int>("directionMode"),
                    };
                }
            }

            // Garages
            var garages = jo["garagesData"] as JArray;
            if (garages != null)
            {
                foreach (JObject g in garages)
                {
                    int gx = g.Value<int>("coordinateX"), gy = g.Value<int>("coordinateY");
                    if (gx < 0 || gx >= _ctx.GridWidth || gy < 0 || gy >= _ctx.GridHeight) continue;

                    int gid = _ctx.CreateGarage(gx, gy);
                    int idx = gy * _ctx.GridWidth + gx;
                    _ctx.Cells[idx].garageId   = gid;
                    _ctx.Cells[idx].colorId    = -1;
                    _ctx.Cells[idx].isObstacle = false;

                    var info = _ctx.GarageMap[gid];
                    info.directionType = g.Value<int>("directionType");

                    var cars = g["carsData"] as JArray;
                    if (cars != null)
                    {
                        foreach (JObject car in cars)
                            info.carColors.Add(car.Value<int>("entityColorType"));
                        LevelEditorContext.UpdateGarageCountCache(info);
                    }

                    _ctx.GarageImportGUIDs[gid] = g.Value<int>("collectToolGUID");
                }
            }

            // Connections (flat index pairs)
            var conns = jo["vehicleConnectionsData"] as JArray;
            if (conns != null)
            {
                foreach (JObject c in conns)
                {
                    int a = c.Value<int>("firstConnectedIndex");
                    int b = c.Value<int>("secondConnectedIndex");
                    if (a >= 0 && a < maxIdx && b >= 0 && b < maxIdx)
                        _ctx.Connections.Add(LevelEditorDrawUtils.PackEdge(a, b));
                }
            }

            // Receiver queues (passengersQueuesData)
            var queuesArr = jo["passengersQueuesData"] as JArray;
            if (queuesArr != null && queuesArr.Count > 0)
            {
                var queues = new ReceiverQueueResult[queuesArr.Count];
                for (int i = 0; i < queuesArr.Count; i++)
                {
                    var qObj = (JObject)queuesArr[i];
                    var colorsArr = qObj["colorTypesQueue"] as JArray;
                    int[] colors = colorsArr != null ? new int[colorsArr.Count] : Array.Empty<int>();
                    for (int c = 0; c < colors.Length; c++)
                        colors[c] = colorsArr[c].Value<int>();

                    queues[i] = new ReceiverQueueResult
                    {
                        queueIndex = qObj.Value<int>("queueIndex"),
                        colorTypesQueue = colors,
                    };
                }
                _ctx.GeneratedReceiverQueues = queues;
            }

            _ctx.GridActive = true;
            _ctx.LayoutDirty = true;
            _ctx.MarkStatusDirty();
        }

        void ClearEditorStateForImport()
        {
            for (int i = 0; i < LevelEditorContext.MaxCellCount; i++)
            {
                _ctx.Cells[i].colorId = -1;
                _ctx.Cells[i].isObstacle = false;
                _ctx.Cells[i].isHidden = false;
                _ctx.Cells[i].garageId = -1;
                _ctx.VehicleImportData[i] = default;
            }

            _ctx.GarageMap.Clear();
            _ctx.NextGarageId = 0;
            _ctx.GarageImportGUIDs.Clear();

            _ctx.Connections.Clear();
            _ctx.GeneratedReceiverQueues = null;

            // Fire OnToolChanged to reset tool-local state (_linkFirstIdx, popup, etc.)
            _ctx.SelectTool(ToolMode.None);
        }

        // ════════════════════════════════════════════════════════
        //  Export
        // ════════════════════════════════════════════════════════

        public JObject BuildExportJson()
        {
            JObject jo;
            if (_ctx.ImportedJson != null)
            {
                jo = (JObject)_ctx.ImportedJson.DeepClone();
            }
            else
            {
                jo = new JObject
                {
                    ["colorHexCodes"] = new JArray(),
                    ["levelIndex"] = 0,
                    ["passengerQueuesCount"] = 0,
                    ["hasCurtainCovered"] = false,
                    ["difficultyType"] = 0,
                    ["passengersQueuesData"] = new JArray(),
                };
            }

            jo["gridWidth"]  = _ctx.GridWidth;
            jo["gridHeight"] = _ctx.GridHeight;
            if (_ctx.LevelId >= 0)
                jo["levelIndex"] = _ctx.LevelId;
            jo["gridSlotsData"]          = BuildGridSlotsArray();
            jo["vehiclesData"]           = BuildVehiclesArray();
            jo["garagesData"]            = BuildGaragesArray();
            jo["vehicleConnectionsData"] = BuildConnectionsArray();

            if (_ctx.GeneratedReceiverQueues != null)
            {
                jo["passengersQueuesData"] = BuildReceiverQueuesArray();
                jo["passengerQueuesCount"] = _ctx.GeneratedReceiverQueues.Length;
            }

            return jo;
        }

        public bool HasAnyData()
        {
            if (!_ctx.GridActive) return false;
            if (_ctx.GarageMap.Count > 0 || _ctx.Connections.Count > 0) return true;
            int total = _ctx.GridWidth * _ctx.GridHeight;
            for (int i = 0; i < total; i++)
            {
                if (_ctx.Cells[i].colorId >= 0 || _ctx.Cells[i].isObstacle) return true;
            }
            return false;
        }

        // ════════════════════════════════════════════════════════
        //  Build arrays
        // ════════════════════════════════════════════════════════

        JArray BuildGridSlotsArray()
        {
            var arr = new JArray();
            for (int y = 0; y < _ctx.GridHeight; y++)
            for (int x = 0; x < _ctx.GridWidth; x++)
            {
                int idx = y * _ctx.GridWidth + x;
                ref var cell = ref _ctx.Cells[idx];
                int slotType = cell.isObstacle ? 3
                    : (cell.colorId >= 0 || cell.garageId >= 0) ? 1
                    : 0;
                arr.Add(new JObject
                {
                    ["coordinateX"] = x,
                    ["coordinateY"] = y,
                    ["gridSlotType"] = slotType,
                });
            }
            return arr;
        }

        JArray BuildVehiclesArray()
        {
            Dictionary<long, JObject> origVehicles = null;
            if (_ctx.ImportedJson != null)
            {
                var imported = _ctx.ImportedJson["vehiclesData"] as JArray;
                if (imported != null)
                {
                    origVehicles = new Dictionary<long, JObject>(imported.Count);
                    foreach (JObject v in imported)
                    {
                        long key = LevelEditorDrawUtils.PackCoordKey(v.Value<int>("coordinateX"), v.Value<int>("coordinateY"));
                        origVehicles[key] = v;
                    }
                }
            }

            var result = new JArray();
            for (int y = 0; y < _ctx.GridHeight; y++)
            for (int x = 0; x < _ctx.GridWidth; x++)
            {
                int idx = y * _ctx.GridWidth + x;
                ref var cell = ref _ctx.Cells[idx];
                if (cell.colorId < 0 || cell.isObstacle || cell.garageId >= 0) continue;

                long coordKey = LevelEditorDrawUtils.PackCoordKey(x, y);
                JObject orig = null;
                bool hasImport = _ctx.VehicleImportData[idx].hasData
                              && origVehicles != null
                              && origVehicles.TryGetValue(coordKey, out orig);

                JObject vj = hasImport ? (JObject)orig.DeepClone() : new JObject();

                vj["entityColorType"] = cell.colorId;
                vj["isHidden"]        = cell.isHidden;
                vj["coordinateX"]     = x;
                vj["coordinateY"]     = y;

                if (_ctx.VehicleImportData[idx].hasData)
                {
                    vj["hasIce"]        = _ctx.VehicleImportData[idx].hasIce;
                    vj["iceCount"]      = _ctx.VehicleImportData[idx].iceCount;
                    vj["directionMode"] = _ctx.VehicleImportData[idx].directionMode;
                }
                else if (!hasImport)
                {
                    vj["hasIce"]        = false;
                    vj["iceCount"]      = 0;
                    vj["directionMode"] = 0;
                }

                result.Add(vj);
            }
            return result;
        }

        JArray BuildGaragesArray()
        {
            Dictionary<long, JObject> origGarages = null;
            if (_ctx.ImportedJson != null)
            {
                var imported = _ctx.ImportedJson["garagesData"] as JArray;
                if (imported != null)
                {
                    origGarages = new Dictionary<long, JObject>(imported.Count);
                    foreach (JObject g in imported)
                    {
                        long key = LevelEditorDrawUtils.PackCoordKey(g.Value<int>("coordinateX"), g.Value<int>("coordinateY"));
                        origGarages[key] = g;
                    }
                }
            }

            var result = new JArray();
            foreach (var kv in _ctx.GarageMap)
            {
                var info = kv.Value;
                if (info.cellX >= _ctx.GridWidth || info.cellY >= _ctx.GridHeight) continue;
                long coordKey = LevelEditorDrawUtils.PackCoordKey(info.cellX, info.cellY);
                JObject orig = null;
                bool hasImport = origGarages != null && origGarages.TryGetValue(coordKey, out orig);

                JObject gj = hasImport ? (JObject)orig.DeepClone() : new JObject();

                gj["coordinateX"]   = info.cellX;
                gj["coordinateY"]   = info.cellY;
                gj["directionType"] = info.directionType;

                var carsArr = new JArray();
                foreach (int colorId in info.carColors)
                    carsArr.Add(new JObject { ["entityColorType"] = colorId });
                gj["carsData"] = carsArr;

                if (_ctx.GarageImportGUIDs.TryGetValue(kv.Key, out int guid))
                    gj["collectToolGUID"] = guid;
                else if (!hasImport)
                    gj["collectToolGUID"] = 0;

                result.Add(gj);
            }
            return result;
        }

        JArray BuildConnectionsArray()
        {
            int maxIdx = _ctx.GridWidth * _ctx.GridHeight;
            var result = new JArray();
            foreach (long edge in _ctx.Connections)
            {
                LevelEditorDrawUtils.UnpackEdge(edge, out int a, out int b);
                if (a >= maxIdx || b >= maxIdx) continue;
                result.Add(new JObject
                {
                    ["firstConnectedIndex"]  = a,
                    ["secondConnectedIndex"] = b,
                });
            }
            return result;
        }

        JArray BuildReceiverQueuesArray()
        {
            var arr = new JArray();
            for (int q = 0; q < _ctx.GeneratedReceiverQueues.Length; q++)
            {
                ref var queue = ref _ctx.GeneratedReceiverQueues[q];
                var colorArr = new JArray();
                for (int i = 0; i < queue.colorTypesQueue.Length; i++)
                    colorArr.Add(queue.colorTypesQueue[i]);

                arr.Add(new JObject
                {
                    ["queueIndex"] = queue.queueIndex,
                    ["colorTypesQueue"] = colorArr,
                    ["hiddenIndexes"] = new JArray(),
                });
            }
            return arr;
        }
    }
}
