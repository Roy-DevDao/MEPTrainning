using System.Collections.Generic;

namespace PipeSystemTransfer.Core.Models
{
    public enum FittingCategory
    {
        Elbow,
        Tee,
        Cross,
        Transition,
        Union,
        Cap,
        Flange,
        Coupling,
        Other
    }

    public class ConnectorDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double DirectionX { get; set; }
        public double DirectionY { get; set; }
        public double DirectionZ { get; set; }
        public double Radius { get; set; }
        public int ConnectedElementId { get; set; }
    }

    public class PipeFittingDto : PipeElementDto
    {
        public FittingCategory Category { get; set; }
        public double RotationAngle { get; set; }
        public double HandFlipValue { get; set; }
        public double FacingFlipValue { get; set; }
        public List<ConnectorDto> Connectors { get; set; } = new List<ConnectorDto>();
    }
}
