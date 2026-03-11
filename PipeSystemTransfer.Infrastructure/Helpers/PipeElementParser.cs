using Autodesk.Revit.DB;
using PipeSystemTransfer.Core.Models;

namespace PipeSystemTransfer.Infrastructure.Helpers
{
    internal static class PipeElementParser
    {
        internal static XYZ ParseXYZ(Point3D pt)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return new XYZ(
                double.Parse(pt.X, inv),
                double.Parse(pt.Y, inv),
                double.Parse(pt.Z, inv));
        }

        internal static Transform ParseTransform(TransformDto t)
        {
            var tr    = Transform.Identity;
            tr.BasisX = ParseXYZ(t.BasisX);
            tr.BasisY = ParseXYZ(t.BasisY);
            tr.BasisZ = ParseXYZ(t.BasisZ);
            tr.Origin  = ParseXYZ(t.Origin);
            return tr;
        }
    }
}
