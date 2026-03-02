using System;
using System.Collections.Generic;

namespace PipeSystemTransfer.Core.Models
{
    public class PipeSystemDto
    {
        public string ExportedFrom { get; set; }
        public DateTime ExportedAt { get; set; } = DateTime.Now;
        public string RevitVersion { get; set; }
        public List<PipeDto> Pipes { get; set; } = new List<PipeDto>();
        public List<PipeFittingDto> Fittings { get; set; } = new List<PipeFittingDto>();

        public int TotalCount => Pipes.Count + Fittings.Count;
    }
}
