using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Vcruntime.Runners.Utils.Abstract;

/// <summary>Prepares the latest Windows x64 retail CRT from Microsoft's configured Visual Studio channel.</summary>
public interface IFileOperationsUtil
{
    /// <summary>Verifies Microsoft payload checksums and writes retail DLLs, notices, and provenance under win-x64/vcruntime.</summary>
    /// <param name="outputDirectory">An empty destination directory owned by the caller.</param>
    /// <param name="cancellationToken">Cancels downloads and file operations.</param>
    ValueTask Process(string outputDirectory, CancellationToken cancellationToken = default);
}

