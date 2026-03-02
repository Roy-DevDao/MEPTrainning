using System.IO;
using Newtonsoft.Json;
using PipeSystemTransfer.Core.Interfaces;
using PipeSystemTransfer.Core.Models;

namespace PipeSystemTransfer.Infrastructure.Services
{
    public class JsonService : IJsonService
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public string Serialize(PipeSystemDto pipeSystem)
            => JsonConvert.SerializeObject(pipeSystem, Settings);

        public PipeSystemDto Deserialize(string json)
            => JsonConvert.DeserializeObject<PipeSystemDto>(json, Settings);

        public void SaveToFile(PipeSystemDto pipeSystem, string filePath)
        {
            var json = Serialize(pipeSystem);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        public PipeSystemDto LoadFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return Deserialize(json);
        }
    }
}
