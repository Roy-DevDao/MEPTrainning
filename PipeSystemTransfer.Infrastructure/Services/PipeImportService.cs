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

        public ImportResult ImportPipeSystem(
            PipeSystemDto pipeSystem,
            Action<ProgressReport> onProgress = null)
        {
            var result = new ImportResult();
            int total = pipeSystem.Pipes.Count + pipeSystem.Fittings.Count;
            int current = 0;

            var createdPipes    = new List<Pipe>();
            var pipeIdMap       = new Dictionary<string, Pipe>();
            var createdFittings = new List<FamilyInstance>();
            var fittingIdMap    = new Dictionary<string, FamilyInstance>();

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
                        var pipeTypeMap  = BuildPipeTypeMap();
                        var levelMap     = BuildLevelMap();
                        var systemTypeMap = BuildPipeSystemTypeMap();
                        var symbolCache  = BuildSymbolCache();
                        var fittingCache = BuildFittingSymbolCache();

                        result.CreatedFittings = CreateFittings(
                            pipeSystem.Fittings, levelMap, symbolCache, fittingCache, result,
                            onProgress, ref current, total, createdFittings, fittingIdMap);

                        result.CreatedPipes = CreatePipes(
                            pipeSystem.Pipes, pipeTypeMap, levelMap, systemTypeMap,
                            result, onProgress, ref current, total, createdPipes, pipeIdMap);

                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.RollBack();
                        result.Success = false;
                        result.ErrorMessage = ex.Message;
                        return result;
                    }
                }

                //Report(onProgress, total, total, "Đang kết nối phần tử...");
                //using (var connectTx = new Transaction(_doc, "Connect MEP Elements"))
                //{
                //    ApplyFastFailureHandling(connectTx);
                //    connectTx.Start();
                //    try
                //    {
                //        ConnectAllElements(
                //            pipeSystem.Pipes, pipeIdMap,
                //            pipeSystem.Fittings, fittingIdMap,
                //            result.ErrorLog);
                //        connectTx.Commit();
                //    }
                //    catch (Exception ex)
                //    {
                //        connectTx.RollBack();
                //        result.ErrorLog.Add($"[Connect Tx] {ex.Message}");
                //    }
                //}

                result.Success = true;
                //result.JoinedConnectors = CountConnectedPairs(createdPipes, createdFittings);
                Report(onProgress, total, total, "Hoàn tất import!");
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
                foreach (var dto in pipeSystem.Fittings)
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
            List<Pipe> createdPipes,
            Dictionary<string, Pipe> idMap)
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

                    var start = ParseXYZ(dto.StartPoint);
                    var end   = ParseXYZ(dto.EndPoint);

                    if (start.DistanceTo(end) < 0.001)
                    {
                        result.FailedElements++;
                        result.ErrorLog.Add($"[Pipe {dto.Id}] Chiều dài < 0.001 ft, bỏ qua");
                        continue;
                    }

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
                Report(onProgress, ++current, total, $"Tạo ống {count}/{pipes.Count}");
            }
            return count;
        }

        private int CreateFittings(List<PipeFittingDto> fittings,
            Dictionary<string, Level> levelMap,
            Dictionary<(string, string), FamilySymbol> symbolCache,
            Dictionary<(string, string), FamilySymbol> fittingCache,
            ImportResult result,
            Action<ProgressReport> onProgress, ref int current, int total,
            List<FamilyInstance> createdFittings,
            Dictionary<string, FamilyInstance> idMap)
        {
            int count = 0;
            var defaultLevel = levelMap.Values.FirstOrDefault();

            foreach (var dto in fittings)
            {
                try
                {
                    var key = (dto.FamilyName, dto.TypeName);
                    if (!symbolCache.TryGetValue(key, out var symbol))
                    {
                        symbol = FindBestMatch(fittingCache, dto.FamilyName, dto.TypeName);
                    }

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
                            var location = ParseXYZ(dto.LocationPoint);
                            var instance = _doc.Create.NewFamilyInstance(
                                location, symbol, level,
                                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                            if (instance != null)
                            {
                                createdFittings.Add(instance);
                                idMap[dto.Id] = instance;
                                count++;
                                ApplyTransformAndFlips(instance, dto);
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
                Report(onProgress, ++current, total, $"Tạo fitting {count}/{fittings.Count}");
            }
            return count;
        }

        private static void Report(Action<ProgressReport> onProgress, int current, int total, string message)
            => onProgress?.Invoke(new ProgressReport { Current = current, Total = total, Message = message });

        private int ConnectAllElements(
            List<PipeDto> pipeDtos, Dictionary<string, Pipe> pipeMap,
            List<PipeFittingDto> fittingDtos, Dictionary<string, FamilyInstance> fittingMap,
            List<string> errorLog)
        {
            var idToElement = new Dictionary<string, Element>();
            foreach (var kv in pipeMap)
            {
                var el = kv.Value;
                if (el != null && el.IsValidObject)
                    idToElement[kv.Key] = el;
            }
            foreach (var kv in fittingMap)
            {
                var el = kv.Value;
                if (el != null && el.IsValidObject)
                    idToElement[kv.Key] = el;
            }

            var processed = new HashSet<string>();
            int joined = 0;

            foreach (var dto in pipeDtos)
            {
                if (!pipeMap.TryGetValue(dto.Id, out var pipe) ||
                    pipe == null || !pipe.IsValidObject)
                    continue;

                var cm = pipe.ConnectorManager;
                if (cm == null) continue;

                foreach (var connDto in dto.Connectors)
                    TryConnectByIds(dto.Id, cm, connDto,
                                    idToElement, processed, ref joined, errorLog);
            }

            foreach (var dto in fittingDtos)
            {
                if (!fittingMap.TryGetValue(dto.Id, out var inst) ||
                    inst == null || !inst.IsValidObject)
                    continue;

                var cm = inst.MEPModel?.ConnectorManager;
                if (cm == null) continue;

                foreach (var connDto in dto.Connectors)
                    TryConnectByIds(dto.Id, cm, connDto,
                                    idToElement, processed, ref joined, errorLog);
            }

            return joined;
        }

        private static void TryConnectByIds(
            string ownerId, ConnectorManager cmA, ConnectorDto connDto,
            Dictionary<string, Element> idToElement,
            HashSet<string> processed, ref int joined, List<string> errorLog)
        {
            if (string.IsNullOrEmpty(connDto.ConnectedToId)) return;

            var pairKey = string.Compare(ownerId, connDto.ConnectedToId, StringComparison.Ordinal) < 0
                ? $"{ownerId}:{connDto.ConnectedToId}"
                : $"{connDto.ConnectedToId}:{ownerId}";
            if (!processed.Add(pairKey)) return;

            if (!idToElement.TryGetValue(connDto.ConnectedToId, out var otherElement)) return;
            var cmB = GetConnectorManager(otherElement);
            if (cmB == null) return;

            var connA = FindConnectorById(cmA, connDto.ConnectorId)
                     ?? FindFreeConnector(cmA);
            if (connA == null) return;

            if (connA.IsConnected)
            {
                foreach (Connector r in connA.AllRefs)
                    if (r.Owner.Id == otherElement.Id) return;
                return; 
            }

            var connB = FindClosestFreeConnector(cmB, connA.Origin);
            if (connB == null || connB.IsConnected) return;

            try
            {
                connA.ConnectTo(connB);
                joined++;
            }
            catch (Exception ex)
            {
                errorLog.Add($"[Connect] {ownerId}↔{connDto.ConnectedToId} — {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static ConnectorManager GetConnectorManager(Element el)
        {
            if (el is Pipe pipe) return pipe.ConnectorManager;
            if (el is FamilyInstance fi) return fi.MEPModel?.ConnectorManager;
            return null;
        }

        private static Connector FindConnectorById(ConnectorManager cm, int id)
        {
            foreach (Connector c in cm.Connectors)
                if (c.Domain == Domain.DomainPiping && c.Id == id)
                    return c;
            return null;
        }

        private static Connector FindFreeConnector(ConnectorManager cm)
        {
            foreach (Connector c in cm.Connectors)
                if (c.Domain == Domain.DomainPiping && !c.IsConnected)
                    return c;
            return null;
        }

        private static Connector FindClosestFreeConnector(ConnectorManager cm, XYZ origin)
        {
            Connector best = null;
            double bestDist = double.MaxValue;
            foreach (Connector c in cm.Connectors)
            {
                if (c.Domain != Domain.DomainPiping || c.IsConnected) continue;
                double dist = c.Origin.DistanceTo(origin);
                if (dist < bestDist) { bestDist = dist; best = c; }
            }
            return best;
        }

        private void ApplyTransformAndFlips(FamilyInstance instance, PipeFittingDto dto)
        {
            if (dto.Transform == null) return;

            try
            {
                var center   = ParseXYZ(dto.LocationPoint);
                var T_target = ParseTransformDto(dto.Transform);

              
                double det = T_target.BasisX.DotProduct(T_target.BasisY.CrossProduct(T_target.BasisZ));
                bool needFacingFlip = false;
                if (det < 0)
                {
                    T_target.BasisX = -T_target.BasisX;
                    needFacingFlip = true;
                }

                if (TryExtractRotation(T_target, out var axis, out var angle) && Math.Abs(angle) > 1e-6)
                {
                    var rotAxis = Line.CreateBound(center, center + axis);
                    ElementTransformUtils.RotateElement(_doc, instance.Id, rotAxis, angle);
                }

                if (needFacingFlip)
                {
                    try { instance.flipFacing(); } catch { }
                }
            }
            catch { }
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

        private Dictionary<(string, string), FamilySymbol> BuildFittingSymbolCache()
        {
            var cache = new Dictionary<(string, string), FamilySymbol>();
            var symbols = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_PipeFitting)
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

        private static readonly char[] _splitChars = { ' ', '-', '_', '/', '°', '(', ')' };

        private static readonly HashSet<string> _typeKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tee", "elbow", "union", "transition", "cap", "wye", "flange", "trap",
            "siphon", "coupling", "joint", "reducer", "bend", "cross", "plug",
            "socket", "sweep", "swept", "lateral", "saddle", "valve", "adapter",
            "inspection", "cleanout", "strainer", "nipple", "bushing"
        };

        private static XYZ ParseXYZ(Point3D pt)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return new XYZ(
                double.Parse(pt.X, inv),
                double.Parse(pt.Y, inv),
                double.Parse(pt.Z, inv));
        }

        private static Transform ParseTransformDto(TransformDto t)
        {
            var tr = Transform.Identity;
            tr.BasisX = ParseXYZ(t.BasisX);
            tr.BasisY = ParseXYZ(t.BasisY);
            tr.BasisZ = ParseXYZ(t.BasisZ);
            tr.Origin  = ParseXYZ(t.Origin);
            return tr;
        }
        private static bool TryExtractRotation(Transform deltaT, out XYZ axis, out double angle)
        {
            double trace    = deltaT.BasisX.X + deltaT.BasisY.Y + deltaT.BasisZ.Z;
            double cosAngle = Math.Max(-1.0, Math.Min(1.0, (trace - 1.0) / 2.0));
            angle = Math.Acos(cosAngle);

            if (angle < 1e-6)
            {
                axis = XYZ.BasisZ;
                return false;
            }

            double sinA = Math.Sin(angle);

            if (Math.Abs(sinA) < 1e-6)
            {
                double xx = (deltaT.BasisX.X + 1.0) / 2.0;
                double yy = (deltaT.BasisY.Y + 1.0) / 2.0;
                double zz = (deltaT.BasisZ.Z + 1.0) / 2.0;

                if (xx >= yy && xx >= zz)
                {
                    double nx = Math.Sqrt(Math.Max(0.0, xx));
                    double ny = nx > 1e-10 ? deltaT.BasisX.Y / (2.0 * nx) : 0.0;
                    double nz = nx > 1e-10 ? deltaT.BasisX.Z / (2.0 * nx) : 0.0;
                    axis = new XYZ(nx, ny, nz);
                }
                else if (yy >= zz)
                {
                    double ny = Math.Sqrt(Math.Max(0.0, yy));
                    double nx = ny > 1e-10 ? deltaT.BasisY.X / (2.0 * ny) : 0.0;
                    double nz = ny > 1e-10 ? deltaT.BasisY.Z / (2.0 * ny) : 0.0;
                    axis = new XYZ(nx, ny, nz);
                }
                else
                {
                    double nz = Math.Sqrt(Math.Max(0.0, zz));
                    double nx = nz > 1e-10 ? deltaT.BasisZ.X / (2.0 * nz) : 0.0;
                    double ny = nz > 1e-10 ? deltaT.BasisZ.Y / (2.0 * nz) : 0.0;
                    axis = new XYZ(nx, ny, nz);
                }

                if (axis.GetLength() < 1e-10) { axis = XYZ.BasisZ; }
                else axis = axis.Normalize();
                return true;
            }

            double ax = (deltaT.BasisY.Z - deltaT.BasisZ.Y) / (2.0 * sinA);
            double ay = (deltaT.BasisZ.X - deltaT.BasisX.Z) / (2.0 * sinA);
            double az = (deltaT.BasisX.Y - deltaT.BasisY.X) / (2.0 * sinA);
            axis = new XYZ(ax, ay, az);

            if (axis.GetLength() < 1e-10) { axis = XYZ.BasisZ; return false; }
            axis = axis.Normalize();
            return true;
        }

        private static readonly Dictionary<string, string> _abbrevMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "flg",  "flange"  }, { "elb", "elbow"    }, { "ell",  "elbow"    },
            { "red",  "reducer" }, { "thr", "thread"   }, { "thrd", "thread"   },
            { "cplg", "coupling"}, { "str", "strainer" }, { "adpt", "adapter"  },
            { "trans","transition"},{ "unin","union"    }, { "flex", "flexible" }
        };

        private enum GenericShape
        {
            Unknown,
            Elbow,
            Tee,
            Cross,
            Transition,
            Cap,
            Coupling
        }

        private static GenericShape DetectGenericShape(string familyName, string typeName)
        {
            var name = $"{familyName} {typeName}".ToLowerInvariant();

            if (name.Contains("tee")) return GenericShape.Tee;
            if (name.Contains("elbow") || name.Contains("bend")) return GenericShape.Elbow;
            if (name.Contains("cross")) return GenericShape.Cross;
            if (name.Contains("transition") || name.Contains("trans")) return GenericShape.Transition;
            if (name.Contains("cap")) return GenericShape.Cap;
            if (name.Contains("coupling") || name.Contains("cplg")) return GenericShape.Coupling;

            return GenericShape.Unknown;
        }

        private static FamilySymbol TryMapToProvidedGenericFamily(
            Dictionary<(string, string), FamilySymbol> cache,
            GenericShape shape)
        {
            if (shape == GenericShape.Unknown || cache.Count == 0)
                return null;

            string targetKeyword = null;

            switch (shape)
            {
                case GenericShape.Elbow:
                    targetKeyword = "elbow_generic";
                    break;
                case GenericShape.Tee:
                    targetKeyword = "tee_generic";
                    break;
                case GenericShape.Cross:
                    targetKeyword = "cross_generic";
                    break;
                case GenericShape.Transition:
                    targetKeyword = "transition_generic";
                    break;
                case GenericShape.Cap:
                    targetKeyword = "cap_generic";
                    break;
                case GenericShape.Coupling:
                    targetKeyword = "coupling_generic";
                    break;
                default:
                    targetKeyword = null;
                    break;
            }

            if (targetKeyword == null) return null;

            FamilySymbol best = null;

            foreach (var kvp in cache)
            {
                var famName = kvp.Key.Item1;
                if (famName.IndexOf(targetKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (kvp.Key.Item2.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                        return kvp.Value;

                    best = kvp.Value;
                }
            }

            return best;
        }

        private static FamilySymbol FindBestMatch(
            Dictionary<(string, string), FamilySymbol> cache,
            string familyName, string typeName)
        {
            if (cache.Count == 0) return null;

            var shape = DetectGenericShape(familyName, typeName);
            var mapped = TryMapToProvidedGenericFamily(cache, shape);
            if (mapped != null)
                return mapped;

            var rawWords = $"{familyName} {typeName}"
                .ToLowerInvariant()
                .Split(_splitChars, StringSplitOptions.RemoveEmptyEntries);
            var queryWords = new HashSet<string>();
            foreach (var w in rawWords)
            {
                queryWords.Add(w);
                if (_abbrevMap.TryGetValue(w, out var expanded))
                    queryWords.Add(expanded);
            }

            FamilySymbol best = null;
            int bestScore = 0;

            foreach (var kvp in cache)
            {
                var candidate = $"{kvp.Key.Item1} {kvp.Key.Item2}".ToLowerInvariant();
                int score = 0;
                foreach (var word in queryWords)
                {
                    if (candidate.Contains(word))
                        score += _typeKeywords.Contains(word) ? 10 : 1;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = kvp.Value;
                }
            }

            return best ?? cache.Values.FirstOrDefault();
        }


        private static int CountConnectedPairs(List<Pipe> pipes, List<FamilyInstance> fittings)
        {
            var seen = new HashSet<string>();
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
                        int a = Math.Min(ownerId, otherId);
                        int b = Math.Max(ownerId, otherId);
                        if (seen.Add($"{a}:{b}")) count++;
                    }
                }
            }

            foreach (var pipe in pipes)
            {
                if (pipe == null || !pipe.IsValidObject) continue;
                CountFromManager(pipe.ConnectorManager, pipe.Id.IntegerValue);
            }

            foreach (var fi in fittings)
            {
                if (fi == null || !fi.IsValidObject) continue;
                CountFromManager(fi.MEPModel?.ConnectorManager, fi.Id.IntegerValue);
            }

            return count;
        }


        internal class SilentFailurePreprocessor : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                foreach (var failure in failuresAccessor.GetFailureMessages())
                {
                    var severity = failure.GetSeverity();

                    if (severity == FailureSeverity.Warning)
                    {
                        failuresAccessor.DeleteWarning(failure);
                        continue;
                    }

                    if (severity == FailureSeverity.Error)
                    {
                        if (failure.HasResolutionOfType(FailureResolutionType.DetachElements))
                        {
                            failure.SetCurrentResolutionType(FailureResolutionType.DetachElements);
                            failuresAccessor.ResolveFailure(failure);
                            continue;
                        }

                        if (failure.HasResolutionOfType(FailureResolutionType.SkipElements))
                        {
                            failure.SetCurrentResolutionType(FailureResolutionType.SkipElements);
                            failuresAccessor.ResolveFailure(failure);
                            continue;
                        }

                        if (failure.HasResolutionOfType(FailureResolutionType.DeleteElements))
                        {
                            failure.SetCurrentResolutionType(FailureResolutionType.DeleteElements);
                            failuresAccessor.ResolveFailure(failure);
                        }
                    }
                }

                return FailureProcessingResult.Continue;
            }
        }
    }
}


