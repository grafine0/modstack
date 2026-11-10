using System;
using System.Threading.Tasks;

namespace ModStack.Services.Interfaces;

public interface IMailCatcherService
{
    bool IsRunning { get; }
    
    event EventHandler? NewEmailReceived;
    
    Task StartAsync();
    Task StopAsync();
}
