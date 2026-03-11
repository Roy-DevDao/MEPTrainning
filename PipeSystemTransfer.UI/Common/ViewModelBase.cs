using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using PipeSystemTransfer.Core.Models;

namespace PipeSystemTransfer.UI.Common
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private readonly Dispatcher _dispatcher;
        private string _statusMessage;
        private bool _isBusy;
        private double _progressValue;
        private string _progressText = "";
        private double _lastDispatchPct;

        protected ViewModelBase()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public event PropertyChangedEventHandler PropertyChanged;

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

        protected Action<ProgressReport> CreateProgressHandler()
        {
            _lastDispatchPct = -1;
            return report =>
            {
                ProgressValue = report.Percentage;
                ProgressText  = report.Message;
                if (report.Percentage - _lastDispatchPct >= 1.0 || report.Percentage >= 100)
                {
                    _lastDispatchPct = report.Percentage;
                    _dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
                }
            };
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
