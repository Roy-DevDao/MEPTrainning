using System.Collections.Generic;

namespace PipeSystemTransfer.Core.Models
{
    public abstract class PipeElementDto
    {
        public int RevitId { get; set; }
        public string UniqueId { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public double LocationX { get; set; }
        public double LocationY { get; set; }
        public double LocationZ { get; set; }
        public string LevelName { get; set; }
        public string SystemType { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
}
