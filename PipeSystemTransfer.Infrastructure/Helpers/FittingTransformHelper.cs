using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using PipeSystemTransfer.Core.Models;

namespace PipeSystemTransfer.Infrastructure.Helpers
{
    internal static class FittingTransformHelper
    {
        private const double Epsilon         = 1e-10;
        private const double RotationEpsilon = 1e-6;

        internal static void ApplyTransformAndFlips(
            Document doc, FamilyInstance instance, PipeFittingDto dto, List<string> errorLog)
        {
            if (dto.Transform == null) return;
            try
            {
                var center   = PipeElementParser.ParseXYZ(dto.LocationPoint);
                var T_target = PipeElementParser.ParseTransform(dto.Transform);

                var recomputedY = T_target.BasisZ.CrossProduct(T_target.BasisX);
                if (recomputedY.GetLength() > Epsilon)
                    T_target.BasisY = recomputedY.Normalize();

                if (dto.FacingFlipped || dto.Mirrored)
                {
                    try { instance.flipFacing(); }
                    catch (Exception ex)
                    { errorLog?.Add($"[Fitting {dto.Id}] flipFacing: {ex.GetType().Name}: {ex.Message}"); }
                }

                var T_current = instance.GetTransform();
                var currentY  = T_current.BasisZ.CrossProduct(T_current.BasisX);
                if (currentY.GetLength() > Epsilon)
                    T_current.BasisY = currentY.Normalize();

                var R_delta = ComputeRotationDelta(T_current, T_target);
                if (TryExtractRotation(R_delta, out var axis, out var angle) && Math.Abs(angle) > RotationEpsilon)
                {
                    var rotAxis = Line.CreateBound(center, center + axis);
                    ElementTransformUtils.RotateElement(doc, instance.Id, rotAxis, angle);
                }
            }
            catch (Exception ex)
            {
                errorLog?.Add($"[Fitting {dto.Id}] ApplyTransform: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static Transform ComputeRotationDelta(Transform T_current, Transform T_target)
        {
            var delta = Transform.Identity;
            delta.BasisX = T_current.BasisX.X * T_target.BasisX
                         + T_current.BasisY.X * T_target.BasisY
                         + T_current.BasisZ.X * T_target.BasisZ;
            delta.BasisY = T_current.BasisX.Y * T_target.BasisX
                         + T_current.BasisY.Y * T_target.BasisY
                         + T_current.BasisZ.Y * T_target.BasisZ;
            delta.BasisZ = T_current.BasisX.Z * T_target.BasisX
                         + T_current.BasisY.Z * T_target.BasisY
                         + T_current.BasisZ.Z * T_target.BasisZ;
            return delta;
        }

        private static bool TryExtractRotation(Transform deltaT, out XYZ axis, out double angle)
        {
            double trace    = deltaT.BasisX.X + deltaT.BasisY.Y + deltaT.BasisZ.Z;
            double cosAngle = Math.Max(-1.0, Math.Min(1.0, (trace - 1.0) / 2.0));
            angle = Math.Acos(cosAngle);

            if (angle < RotationEpsilon) { axis = XYZ.BasisZ; return false; }

            double sinA = Math.Sin(angle);
            if (Math.Abs(sinA) < RotationEpsilon)
            {
                double xx = (deltaT.BasisX.X + 1.0) / 2.0;
                double yy = (deltaT.BasisY.Y + 1.0) / 2.0;
                double zz = (deltaT.BasisZ.Z + 1.0) / 2.0;

                if (xx >= yy && xx >= zz)
                {
                    double nx = Math.Sqrt(Math.Max(0.0, xx));
                    double ny = nx > Epsilon ? deltaT.BasisX.Y / (2.0 * nx) : 0.0;
                    double nz = nx > Epsilon ? deltaT.BasisX.Z / (2.0 * nx) : 0.0;
                    axis = new XYZ(nx, ny, nz);
                }
                else if (yy >= zz)
                {
                    double ny = Math.Sqrt(Math.Max(0.0, yy));
                    double nx = ny > Epsilon ? deltaT.BasisY.X / (2.0 * ny) : 0.0;
                    double nz = ny > Epsilon ? deltaT.BasisY.Z / (2.0 * ny) : 0.0;
                    axis = new XYZ(nx, ny, nz);
                }
                else
                {
                    double nz = Math.Sqrt(Math.Max(0.0, zz));
                    double nx = nz > Epsilon ? deltaT.BasisZ.X / (2.0 * nz) : 0.0;
                    double ny = nz > Epsilon ? deltaT.BasisZ.Y / (2.0 * nz) : 0.0;
                    axis = new XYZ(nx, ny, nz);
                }

                if (axis.GetLength() < Epsilon) axis = XYZ.BasisZ;
                else axis = axis.Normalize();
                return true;
            }

            double ax = (deltaT.BasisY.Z - deltaT.BasisZ.Y) / (2.0 * sinA);
            double ay = (deltaT.BasisZ.X - deltaT.BasisX.Z) / (2.0 * sinA);
            double az = (deltaT.BasisX.Y - deltaT.BasisY.X) / (2.0 * sinA);
            axis = new XYZ(ax, ay, az);

            if (axis.GetLength() < Epsilon) { axis = XYZ.BasisZ; return false; }
            axis = axis.Normalize();
            return true;
        }
    }
}
