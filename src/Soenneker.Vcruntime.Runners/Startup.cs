using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Soenneker.Vcruntime.Runners.Utils;
using Soenneker.Vcruntime.Runners.Utils.Abstract;
using Soenneker.Managers.Runners.Registrars;

namespace Soenneker.Vcruntime.Runners;

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services) => services.SetupIoC();

    public static IServiceCollection SetupIoC(this IServiceCollection services)
    {
        services.AddHostedService<ConsoleHostedService>()
            .AddSingleton(_ =>
            {
                var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Soenneker.Vcruntime.Runners/1.0");
                return client;
            })
            .AddSingleton<IFileOperationsUtil, FileOperationsUtil>()
            .AddRunnersManagerAsSingleton();
        return services;
    }
}

