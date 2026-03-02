using System.Windows;
using PipeSystemTransfer.UI.ViewModels;

namespace PipeSystemTransfer.UI.Views
{
    public partial class ExportWindow : Window
    {
        public ExportWindow(ExportViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
