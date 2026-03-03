using System.Collections.Generic;

namespace PipeSystemTransfer.Core.Models
{
    public class Point3D
    {
        public string X { get; set; }
        public string Y { get; set; }
        public string Z { get; set; }
    }

    public class TransformDto
    {
        public Point3D Origin { get; set; }
        public Point3D BasisX { get; set; }
        public Point3D BasisY { get; set; }
        public Point3D BasisZ { get; set; }
    }

    public class ConnectorDto
    {
        public int     ConnectorId   { get; set; }
        public Point3D Origin        { get; set; }
        public double  Diameter      { get; set; }
        public string  ConnectedToId { get; set; }
    }

    public class PipeFittingDto
    {
        public TransformDto       Transform     { get; set; }
        public string             Id            { get; set; }
        public string             LevelName     { get; set; }
        public string             FamilyName    { get; set; }
        public string             TypeName      { get; set; }
        public Point3D            LocationPoint { get; set; } = new Point3D();
        public double             Angle         { get; set; }  // degrees
        public List<ConnectorDto> Connectors    { get; set; } = new List<ConnectorDto>();
        public bool               Mirrored      { get; set; }
        public bool               HandFlipped   { get; set; }
        public bool               FacingFlipped { get; set; }
    }
}
