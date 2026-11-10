using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MimeKit;
using ModStack.Common.Models;
using ModStack.Core.Services;
using ModStack.Data.Interfaces;
using ModStack.Services.Interfaces;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace ModStack.Services;

public class MailCatcherService : IMailCatcherService
{
    private readonly IMailCatcherRepository _repository;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<MailCatcherService> _logger;
    private SmtpServer.SmtpServer? _smtpServer;
    private CancellationTokenSource? _cancellationTokenSource;

    public bool IsRunning { get; private set; }
    public event EventHandler? NewEmailReceived;

    public MailCatcherService(IMailCatcherRepository repository, ISettingsService settingsService, ILogger<MailCatcherService> logger)
    {
        _repository = repository;
        _settingsService = settingsService;
        _logger = logger;
    }

    public Task StartAsync()
    {
        if (IsRunning)
            return Task.CompletedTask;

        if (!_settingsService.EnableMailCatcher)
        {
            _logger.LogInformation("Mail Catcher is disabled in settings. Skipping start.");
            return Task.CompletedTask;
        }

        int port = _settingsService.MailCatcherPort > 0 ? _settingsService.MailCatcherPort : 1025;
        _logger.LogInformation("Starting Mail Catcher SMTP Service on port {Port}...", port);

        var options = new SmtpServerOptionsBuilder()
            .ServerName("localhost")
            .Port(port)
            .Build();

        var serviceProvider = new SmtpServer.ComponentModel.ServiceProvider();
        serviceProvider.Add(new ModStackMessageStore(_repository, this, _logger));

        _smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);
        
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Start in background
        _ = Task.Run(async () =>
        {
            try
            {
                IsRunning = true;
                await _smtpServer.StartAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mail Catcher SMTP server stopped unexpectedly.");
            }
            finally
            {
                IsRunning = false;
            }
        });

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!IsRunning)
            return Task.CompletedTask;

        _logger.LogInformation("Stopping Mail Catcher SMTP Service...");
        
        if (_smtpServer != null)
        {
            _smtpServer.Shutdown();
        }
        
        _cancellationTokenSource?.Cancel();
        IsRunning = false;

        return Task.CompletedTask;
    }

    internal void NotifyEmailReceived()
    {
        NewEmailReceived?.Invoke(this, EventArgs.Empty);
    }
}

internal class ModStackMessageStore : MessageStore
{
    private readonly IMailCatcherRepository _repository;
    private readonly MailCatcherService _service;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    public ModStackMessageStore(IMailCatcherRepository repository, MailCatcherService service, Microsoft.Extensions.Logging.ILogger logger)
    {
        _repository = repository;
        _service = service;
        _logger = logger;
    }

    public override async Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            using var ms = new MemoryStream(buffer.ToArray());
            var message = await MimeMessage.LoadAsync(ms, cancellationToken);
            
            var interceptedEmail = new InterceptedEmail
            {
                MessageId = message.MessageId ?? Guid.NewGuid().ToString(),
                Sender = message.From.Count > 0 ? message.From[0].ToString() : (transaction.From?.User + "@" + transaction.From?.Host) ?? "unknown",
                Recipient = message.To.Count > 0 ? string.Join(", ", message.To.Select(x => x.ToString())) : string.Join(", ", transaction.To.Select(x => x.User + "@" + x.Host)),
                Subject = message.Subject ?? "(No Subject)",
                BodyHtml = message.HtmlBody ?? string.Empty,
                BodyText = message.TextBody ?? string.Empty,
                RawHeaders = string.Join("\n", message.Headers.Select(h => $"{h.Field}: {h.Value}")),
                ReceivedAt = DateTime.UtcNow,
                IsRead = false
            };

            await _repository.InsertAsync(interceptedEmail);
            
            _logger.LogInformation("Intercepted email from {Sender} to {Recipient}: {Subject}", interceptedEmail.Sender, interceptedEmail.Recipient, interceptedEmail.Subject);
            
            _service.NotifyEmailReceived();
            
            return SmtpResponse.Ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse and save intercepted email.");
            return SmtpResponse.TransactionFailed;
        }
    }
}
