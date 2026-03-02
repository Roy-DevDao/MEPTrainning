using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using PipeSystemTransfer.Core.Interfaces;
using PipeSystemTransfer.Core.Models;
using PipeSystemTransfer.Infrastructure.Mapper;

namespace PipeSystemTransfer.Infrastructure.Services
{
    public class PipeExportService : IExportService
    {
        private readonly Document _doc;

        public PipeExportService(Document doc)
        {
            _doc = doc;
        }

        public PipeSystemDto ExportPipeSystem(Action<ProgressReport> onProgress = null)
        {
            var result = new PipeSystemDto
            {
                ExportedFrom = _doc.Title,
                RevitVersion = _doc.Application.VersionNumber
            };

            var pipes    = CollectAll<Pipe>(typeof(Pipe), null);
            var fittings = CollectAll<FamilyInstance>(typeof(FamilyInstance), BuiltInCategory.OST_PipeFitting);

            int total   = pipes.Count + fittings.Count;
            int current = 0;

            Report(onProgress, 0, total, "Chuẩn bị...");

            for (int i = 0; i < pipes.Count; i++)
            {
                try { result.Pipes.Add(PipeMapper.ToDto(pipes[i])); }
                catch { }
                Report(onProgress, ++current, total, $"Đọc ống {i + 1}/{pipes.Count}");
            }

            for (int i = 0; i < fittings.Count; i++)
            {
                try { result.Fittings.Add(PipeMapper.ToFittingDto(fittings[i])); }
                catch {  }
                Report(onProgress, ++current, total, $"Đọc fitting {i + 1}/{fittings.Count}");
            }

            Report(onProgress, total, total, "Hoàn tất đọc dữ liệu.");
            return result;
        }

        private List<T> CollectAll<T>(Type elementType, BuiltInCategory? category) where T : Element
        {
            var collector = new FilteredElementCollector(_doc).OfClass(elementType);
            if (category.HasValue)
                collector = collector.OfCategory(category.Value);
            return collector.Cast<T>().ToList();
        }

        private static void Report(Action<ProgressReport> onProgress, int current, int total, string message)
        {
            onProgress?.Invoke(new ProgressReport { Current = current, Total = total, Message = message });
        }
    }
}
