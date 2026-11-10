using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ModStack.Common.Models
{
    /// <summary>
    /// Represents an Apache module that can be enabled or disabled
    /// </summary>
    public class ApacheModule
    {
        public long Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(150)]
        public string ModuleName { get; set; } = string.Empty; // e.g., "ssl_module"
        
        [Required]
        [MaxLength(200)]
        public string FileName { get; set; } = string.Empty; // e.g., "modules/mod_ssl.so"
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [MaxLength(50)]
        public string? Category { get; set; } // e.g., "Security", "Performance", "PHP", etc.
        
        public bool IsEnabled { get; set; } = false;
        public bool IsRequired { get; set; } = false; // Core modules that shouldn't be disabled
        public bool IsAvailable { get; set; } = true; // Whether the .so file exists
        
        [MaxLength(20)]
        public string? ApacheVersion { get; set; } // e.g., "2.4.65"
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// ViewModel for Apache module display in UI
    /// </summary>
    public class ApacheModuleViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled;
        private bool _isRequired;
        private bool _isAvailable;

        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        
        public bool IsEnabled 
        { 
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(ToggleButtonText));
                }
            }
        }
        
        public bool IsRequired 
        { 
            get => _isRequired;
            set
            {
                if (_isRequired != value)
                {
                    _isRequired = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanToggle));
                }
            }
        }
        
        public bool IsAvailable 
        { 
            get => _isAvailable;
            set
            {
                if (_isAvailable != value)
                {
                    _isAvailable = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvailabilityText));
                    OnPropertyChanged(nameof(CanToggle));
                }
            }
        }
        
        public string ApacheVersion { get; set; } = string.Empty;
        
        public string StatusText => IsEnabled ? "Enabled" : "Disabled";
        public string AvailabilityText => IsAvailable ? "Available" : "Missing";
        public bool CanToggle => IsAvailable && !IsRequired;
        public string ToggleButtonText => IsEnabled ? "Disable" : "Enable";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
