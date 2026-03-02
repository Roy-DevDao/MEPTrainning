using System;
using PipeSystemTransfer.Core.Models;

namespace PipeSystemTransfer.Core.Interfaces
{
    public interface IExportService
    {
        PipeSystemDto ExportPipeSystem(Action<ProgressReport> onProgress = null);
    }
}
