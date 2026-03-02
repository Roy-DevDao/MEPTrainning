using System;
using System.Collections.Generic;
using PipeSystemTransfer.Core.Models;

namespace PipeSystemTransfer.Core.Interfaces
{
    public interface IImportService
    {
        ImportResult ImportPipeSystem(PipeSystemDto pipeSystem, Action<ProgressReport> onProgress = null);
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public int CreatedPipes { get; set; }
        public int CreatedFittings { get; set; }
        public int JoinedConnectors { get; set; }
        public int FailedElements { get; set; }
        public string ErrorMessage { get; set; }
        public HashSet<string> MissingFamilies { get; set; } = new HashSet<string>();

        public int TotalCreated => CreatedPipes + CreatedFittings;
    }
}
