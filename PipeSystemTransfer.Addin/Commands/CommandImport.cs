using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PipeSystemTransfer.Infrastructure.Services;
using PipeSystemTransfer.UI.ViewModels;
using PipeSystemTransfer.UI.Views;

namespace PipeSystemTransfer.Addin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CommandImport : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            var importService = new PipeImportService(doc);
            var jsonService = new JsonService();
            var viewModel = new ImportViewModel(importService, jsonService);
            var window = new ImportWindow(viewModel);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
