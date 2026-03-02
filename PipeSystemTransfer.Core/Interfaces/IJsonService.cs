using PipeSystemTransfer.Core.Models;

namespace PipeSystemTransfer.Core.Interfaces
{
    public interface IJsonService
    {
        string Serialize(PipeSystemDto pipeSystem);
        PipeSystemDto Deserialize(string json);
        void SaveToFile(PipeSystemDto pipeSystem, string filePath);
        PipeSystemDto LoadFromFile(string filePath);
    }
}
