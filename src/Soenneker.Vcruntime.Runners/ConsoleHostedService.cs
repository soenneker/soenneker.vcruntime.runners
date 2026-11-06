using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Soenneker.Managers.Runners.Abstract;
using Soenneker.Vcruntime.Runners.Utils.Abstract;

namespace Soenneker.Vcruntime.Runners;

public sealed class ConsoleHostedService(IFileOperationsUtil files, IRunnersManager runners, IConfiguration configuration,
    IHostApplicationLifetime lifetime, ILogger<ConsoleHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string stage = Directory.CreateTempSubdirectory("vcruntime-").FullName;
        try
        {
            await files.Process(stage, stoppingToken);
            string? output = configuration["Runner:OutputDirectory"];
            if (!string.IsNullOrWhiteSpace(output))
            {
                foreach (string file in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    string destination = Path.Combine(Path.GetFullPath(output), Path.GetRelativePath(stage, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file, destination, overwrite: true);
                }
                logger.LogInformation("Saved the Windows x64 runtime to {OutputDirectory}", Path.GetFullPath(output));
            }
            else
            {
                string relativePath = Path.Combine("win-x64", "vcruntime");
                await runners.PushIfChangesNeededForDirectory(relativePath, Path.Combine(stage, relativePath), "Soenneker.Vcruntime.Library",
                    "https://github.com/soenneker/soenneker.vcruntime.library", cancellationToken: stoppingToken);
            }
            Environment.ExitCode = 0;
        }
        catch (Exception exception)
        {
            Environment.ExitCode = 1;
            logger.LogError(exception, "Could not update the Visual C++ runtime");
        }
        finally
        {
            try { Directory.Delete(stage, recursive: true); }
            finally { lifetime.StopApplication(); }
        }
    }
}

