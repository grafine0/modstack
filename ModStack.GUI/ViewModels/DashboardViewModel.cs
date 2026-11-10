using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ModStack.Common.Models;
using ModStack.GUI.Models;

namespace ModStack.GUI.ViewModels;

public class DashboardViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // Stats properties
    private int _totalServicesCount;
    private int _totalStacksCount;
    private int _projectsCount;
    private int _totalPackagesCount;

    // Collections
    public ObservableCollection<Stack> AvailableStacks { get; } = new();
    public ObservableCollection<ServiceDisplayItem> Services { get; } = new();

    // Stats properties with change notification
    public int TotalServicesCount
    {
        get => _totalServicesCount;
        set => SetProperty(ref _totalServicesCount, value);
    }

    public int TotalStacksCount
    {
        get => _totalStacksCount;
        set => SetProperty(ref _totalStacksCount, value);
    }

    public int ProjectsCount
    {
        get => _projectsCount;
        set => SetProperty(ref _projectsCount, value);
    }

    public int TotalPackagesCount
    {
        get => _totalPackagesCount;
        set => SetProperty(ref _totalPackagesCount, value);
    }

    // Selected stack property
    private Stack? _selectedStack;
    public Stack? SelectedStack
    {
        get => _selectedStack;
        set => SetProperty(ref _selectedStack, value);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
