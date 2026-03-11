using System;
using System.Windows.Input;
using Microsoft.Win32;
using PipeSystemTransfer.Core.Interfaces;
using PipeSystemTransfer.Core.Models;
using PipeSystemTransfer.UI.Common;

namespace PipeSystemTransfer.UI.ViewModels
{
    public class ExportViewModel : ViewModelBase
    {
        private readonly IExportService _exportService;
        private readonly IJsonService _jsonService;

        private string _filePath;
        private PipeSystemDto _lastExported;

        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string SummaryText => _lastExported == null
            ? "Chưa xuất dữ liệu"
            : $"Pipes: {_lastExported.Pipes.Count} | Fittings: {_lastExported.Fittings.Count} | Tổng: {_lastExported.TotalCount}";

        public ICommand BrowseCommand { get; }
        public ICommand ExportCommand { get; }

        public ExportViewModel(IExportService exportService, IJsonService jsonService)
        {
            _exportService = exportService;
            _jsonService   = jsonService;
            BrowseCommand  = new RelayCommand(BrowseFile);
            ExportCommand  = new RelayCommand(ExecuteExport, CanExport);
        }

        private void BrowseFile()
        {
            var dialog = new SaveFileDialog
            {
                Title      = "Lưu file JSON hệ thống ống",
                Filter     = "JSON files (*.json)|*.json",
                DefaultExt = ".json",
                FileName   = "PipeSystem_Export"
            };
            if (dialog.ShowDialog() == true)
                FilePath = dialog.FileName;
        }

        private bool CanExport() => !IsBusy && !string.IsNullOrWhiteSpace(FilePath);

        private void ExecuteExport()
        {
            IsBusy        = true;
            ProgressValue = 0;
            ProgressText  = "";
            StatusMessage = "Đang xuất dữ liệu...";
            try
            {
                _lastExported = _exportService.ExportPipeSystem(CreateProgressHandler());
                _jsonService.SaveToFile(_lastExported, FilePath);
                OnPropertyChanged(nameof(SummaryText));
                StatusMessage = $"Xuất thành công! {_lastExported.TotalCount} phần tử → {FilePath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
