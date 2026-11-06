using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soenneker.Vcruntime.Runners.Utils.Abstract;

namespace Soenneker.Vcruntime.Runners.Utils;

public sealed class FileOperationsUtil(HttpClient client, IConfiguration configuration, ILogger<FileOperationsUtil> logger) : IFileOperationsUtil
{
    public async ValueTask Process(string outputDirectory, CancellationToken cancellationToken = default)
    {
        string channelUrl = configuration["Runner:ChannelUrl"] ?? "https://aka.ms/vs/17/release/channel";
        using JsonDocument channel = JsonDocument.Parse(await client.GetStringAsync(channelUrl, cancellationToken));
        JsonElement catalogPayload = channel.RootElement.GetProperty("channelItems").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "Microsoft.VisualStudio.Manifests.VisualStudio")
            .GetProperty("payloads")[0];
        using JsonDocument catalog = JsonDocument.Parse(await client.GetStringAsync(
            catalogPayload.GetProperty("url").GetString()!, cancellationToken));
        JsonElement package = catalog.RootElement.GetProperty("packages").EnumerateArray()
            .Where(item => Regex.IsMatch(item.GetProperty("id").GetString()!,
                @"^Microsoft\.VC\.[0-9.]+\.CRT\.Redist\.X64\.base$", RegexOptions.IgnoreCase))
            .OrderByDescending(item => Version.Parse(item.GetProperty("version").GetString()!))
            .FirstOrDefault();
        if (package.ValueKind == JsonValueKind.Undefined)
            throw new InvalidDataException("The Microsoft catalog contains no Windows x64 CRT redistributable.");

        string version = package.GetProperty("version").GetString()!;
        JsonElement payload = package.GetProperty("payloads").EnumerateArray()
            .Single(item => item.GetProperty("fileName").GetString()!.EndsWith(".vsix", StringComparison.OrdinalIgnoreCase));
        byte[] bytes = await Download(payload, cancellationToken);
        string destination = Path.Combine(outputDirectory, "win-x64", "vcruntime");
        Directory.CreateDirectory(destination);
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        ZipArchiveEntry[] entries = archive.Entries.Where(entry => Regex.IsMatch(entry.FullName,
            @"^Contents/VC/Redist/MSVC/[^/]+/x64/Microsoft\.VC[0-9]+\.CRT/[^/]+\.dll$", RegexOptions.IgnoreCase)).ToArray();
        if (!entries.Any(entry => entry.Name.Equals("vcruntime140.dll", StringComparison.OrdinalIgnoreCase)) ||
            entries.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
            throw new InvalidDataException("The redistributable must contain vcruntime140.dll and unique retail CRT filenames.");

        foreach (ZipArchiveEntry entry in entries)
        {
            if (entry.Length == 0)
                throw new InvalidDataException($"The runtime DLL {entry.Name} is empty.");
            await using Stream source = entry.Open();
            await using FileStream target = File.Create(Path.Combine(destination, entry.Name));
            await source.CopyToAsync(target, cancellationToken);
        }

        await File.WriteAllTextAsync(Path.Combine(destination, "NOTICE.vcruntime.txt"),
            "Microsoft Visual C++ Runtime\nCopyright (c) Microsoft Corporation. All rights reserved.\n" +
            "Microsoft DLLs are distributed under Microsoft's terms, not the wrapper's MIT license.\n" +
            "License: https://visualstudio.microsoft.com/license-terms/vs2022-ga-diagnosticbuildtools/\n" +
            "Redistributable list: https://learn.microsoft.com/visualstudio/releases/2022/redistribution\n", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(destination, "SOURCE.txt"),
            $"Version: {version}\nChannel: {channelUrl}\nPackage: {package.GetProperty("id").GetString()}\n" +
            $"Source: {payload.GetProperty("url").GetString()}\nSHA256: {payload.GetProperty("sha256").GetString()}\n", cancellationToken);
        logger.LogInformation("Prepared {Count} Windows x64 CRT libraries, version {Version}, in {Destination}", entries.Length, version, destination);
    }

    private async Task<byte[]> Download(JsonElement payload, CancellationToken cancellationToken)
    {
        string url = payload.GetProperty("url").GetString()!;
        string expected = payload.GetProperty("sha256").GetString()!;
        byte[] bytes = await client.GetByteArrayAsync(url, cancellationToken);
        if (!Convert.ToHexStringLower(SHA256.HashData(bytes)).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Microsoft payload checksum mismatch: {payload.GetProperty("fileName").GetString()}");
        return bytes;
    }
}
