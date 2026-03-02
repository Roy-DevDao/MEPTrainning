using System.Windows;
using PipeSystemTransfer.UI.ViewModels;

namespace PipeSystemTransfer.UI.Views
{
    public partial class ImportWindow : Window
    {
        public ImportWindow(ImportViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
