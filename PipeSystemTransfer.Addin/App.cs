using Autodesk.Revit.UI;

namespace PipeSystemTransfer.Addin
{
    public class App : IExternalApplication
    {
        private const string TabName = "PipeTransfer";
        private const string PanelName = "Hệ Thống Ống";

        public Result OnStartup(UIControlledApplication app)
        {
            app.CreateRibbonTab(TabName);
            var panel = app.CreateRibbonPanel(TabName, PanelName);

            var addinPath = typeof(App).Assembly.Location;

            panel.AddItem(new PushButtonData(
                "ExportPipes",
                "Xuất JSON",
                addinPath,
                "PipeSystemTransfer.Addin.Commands.CommandExport")
            {
                ToolTip = "Xuất toàn bộ hệ thống ống ra file JSON"
            });

            panel.AddItem(new PushButtonData(
                "ImportPipes",
                "Import JSON",
                addinPath,
                "PipeSystemTransfer.Addin.Commands.CommandImport")
            {
                ToolTip = "Import hệ thống ống từ file JSON vào bản vẽ hiện tại"
            });

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
