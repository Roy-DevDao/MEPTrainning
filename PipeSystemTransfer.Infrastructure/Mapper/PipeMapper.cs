using System.Collections.Generic;
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
            var end   = curve?.GetEndPoint(1) ?? XYZ.Zero;

            return new PipeDto
            {
                Id             = pipe.Id.IntegerValue.ToString(),
                LevelName      = GetLevelName(pipe),
                SystemTypeName = pipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "",
                PipeTypeName   = pipe.PipeType?.Name ?? "",
                Diameter       = pipe.Diameter,
                StartPoint     = ToPoint3D(start),
                EndPoint       = ToPoint3D(end),
                Connectors     = MapPipeConnectors(pipe)
            };
        }

        public static PipeFittingDto ToFittingDto(FamilyInstance fitting)
        {
            var location    = (fitting.Location as LocationPoint)?.Point ?? XYZ.Zero;
            var t           = fitting.GetTransform();
            double angleDeg = 0;
            if (fitting.Location is LocationPoint lp)
                angleDeg = lp.Rotation * (180.0 / System.Math.PI);

            return new PipeFittingDto
            {
                Transform = new TransformDto
                {
                    Origin = ToPoint3D(t.Origin),
                    BasisX = ToPoint3D(t.BasisX),
                    BasisY = ToPoint3D(t.BasisY),
                    BasisZ = ToPoint3D(t.BasisZ)
                },
                Id            = fitting.Id.IntegerValue.ToString(),
                LevelName     = GetLevelName(fitting),
                FamilyName    = fitting.Symbol?.FamilyName ?? "",
                TypeName      = fitting.Symbol?.Name ?? "",
                LocationPoint = ToPoint3D(location),
                Angle         = angleDeg,
                Connectors    = MapFittingConnectors(fitting),
                Mirrored      = fitting.Mirrored,
                HandFlipped   = fitting.HandFlipped,
                FacingFlipped = fitting.FacingFlipped
            };
        }


        private static Point3D ToPoint3D(XYZ pt) =>
            new Point3D
            {
                X = pt.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                Y = pt.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                Z = pt.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            };

        private static List<ConnectorDto> MapPipeConnectors(Pipe pipe)
        {
            var list = new List<ConnectorDto>();
            foreach (Connector c in pipe.ConnectorManager.Connectors)
            {
                if (c.Domain != Domain.DomainPiping) continue;
                list.Add(new ConnectorDto
                {
                    ConnectorId   = c.Id,
                    Origin        = ToPoint3D(c.Origin),
                    Diameter      = c.Radius * 2,
                    ConnectedToId = GetConnectedToId(c, pipe.Id)
                });
            }
            return list;
        }

        private static List<ConnectorDto> MapFittingConnectors(FamilyInstance fitting)
        {
            var list       = new List<ConnectorDto>();
            var connectors = fitting.MEPModel?.ConnectorManager?.Connectors;
            if (connectors == null) return list;

            foreach (Connector c in connectors)
            {
                if (c.Domain != Domain.DomainPiping) continue;
                list.Add(new ConnectorDto
                {
                    ConnectorId   = c.Id,
                    Origin        = ToPoint3D(c.Origin),
                    Diameter      = c.Radius * 2,
                    ConnectedToId = GetConnectedToId(c, fitting.Id)
                });
            }
            return list;
        }

        private static string GetConnectedToId(Connector c, ElementId ownerId)
        {
            if (!c.IsConnected) return "";
            foreach (Connector other in c.AllRefs)
                if (other.Owner.Id != ownerId)
                    return other.Owner.Id.IntegerValue.ToString();
            return "";
        }

        private static string GetLevelName(Element element)
        {
            var levelId = element.LevelId;
            if (levelId == ElementId.InvalidElementId) return "";
            return (element.Document.GetElement(levelId) as Level)?.Name ?? "";
        }
    }
}
