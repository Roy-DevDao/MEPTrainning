using Microsoft.Win32;
using PipeSystemTransfer.Core.Interfaces;
using PipeSystemTransfer.Core.Models;
using PipeSystemTransfer.UI.Common;
using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;

namespace PipeSystemTransfer.UI.ViewModels
{
    public class ImportViewModel : ViewModelBase
    {
        private readonly IImportService _importService;
        private readonly IJsonService _jsonService;
        private readonly Dispatcher _dispatcher;

        private string _filePath;
        private string _statusMessage;
        private bool _isBusy;
        private double _progressValue;
        private string _progressText = "";
        private double _lastDispatchPct;
        private PipeSystemDto _loaded;

        public string FilePath
        {
            get => _filePath;
            set { SetProperty(ref _filePath, value); LoadPreview(); }
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

        public string PreviewText => _loaded == null
            ? "Chưa tải file"
            : $"File: {System.IO.Path.GetFileName(_filePath)}\nNgày: {_loaded.ExportedAt:dd/MM/yyyy HH:mm}\nRevit: {_loaded.ExportedFrom}\nPipes: {_loaded.Pipes.Count} | Fittings: {_loaded.Fittings.Count}";

        public ICommand BrowseCommand { get; }
        public ICommand ImportCommand { get; }

        public ImportViewModel(IImportService importService, IJsonService jsonService)
        {
            _importService = importService;
            _jsonService = jsonService;
            _dispatcher = Dispatcher.CurrentDispatcher;
            BrowseCommand = new RelayCommand(BrowseFile);
            ImportCommand = new RelayCommand(ExecuteImport, CanImport);
        }

        private void BrowseFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file JSON hệ thống ống",
                Filter = "JSON files (*.json)|*.json"
            };
            if (dialog.ShowDialog() == true)
                FilePath = dialog.FileName;
        }

        private void LoadPreview()
        {
            if (string.IsNullOrWhiteSpace(FilePath)) return;
            try
            {
                _loaded = _jsonService.LoadFromFile(FilePath);
                OnPropertyChanged(nameof(PreviewText));
                StatusMessage = "Tải file thành công. Nhấn Import để vẽ.";
            }
            catch (Exception ex)
            {
                _loaded = null;
                OnPropertyChanged(nameof(PreviewText));
                StatusMessage = $"Lỗi tải file: {ex.Message}";
            }
        }

        private bool CanImport() => !IsBusy && _loaded != null;

        private void ExecuteImport()
        {
            IsBusy = true;
            ProgressValue = 0;
            ProgressText = "";
            _lastDispatchPct = -1;
            StatusMessage = "Đang import hệ thống ống...";
            try
            {
                var result = _importService.ImportPipeSystem(_loaded, report =>
                {
                    ProgressValue = report.Percentage;
                    ProgressText = report.Message;
                    if (report.Percentage - _lastDispatchPct >= 1.0 || report.Percentage >= 100)
                    {
                        _lastDispatchPct = report.Percentage;
                        _dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                    }
                });
                if (result.Success)
                {
                    var msg = $"Import thành công! Pipes: {result.CreatedPipes} (nối: {result.JoinedConnectors}), Fittings: {result.CreatedFittings}.";
                    if (result.FailedElements > 0)
                        msg += $" Lỗi: {result.FailedElements}.";
                    if (result.MissingFamilies.Count > 0)
                    {
                        const int maxShow = 8;
                        var shown = result.MissingFamilies.Take(maxShow).ToList();
                        var more  = result.MissingFamilies.Count - shown.Count;
                        msg += $"\nThiếu {result.MissingFamilies.Count} family (cần load vào file đích):\n• "
                             + string.Join("\n• ", shown);
                        if (more > 0)
                            msg += $"\n• ... và {more} family khác";
                    }
                    try
                    {
                        var logFolder = @"D:\BAO";
                        if (!Directory.Exists(logFolder))
                            Directory.CreateDirectory(logFolder);

                        var logPath = Path.Combine(
                            logFolder,
                            $"PipeImport_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                        var logLines = new System.Collections.Generic.List<string>
                        {
                            $"=== PipeSystemTransfer Import Log ===",
                            $"Thời gian : {DateTime.Now:dd/MM/yyyy HH:mm:ss}",
                            $"File JSON : {_filePath}",
                            $"Nguồn     : {_loaded.ExportedFrom}",
                            $"",
                            $"[Kết quả]",
                            $"  Pipes tạo thành công : {result.CreatedPipes}",
                            $"  Fittings tạo thành công : {result.CreatedFittings}",
                            $"  Kết nối (auto): {result.JoinedConnectors}",
                            $"  Phần tử lỗi  : {result.FailedElements}",
                        };

                        if (result.MissingFamilies.Count > 0)
                        {
                            logLines.Add("");
                            logLines.Add($"[Family bị thiếu — {result.MissingFamilies.Count}]");
                            foreach (var f in result.MissingFamilies)
                                logLines.Add($"  • {f}");
                        }

                        if (result.ErrorLog.Count > 0)
                        {
                            logLines.Add("");
                            logLines.Add($"[Chi tiết lỗi — {result.ErrorLog.Count} dòng]");
                            logLines.AddRange(result.ErrorLog);
                        }

                        File.WriteAllLines(logPath, logLines,
                            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                        msg += $"\n\nLog import: {logPath}";
                    }
                    catch (Exception ex)
                    {
                        msg += $"\nKhông ghi được log: {ex.Message}";
                    }
                    StatusMessage = msg;
                }
                else
                {
                    StatusMessage = $"Import thất bại: {result.ErrorMessage}";
                }
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
