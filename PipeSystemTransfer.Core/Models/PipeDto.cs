using System.Collections.Generic;

namespace PipeSystemTransfer.Core.Models
{
    public class PipeDto : PipeElementDto
    {
        public string            SystemTypeName { get; set; }
        public string            PipeTypeName   { get; set; }
        public double            Diameter       { get; set; }
        public Point3D           StartPoint     { get; set; } = new Point3D();
        public Point3D           EndPoint       { get; set; } = new Point3D();
        public List<ConnectorDto> Connectors    { get; set; } = new List<ConnectorDto>();
    }
}
