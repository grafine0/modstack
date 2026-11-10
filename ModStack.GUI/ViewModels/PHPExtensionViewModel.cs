using System.ComponentModel;
using System.Runtime.CompilerServices;
using ModStack.Common.Models;

namespace ModStack.GUI.ViewModels
{
    public class PHPExtensionViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _category = string.Empty;
        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (SetProperty(ref _isInstalled, value))
                {
                    // Notify that ToggleButtonText changed when IsInstalled changes
                    OnPropertyChanged(nameof(ToggleButtonText));
                }
            }
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    // Notify that ToggleButtonText changed when IsEnabled changes
                    OnPropertyChanged(nameof(ToggleButtonText));
                    OnPropertyChanged(nameof(ToggleButtonIcon));
                }
            }
        }

        private bool _isPopular;
        public bool IsPopular
        {
            get => _isPopular;
            set => SetProperty(ref _isPopular, value);
        }

        private bool _isBuiltIn;
        public bool IsBuiltIn
        {
            get => _isBuiltIn;
            set => SetProperty(ref _isBuiltIn, value);
        }

        private long _id;
        public long Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string ToggleButtonIcon => IsEnabled ? "⏸️" : "▶️";
        public string ToggleButtonText => !IsInstalled ? "Install" : (IsEnabled ? "Disable" : "Enable");
        public string StatusText => IsInstalled ? (IsEnabled ? "Enabled" : "Disabled") : "Not Installed";

        public PHPExtensionViewModel()
        {
        }

        public PHPExtensionViewModel(PHPExtension extension)
        {
            Id = extension.Id;
            Name = extension.Name;
            Description = extension.Description ?? string.Empty;
            Category = extension.Category ?? string.Empty;
            IsPopular = extension.IsPopular;
            IsBuiltIn = extension.IsBuiltIn;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
