using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PipeSystemTransfer.Infrastructure.Services;
using PipeSystemTransfer.UI.ViewModels;
using PipeSystemTransfer.UI.Views;

namespace PipeSystemTransfer.Addin.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CommandExport : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            var exportService = new PipeExportService(doc);
            var jsonService = new JsonService();
            var viewModel = new ExportViewModel(exportService, jsonService);
            var window = new ExportWindow(viewModel);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
