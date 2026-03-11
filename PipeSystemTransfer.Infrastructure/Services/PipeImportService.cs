using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using PipeSystemTransfer.Core.Interfaces;
using PipeSystemTransfer.Core.Models;
using PipeSystemTransfer.Infrastructure.Helpers;

namespace PipeSystemTransfer.Infrastructure.Services
{
    public class PipeImportService : IImportService
    {
        private const double MinPipeLengthFt = 0.01;

        private readonly Document _doc;

        public PipeImportService(Document doc) => _doc = doc;

        public ImportResult ImportPipeSystem(
            PipeSystemDto pipeSystem,
            Action<ProgressReport> onProgress = null)
        {
            var result  = new ImportResult();
            int total   = pipeSystem.Pipes.Count + pipeSystem.Fittings.Count;
            int current = 0;

            var createdPipes    = new List<Pipe>();
            var pipeIdMap       = new Dictionary<string, Pipe>();
            var createdFittings = new List<FamilyInstance>();
            var fittingIdMap    = new Dictionary<string, FamilyInstance>();

            try
            {
                ProgressReport.Report(onProgress, 0, total, "Kích hoạt family...");
                ActivateRequiredSymbols(pipeSystem);
                ProgressReport.Report(onProgress, 0, total, "Bắt đầu tạo phần tử...");

                using (var tx = new Transaction(_doc, "Import Pipe System"))
                {
                    ApplyFastFailureHandling(tx);
                    tx.Start();
                    try
                    {
                        var cache         = new RevitElementCache(_doc);
                        var pipeTypeMap   = cache.BuildPipeTypeMap();
                        var levelMap      = cache.BuildLevelMap();
                        var systemTypeMap = cache.BuildPipeSystemTypeMap();
                        var symbolCache   = cache.BuildSymbolCache();
                        var fittingCache  = cache.BuildFittingSymbolCache();

                        result.CreatedFittings = CreateFittings(
                            pipeSystem.Fittings, levelMap, symbolCache, fittingCache,
                            result, onProgress, ref current, total, createdFittings, fittingIdMap);

                        var snapMap = BuildFittingConnectorSnapMap(pipeSystem.Fittings, fittingIdMap);

                        result.CreatedPipes = CreatePipes(
                            pipeSystem.Pipes, pipeTypeMap, levelMap, systemTypeMap,
                            snapMap, result, onProgress, ref current, total, createdPipes, pipeIdMap);

                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        result.Success      = false;
                        result.ErrorMessage = ex.Message;
                        return result;
                    }
                }

                result.Success = true;
                ProgressReport.Report(onProgress, total, total, "Hoàn tất import!");
            }
            catch (Exception ex)
            {
                result.Success      = false;
                result.ErrorMessage = ex.Message;
            }
            return result;
        }

        private void ActivateRequiredSymbols(PipeSystemDto pipeSystem)
        {
            var symbolCache   = new RevitElementCache(_doc).BuildSymbolCache();
            bool anyActivated = false;

            using (var tx = new Transaction(_doc, "Activate Family Symbols"))
            {
                tx.Start();
                foreach (var dto in pipeSystem.Fittings)
                {
                    var key = (dto.FamilyName, dto.TypeName);
                    if (symbolCache.TryGetValue(key, out var symbol) && !symbol.IsActive)
                    { symbol.Activate(); anyActivated = true; }
                }
                tx.Commit();
            }

            if (anyActivated)
                _doc.Regenerate();
        }

        private int CreatePipes(
            List<PipeDto> pipes,
            Dictionary<string, PipeType> pipeTypeMap,
            Dictionary<string, Level> levelMap,
            Dictionary<string, PipingSystemType> systemTypeMap,
            Dictionary<string, List<(XYZ jsonPos, XYZ actualPos)>> snapMap,
            ImportResult result,
            Action<ProgressReport> onProgress, ref int current, int total,
            List<Pipe> createdPipes, Dictionary<string, Pipe> idMap)
        {
            int count           = 0;
            var defaultPipeType = pipeTypeMap.Values.FirstOrDefault();
            var defaultLevel    = levelMap.Values.FirstOrDefault();
            var defaultSystem   = systemTypeMap.Values.FirstOrDefault();

            foreach (var dto in pipes)
            {
                try
                {
                    var pipeType   = pipeTypeMap.TryGetValue(dto.PipeTypeName,     out var pt) ? pt : defaultPipeType;
                    var level      = levelMap.TryGetValue(dto.LevelName,           out var lv) ? lv : defaultLevel;
                    var systemType = systemTypeMap.TryGetValue(dto.SystemTypeName, out var st) ? st : defaultSystem;

                    if (pipeType == null)
                    {
                        result.FailedElements++;
                        result.ErrorLog.Add($"[Pipe {dto.Id}] PipeType '{dto.PipeTypeName}' không có trong file đích");
                        continue;
                    }
                    if (level == null)
                    {
                        result.FailedElements++;
                        result.ErrorLog.Add($"[Pipe {dto.Id}] Level '{dto.LevelName}' không có trong file đích");
                        continue;
                    }

                    var start = PipeElementParser.ParseXYZ(dto.StartPoint);
                    var end   = PipeElementParser.ParseXYZ(dto.EndPoint);

                    if (snapMap.TryGetValue(dto.Id, out var snaps))
                    {
                        foreach (var (jsonPos, actualPos) in snaps)
                        {
                            if (jsonPos.DistanceTo(start) <= jsonPos.DistanceTo(end)) start = actualPos;
                            else                                                       end   = actualPos;
                        }
                    }

                    if (start.DistanceTo(end) < MinPipeLengthFt) continue;

                    var pipe = Pipe.Create(_doc,
                        systemType?.Id ?? ElementId.InvalidElementId,
                        pipeType.Id, level.Id, start, end);

                    if (pipe != null)
                    {
                        pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(dto.Diameter);
                        createdPipes.Add(pipe);
                        idMap[dto.Id] = pipe;
                        count++;
                    }
                    else
                    {
                        result.FailedElements++;
                        result.ErrorLog.Add($"[Pipe {dto.Id}] Pipe.Create() trả về null");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedElements++;
                    result.ErrorLog.Add($"[Pipe {dto.Id}] {ex.GetType().Name}: {ex.Message}");
                }
                ProgressReport.Report(onProgress, ++current, total, $"Tạo ống {count}/{pipes.Count}");
            }
            return count;
        }

        private Dictionary<string, List<(XYZ jsonPos, XYZ actualPos)>> BuildFittingConnectorSnapMap(
            List<PipeFittingDto> fittingDtos,
            Dictionary<string, FamilyInstance> fittingIdMap)
        {
            var map = new Dictionary<string, List<(XYZ, XYZ)>>();

            foreach (var fittingDto in fittingDtos)
            {
                if (!fittingIdMap.TryGetValue(fittingDto.Id, out var fi) || fi == null) continue;
                var cm = fi.MEPModel?.ConnectorManager;
                if (cm == null) continue;

                var actualById = new Dictionary<int, XYZ>();
                foreach (Connector c in cm.Connectors)
                    if (c.Domain == Domain.DomainPiping)
                        actualById[c.Id] = c.Origin;

                foreach (var connDto in fittingDto.Connectors)
                {
                    if (string.IsNullOrEmpty(connDto.ConnectedToId)) continue;
                    if (!actualById.TryGetValue(connDto.ConnectorId, out var actualPos)) continue;

                    var pipeId = connDto.ConnectedToId;
                    if (!map.ContainsKey(pipeId))
                        map[pipeId] = new List<(XYZ, XYZ)>();
                    map[pipeId].Add((PipeElementParser.ParseXYZ(connDto.Origin), actualPos));
                }
            }

            return map;
        }

        private int CreateFittings(
            List<PipeFittingDto> fittings,
            Dictionary<string, Level> levelMap,
            Dictionary<(string, string), FamilySymbol> symbolCache,
            Dictionary<(string, string), FamilySymbol> fittingCache,
            ImportResult result,
            Action<ProgressReport> onProgress, ref int current, int total,
            List<FamilyInstance> createdFittings,
            Dictionary<string, FamilyInstance> idMap)
        {
            int count        = 0;
            var defaultLevel = levelMap.Values.FirstOrDefault();

            foreach (var dto in fittings)
            {
                try
                {
                    var key = (dto.FamilyName, dto.TypeName);
                    if (!symbolCache.TryGetValue(key, out var symbol))
                        symbol = FamilySymbolMatcher.FindBestMatch(fittingCache, dto.FamilyName, dto.TypeName);

                    if (symbol != null && !symbol.IsActive)
                        symbol.Activate();

                    if (symbol == null)
                    {
                        result.FailedElements++;
                        result.MissingFamilies.Add($"{dto.FamilyName} : {dto.TypeName}");
                        result.ErrorLog.Add($"[Fitting {dto.Id}] Family không có trong file đích: '{dto.FamilyName}' / '{dto.TypeName}'");
                    }
                    else
                    {
                        var level = levelMap.TryGetValue(dto.LevelName, out var lv) ? lv : defaultLevel;
                        if (level == null)
                        {
                            result.FailedElements++;
                            result.ErrorLog.Add($"[Fitting {dto.Id}] Level '{dto.LevelName}' không có trong file đích");
                        }
                        else
                        {
                            var location = PipeElementParser.ParseXYZ(dto.LocationPoint);
                            var instance = _doc.Create.NewFamilyInstance(
                                location, symbol, level,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                            if (instance != null)
                            {
                                createdFittings.Add(instance);
                                idMap[dto.Id] = instance;
                                count++;
                                FittingTransformHelper.ApplyTransformAndFlips(_doc, instance, dto, result.ErrorLog);
                            }
                            else
                            {
                                result.FailedElements++;
                                result.ErrorLog.Add($"[Fitting {dto.Id}] NewFamilyInstance() trả về null: '{dto.FamilyName}' / '{dto.TypeName}'");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.FailedElements++;
                    result.ErrorLog.Add($"[Fitting {dto.Id}] {dto.FamilyName}/{dto.TypeName} — {ex.GetType().Name}: {ex.Message}");
                }
                ProgressReport.Report(onProgress, ++current, total, $"Tạo fitting {count}/{fittings.Count}");
            }
            return count;
        }

        private int ConnectAllElements(
            List<PipeDto> pipeDtos, Dictionary<string, Pipe> pipeMap,
            List<PipeFittingDto> fittingDtos, Dictionary<string, FamilyInstance> fittingMap,
            List<string> errorLog)
        {
            var idToElement = new Dictionary<string, Element>();
            foreach (var kv in pipeMap)
                if (kv.Value != null && kv.Value.IsValidObject) idToElement[kv.Key] = kv.Value;
            foreach (var kv in fittingMap)
                if (kv.Value != null && kv.Value.IsValidObject) idToElement[kv.Key] = kv.Value;

            var processed = new HashSet<string>();
            int joined    = 0;

            foreach (var dto in pipeDtos)
            {
                if (!pipeMap.TryGetValue(dto.Id, out var pipe) || pipe == null || !pipe.IsValidObject) continue;
                var cm = pipe.ConnectorManager;
                if (cm == null) continue;
                foreach (var connDto in dto.Connectors)
                    ConnectorUtils.TryConnect(dto.Id, cm, connDto, idToElement, processed, ref joined, errorLog);
            }

            foreach (var dto in fittingDtos)
            {
                if (!fittingMap.TryGetValue(dto.Id, out var inst) || inst == null || !inst.IsValidObject) continue;
                var cm = inst.MEPModel?.ConnectorManager;
                if (cm == null) continue;
                foreach (var connDto in dto.Connectors)
                    ConnectorUtils.TryConnect(dto.Id, cm, connDto, idToElement, processed, ref joined, errorLog);
            }

            return joined;
        }

        private static void ApplyFastFailureHandling(Transaction tx)
        {
            var options = tx.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new SilentFailurePreprocessor());
            options.SetClearAfterRollback(true);
            tx.SetFailureHandlingOptions(options);
        }

        private static int CountConnectedPairs(List<Pipe> pipes, List<FamilyInstance> fittings)
        {
            var seen  = new HashSet<string>();
            int count = 0;

            void CountFromManager(ConnectorManager cm, int ownerId)
            {
                if (cm == null) return;
                foreach (Connector c in cm.Connectors)
                {
                    if (c.Domain != Domain.DomainPiping || !c.IsConnected) continue;
                    foreach (Connector r in c.AllRefs)
                    {
                        if (r.Domain != Domain.DomainPiping) continue;
                        int otherId = r.Owner.Id.IntegerValue;
                        int a = Math.Min(ownerId, otherId), b = Math.Max(ownerId, otherId);
                        if (seen.Add($"{a}:{b}")) count++;
                    }
                }
            }

            foreach (var pipe in pipes)
                if (pipe != null && pipe.IsValidObject)
                    CountFromManager(pipe.ConnectorManager, pipe.Id.IntegerValue);

            foreach (var fi in fittings)
                if (fi != null && fi.IsValidObject)
                    CountFromManager(fi.MEPModel?.ConnectorManager, fi.Id.IntegerValue);

            return count;
        }
    }
}
