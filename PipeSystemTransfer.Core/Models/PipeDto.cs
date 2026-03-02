namespace PipeSystemTransfer.Core.Models
{
    public class PipeDto : PipeElementDto
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double StartZ { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double EndZ { get; set; }
        public double Diameter { get; set; }
        public double Length { get; set; }
        public string PipeTypeName { get; set; }
        public string InsulationTypeName { get; set; }
        public double InsulationThickness { get; set; }
    }
}
