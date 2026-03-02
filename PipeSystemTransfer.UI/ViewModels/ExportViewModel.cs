using System;
using System.Windows.Input;
using System.Windows.Threading;
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
        private readonly Dispatcher _dispatcher;

        private string _filePath;
        private string _statusMessage;
        private bool _isBusy;
        private double _progressValue;
        private string _progressText = "";
        private double _lastDispatchPct;
        private PipeSystemDto _lastExported;

        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public string SummaryText => _lastExported == null
            ? "Chưa xuất dữ liệu"
            : $"Pipes: {_lastExported.Pipes.Count} | Fittings: {_lastExported.Fittings.Count} | Tổng: {_lastExported.TotalCount}";

        public ICommand BrowseCommand { get; }
        public ICommand ExportCommand { get; }

        public ExportViewModel(IExportService exportService, IJsonService jsonService)
        {
            _exportService = exportService;
            _jsonService = jsonService;
            _dispatcher = Dispatcher.CurrentDispatcher;
            BrowseCommand = new RelayCommand(BrowseFile);
            ExportCommand = new RelayCommand(ExecuteExport, CanExport);
        }

        private void BrowseFile()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Lưu file JSON hệ thống ống",
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = ".json",
                FileName = "PipeSystem_Export"
            };
            if (dialog.ShowDialog() == true)
                FilePath = dialog.FileName;
        }

        private bool CanExport() => !IsBusy && !string.IsNullOrWhiteSpace(FilePath);

        private void ExecuteExport()
        {
            IsBusy = true;
            ProgressValue = 0;
            ProgressText = "";
            _lastDispatchPct = -1;
            StatusMessage = "Đang xuất dữ liệu...";
            try
            {
                _lastExported = _exportService.ExportPipeSystem(report =>
                {
                    ProgressValue = report.Percentage;
                    ProgressText = report.Message;
                    if (report.Percentage - _lastDispatchPct >= 1.0 || report.Percentage >= 100)
                    {
                        _lastDispatchPct = report.Percentage;
                        _dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                    }
                });
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
