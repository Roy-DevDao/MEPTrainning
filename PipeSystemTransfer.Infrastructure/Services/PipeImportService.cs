using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using PipeSystemTransfer.Core.Interfaces;
using PipeSystemTransfer.Core.Models;

namespace PipeSystemTransfer.Infrastructure.Services
{
    public class PipeImportService : IImportService
    {
        private readonly Document _doc;

        public PipeImportService(Document doc)
        {
            _doc = doc;
        }

        public ImportResult ImportPipeSystem(PipeSystemDto pipeSystem, Action<ProgressReport> onProgress = null)
        {
            var result = new ImportResult();
            int total = pipeSystem.Pipes.Count + pipeSystem.Fittings.Count;
            int current = 0;

            try
            {
                Report(onProgress, 0, total, "Kích hoạt family...");
                ActivateRequiredSymbols(pipeSystem, result);
                Report(onProgress, 0, total, "Bắt đầu tạo phần tử...");

                using (var tx = new Transaction(_doc, "Import Pipe System"))
                {
                    ApplyFastFailureHandling(tx);
                    tx.Start();
                    try
                    {
                        var pipeTypeMap   = BuildPipeTypeMap();
                        var levelMap      = BuildLevelMap();
                        var systemTypeMap = BuildPipeSystemTypeMap();
                        var symbolCache   = BuildSymbolCache();

                        var createdPipes    = new List<Pipe>();
                        var createdFittings = new List<FamilyInstance>();

                        result.CreatedPipes = CreatePipes(
                            pipeSystem.Pipes, pipeTypeMap, levelMap, systemTypeMap, result,
                            onProgress, ref current, total, createdPipes);

                        result.CreatedFittings = CreateFittings(
                            pipeSystem.Fittings, levelMap, symbolCache, result,
                            onProgress, ref current, total, createdFittings);

                        Report(onProgress, current, total, "Nối kết nối...");
                        result.JoinedConnectors = ConnectAllElements(createdPipes, createdFittings, result.ErrorLog);

                        tx.Commit();
                        result.Success = true;
                        Report(onProgress, total, total, "Hoàn tất import!");
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        result.Success = false;
                        result.ErrorMessage = ex.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            return result;
        }

        private void ActivateRequiredSymbols(PipeSystemDto pipeSystem, ImportResult result)
        {
            var symbolCache = BuildSymbolCache();
            bool anyActivated = false;

            using (var tx = new Transaction(_doc, "Activate Family Symbols"))
            {
                tx.Start();
                foreach (var dto in pipeSystem.Fittings.Cast<PipeElementDto>())
                {
                    var key = (dto.FamilyName, dto.TypeName);
                    if (symbolCache.TryGetValue(key, out var symbol) && !symbol.IsActive)
                    {
                        symbol.Activate();
                        anyActivated = true;
                    }
                }
                tx.Commit();
            }

            if (anyActivated)
                _doc.Regenerate();
        }

        private int CreatePipes(List<PipeDto> pipes,
            Dictionary<string, PipeType> pipeTypeMap,
            Dictionary<string, Level> levelMap,
            Dictionary<string, PipingSystemType> systemTypeMap,
            ImportResult result,
            Action<ProgressReport> onProgress, ref int current, int total,
            List<Pipe> createdPipes)
        {
            int count = 0;
            var defaultPipeType = pipeTypeMap.Values.FirstOrDefault();
            var defaultLevel    = levelMap.Values.FirstOrDefault();
            var defaultSystem   = systemTypeMap.Values.FirstOrDefault();

            foreach (var dto in pipes)
            {
                try
                {
                    var pipeType   = pipeTypeMap.TryGetValue(dto.PipeTypeName, out var pt) ? pt : defaultPipeType;
                    var level      = levelMap.TryGetValue(dto.LevelName, out var lv) ? lv : defaultLevel;
                    var systemType = systemTypeMap.TryGetValue(dto.SystemType, out var st) ? st : defaultSystem;

                    if (pipeType == null)
                    {
                        result.FailedElements++;
                        result.ErrorLog.Add($"[Pipe {dto.RevitId}] PipeType '{dto.PipeTypeName}' không có trong file đích");
                    }
                    else if (level == null)
                    {
                        result.FailedElements++;
                        result.ErrorLog.Add($"[Pipe {dto.RevitId}] Level '{dto.LevelName}' không có trong file đích");
                    }
                    else
                    {
                        var start = new XYZ(dto.StartX, dto.StartY, dto.StartZ);
                        var end   = new XYZ(dto.EndX, dto.EndY, dto.EndZ);

                        if (start.DistanceTo(end) < 0.001)
                        {
                            result.FailedElements++;
                            result.ErrorLog.Add($"[Pipe {dto.RevitId}] Chiều dài < 0.001 ft, bỏ qua");
                        }
                        else
                        {
                            var pipe = Pipe.Create(_doc,
                                systemType?.Id ?? ElementId.InvalidElementId,
                                pipeType.Id, level.Id, start, end);

                            if (pipe != null)
                            {
                                pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(dto.Diameter);
                                createdPipes.Add(pipe);
                                count++;
                            }
                            else
                            {
                                result.FailedElements++;
                                result.ErrorLog.Add($"[Pipe {dto.RevitId}] Pipe.Create() trả về null");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.FailedElements++;
                    result.ErrorLog.Add($"[Pipe {dto.RevitId}] {ex.GetType().Name}: {ex.Message}");
                }
                Report(onProgress, ++current, total, $"Tạo ống {count}/{pipes.Count}");
            }
            return count;
        }

        private int CreateFittings(List<PipeFittingDto> fittings,
            Dictionary<string, Level> levelMap,
            Dictionary<(string, string), FamilySymbol> symbolCache,
            ImportResult result,
            Action<ProgressReport> onProgress, ref int current, int total,
            List<FamilyInstance> createdFittings)
        {
            int count = 0;
            var defaultLevel = levelMap.Values.FirstOrDefault();

            foreach (var dto in fittings)
            {
                try
                {
                    if (!symbolCache.TryGetValue((dto.FamilyName, dto.TypeName), out var symbol))
                    {
                        result.FailedElements++;
                        result.MissingFamilies.Add($"{dto.FamilyName} : {dto.TypeName}");
                        result.ErrorLog.Add($"[Fitting {dto.RevitId}] Family không có trong file đích: '{dto.FamilyName}' / '{dto.TypeName}'");
                    }
                    else
                    {
                        var level = levelMap.TryGetValue(dto.LevelName, out var lv) ? lv : defaultLevel;
                        if (level == null)
                        {
                            result.FailedElements++;
                            result.ErrorLog.Add($"[Fitting {dto.RevitId}] Level '{dto.LevelName}' không có trong file đích");
                        }
                        else
                        {
                            var location = new XYZ(dto.LocationX, dto.LocationY, dto.LocationZ);
                            var instance = _doc.Create.NewFamilyInstance(
                                location, symbol, level,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                            if (instance != null)
                            {
                                ApplyRotation(instance, dto.RotationAngle);
                                createdFittings.Add(instance);
                                count++;
                            }
                            else
                            {
                                result.FailedElements++;
                                result.ErrorLog.Add($"[Fitting {dto.RevitId}] NewFamilyInstance() trả về null: '{dto.FamilyName}' / '{dto.TypeName}'");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.FailedElements++;
                    result.ErrorLog.Add($"[Fitting {dto.RevitId}] {dto.FamilyName}/{dto.TypeName} — {ex.GetType().Name}: {ex.Message}");
                }
                Report(onProgress, ++current, total, $"Tạo fitting {count}/{fittings.Count}");
            }
            return count;
        }

        private static void Report(Action<ProgressReport> onProgress, int current, int total, string message)
            => onProgress?.Invoke(new ProgressReport { Current = current, Total = total, Message = message });

        private int ConnectAllElements(List<Pipe> pipes, List<FamilyInstance> fittings, List<string> errorLog)
        {
            var freeConns = new Dictionary<string, Connector>();
            int joined = 0;

            foreach (var pipe in pipes)
                foreach (Connector c in pipe.ConnectorManager.Connectors)
                    if (c.Domain == Domain.DomainPiping)
                        TryJoin(freeConns, c, ref joined, errorLog);

            foreach (var fitting in fittings)
            {
                var cm = fitting.MEPModel?.ConnectorManager;
                if (cm == null) continue;
                foreach (Connector c in cm.Connectors)
                    if (c.Domain == Domain.DomainPiping)
                        TryJoin(freeConns, c, ref joined, errorLog);
            }

            return joined;
        }

        private static void TryJoin(Dictionary<string, Connector> map, Connector c, ref int joined, List<string> errorLog)
        {
            var key = ConnKey(c.Origin);
            if (map.TryGetValue(key, out var other))
            {
                try
                {
                    if (!c.IsConnected && !other.IsConnected)
                    {
                        c.ConnectTo(other);
                        joined++;
                    }
                    else
                    {
                        errorLog.Add($"[Connect] Bỏ qua tại {key} — c.IsConnected={c.IsConnected}, other.IsConnected={other.IsConnected}");
                    }
                }
                catch (Exception ex)
                {
                    errorLog.Add($"[Connect] Lỗi tại {key} — {ex.GetType().Name}: {ex.Message}");
                }
                map.Remove(key);
            }
            else
            {
                map[key] = c;
            }
        }

        private static string ConnKey(XYZ pt)
            => $"{Math.Round(pt.X, 3)},{Math.Round(pt.Y, 3)},{Math.Round(pt.Z, 3)}";

        private void ApplyRotation(FamilyInstance instance, double angle)
        {
            if (Math.Abs(angle) < 0.001) return;
            var lp = instance.Location as LocationPoint;
            if (lp == null) return;

            var axis = Line.CreateBound(lp.Point, lp.Point + XYZ.BasisZ);
            lp.Rotate(axis, angle - lp.Rotation);
        }

        private static void ApplyFastFailureHandling(Transaction tx)
        {
            var options = tx.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new SilentFailurePreprocessor());
            options.SetClearAfterRollback(true);
            tx.SetFailureHandlingOptions(options);
        }

        private Dictionary<(string, string), FamilySymbol> BuildSymbolCache()
        {
            var cache = new Dictionary<(string, string), FamilySymbol>();
            var symbols = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>();

            foreach (var s in symbols)
            {
                var key = (s.FamilyName, s.Name);
                if (!cache.ContainsKey(key))
                    cache[key] = s;
            }
            return cache;
        }

        private Dictionary<string, PipeType> BuildPipeTypeMap()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(PipeType))
                .Cast<PipeType>()
                .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, Level> BuildLevelMap()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToDictionary(l => l.Name, l => l, StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, PipingSystemType> BuildPipeSystemTypeMap()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(PipingSystemType))
                .Cast<PipingSystemType>()
                .ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
        }
    }

    internal class SilentFailurePreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            foreach (var failure in failuresAccessor.GetFailureMessages())
            {
                if (failure.GetSeverity() == FailureSeverity.Warning)
                    failuresAccessor.DeleteWarning(failure);
            }
            return FailureProcessingResult.Continue;
        }
    }
}
