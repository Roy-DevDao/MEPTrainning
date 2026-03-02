namespace PipeSystemTransfer.Core.Models
{
    public class ProgressReport
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string Message { get; set; }
        public double Percentage => Total > 0 ? (double)Current / Total * 100.0 : 0;
    }
}
