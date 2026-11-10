using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Services.Interfaces;
using ModStack.GUI.Services.Interfaces;

namespace ModStack.GUI.ViewModels;

public partial class MailCatcherViewModel : ObservableObject
{
    private readonly IMailCatcherRepository _repository;
    private readonly IMailCatcherService _mailCatcherService;
    private readonly IDialogService _dialogService;
    private readonly Dispatcher _dispatcher;

    [ObservableProperty]
    private ObservableCollection<InterceptedEmail> _emails = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    private InterceptedEmail? _selectedEmail;

    [ObservableProperty]
    private bool _isLoading;

    public MailCatcherViewModel(
        IMailCatcherRepository repository, 
        IMailCatcherService mailCatcherService,
        IDialogService dialogService)
    {
        _repository = repository;
        _mailCatcherService = mailCatcherService;
        _dialogService = dialogService;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _mailCatcherService.NewEmailReceived += OnNewEmailReceived;
        Emails.CollectionChanged += (s, e) => 
        {
            ClearAllCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsNotEmpty));
        };
        
        // Initial load
        _ = LoadEmailsAsync();
    }

    private void OnNewEmailReceived(object? sender, EventArgs e)
    {
        // Must refresh on UI thread
        _dispatcher.InvokeAsync(async () => await LoadEmailsAsync());
    }

    [RelayCommand]
    private async Task LoadEmailsAsync()
    {
        IsLoading = true;
        try
        {
            var emails = await _repository.GetAllAsync();
            Emails.Clear();
            foreach (var email in emails)
            {
                Emails.Add(email);
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Failed to load emails: " + ex.Message, "Error");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsNotEmpty));
            ClearAllCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsEmpty => Emails.Count == 0;
    public bool IsNotEmpty => Emails.Count > 0;

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        var result = await _dialogService.ShowConfirmationAsync("Are you sure you want to delete all intercepted emails?", "Clear All Emails");
        if (result == true)
        {
            await _repository.DeleteAllAsync();
            Emails.Clear();
            SelectedEmail = null;
        }
    }

    private bool CanDeleteSelected => SelectedEmail != null;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedEmail != null)
        {
            await _repository.DeleteAsync(SelectedEmail.Id);
            Emails.Remove(SelectedEmail);
            SelectedEmail = null;
        }
    }

    partial void OnSelectedEmailChanged(InterceptedEmail? value)
    {
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        
        if (value != null && !value.IsRead)
        {
            // Mark as read in background
            _ = Task.Run(async () =>
            {
                await _repository.MarkAsReadAsync(value.Id);
                // We update the local object so the UI might reflect it, but it requires INotifyPropertyChanged on the model if we wanted it to update live.
                value.IsRead = true; 
            });
        }
    }
}
