using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using PipeSystemTransfer.Core.Models;

namespace PipeSystemTransfer.Infrastructure.Mapper
{
    public static class PipeMapper
    {
        public static PipeDto ToDto(Pipe pipe)
        {
            var curve = (pipe.Location as LocationCurve)?.Curve as Line;
            var start = curve?.GetEndPoint(0) ?? XYZ.Zero;
            var end = curve?.GetEndPoint(1) ?? XYZ.Zero;
            var mid = (start + end) / 2;

            return new PipeDto
            {
                RevitId = pipe.Id.IntegerValue,
                UniqueId = pipe.UniqueId,
                FamilyName = pipe.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "",
                TypeName = pipe.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)?.AsValueString() ?? "",
                PipeTypeName = pipe.PipeType?.Name ?? "",
                LocationX = mid.X,
                LocationY = mid.Y,
                LocationZ = mid.Z,
                LevelName = GetLevelName(pipe),
                SystemType = pipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "",
                StartX = start.X,
                StartY = start.Y,
                StartZ = start.Z,
                EndX = end.X,
                EndY = end.Y,
                EndZ = end.Z,
                Diameter = pipe.Diameter,
                Length = pipe.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0,
                InsulationTypeName = GetInsulationTypeName(pipe),
                InsulationThickness = GetInsulationThickness(pipe),
            };
        }

        public static PipeFittingDto ToFittingDto(FamilyInstance fitting)
        {
            var location = (fitting.Location as LocationPoint)?.Point ?? XYZ.Zero;
            var connectors = fitting.MEPModel?.ConnectorManager?.Connectors;

            return new PipeFittingDto
            {
                RevitId = fitting.Id.IntegerValue,
                UniqueId = fitting.UniqueId,
                FamilyName = fitting.Symbol?.FamilyName ?? "",
                TypeName = fitting.Symbol?.Name ?? "",
                LocationX = location.X,
                LocationY = location.Y,
                LocationZ = location.Z,
                LevelName = GetLevelName(fitting),
                SystemType = fitting.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "",
                RotationAngle = GetRotationAngle(fitting),
                HandFlipValue = fitting.HandFlipped ? 1 : 0,
                FacingFlipValue = fitting.FacingFlipped ? 1 : 0,
                Category = MapFittingCategory(fitting.Symbol?.FamilyName ?? ""),
                Connectors = connectors != null ? MapConnectors(connectors, fitting) : new System.Collections.Generic.List<ConnectorDto>()
            };
        }


        private static System.Collections.Generic.List<ConnectorDto> MapConnectors(ConnectorSet connectors, FamilyInstance element)
        {
            var list = new System.Collections.Generic.List<ConnectorDto>();
            foreach (Connector c in connectors)
            {
                if (c.Domain != Domain.DomainPiping) continue;
                var dto = new ConnectorDto
                {
                    X = c.Origin.X,
                    Y = c.Origin.Y,
                    Z = c.Origin.Z,
                    DirectionX = c.CoordinateSystem.BasisZ.X,
                    DirectionY = c.CoordinateSystem.BasisZ.Y,
                    DirectionZ = c.CoordinateSystem.BasisZ.Z,
                    Radius = c.Radius
                };
                if (c.IsConnected)
                {
                    foreach (Connector other in c.AllRefs)
                    {
                        if (other.Owner.Id != element.Id)
                        {
                            dto.ConnectedElementId = other.Owner.Id.IntegerValue;
                            break;
                        }
                    }
                }
                list.Add(dto);
            }
            return list;
        }

        private static string GetLevelName(Element element)
        {
            var levelId = element.LevelId;
            if (levelId == ElementId.InvalidElementId) return "";
            var level = element.Document.GetElement(levelId) as Level;
            return level?.Name ?? "";
        }

        private static string GetInsulationTypeName(Pipe pipe)
        {
            try
            {
                var insulations = InsulationLiningBase.GetInsulationIds(pipe.Document, pipe.Id);
                if (insulations == null || insulations.Count == 0) return "";
                var insulation = pipe.Document.GetElement(insulations.First()) as PipeInsulation;
                if (insulation == null) return "";
                return pipe.Document.GetElement(insulation.GetTypeId())?.Name ?? "";
            }
            catch { return ""; }
        }

        private static double GetInsulationThickness(Pipe pipe)
        {
            try
            {
                var insulations = InsulationLiningBase.GetInsulationIds(pipe.Document, pipe.Id);
                if (insulations == null || insulations.Count == 0) return 0;
                var insulation = pipe.Document.GetElement(insulations.First()) as PipeInsulation;
                return insulation?.Thickness ?? 0;
            }
            catch { return 0; }
        }

        private static double GetRotationAngle(FamilyInstance instance)
        {
            if (instance.Location is LocationPoint lp)
                return lp.Rotation;
            return 0;
        }

        private static FittingCategory MapFittingCategory(string familyName)
        {
            var name = familyName.ToLowerInvariant();
            if (name.Contains("elbow")) return FittingCategory.Elbow;
            if (name.Contains("tee")) return FittingCategory.Tee;
            if (name.Contains("cross")) return FittingCategory.Cross;
            if (name.Contains("transition") || name.Contains("reducer")) return FittingCategory.Transition;
            if (name.Contains("union")) return FittingCategory.Union;
            if (name.Contains("cap")) return FittingCategory.Cap;
            if (name.Contains("flange")) return FittingCategory.Flange;
            if (name.Contains("coupling")) return FittingCategory.Coupling;
            return FittingCategory.Other;
        }
    }
}
