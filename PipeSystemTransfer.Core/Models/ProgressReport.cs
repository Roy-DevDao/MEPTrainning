using System;

namespace PipeSystemTransfer.Core.Models
{
    public class ProgressReport
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string Message { get; set; }
        public double Percentage => Total > 0 ? (double)Current / Total * 100.0 : 0;

        public static void Report(Action<ProgressReport> onProgress, int current, int total, string message)
            => onProgress?.Invoke(new ProgressReport { Current = current, Total = total, Message = message });
    }
}
