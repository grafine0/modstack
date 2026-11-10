using System;
using System.Threading;
using System.Threading.Tasks;
using ModStack.Common.Models;

namespace ModStack.Core.Interfaces;

public interface IUpdateService
{
    Task<UpdateInfo> CheckForUpdatesAsync();
    Task DownloadUpdateAsync(string downloadUrl, IProgress<double> progress, CancellationToken cancellationToken);
    void InstallUpdate(string installerPath);
}
