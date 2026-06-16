using System.Diagnostics;
using BackEnd.Options;
using Microsoft.Extensions.Options;

namespace BackEnd.Services;

public class GdalRunner(IOptions<GdalOptions> opts, ILogger<GdalRunner> logger)
{
    private readonly GdalOptions _opts = opts.Value;

    public async Task<(int ExitCode, string StdErr)> RunAsync(
        string exe, string[] args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_opts.TimeoutSeconds));

        var stderr = await process.StandardError.ReadToEndAsync(cts.Token);
        await process.WaitForExitAsync(cts.Token);

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{exe} 執行超時（{_opts.TimeoutSeconds}s）");
        }

        logger.LogDebug("{Exe} exited with code {Code}. stderr={Stderr}", exe, process.ExitCode, stderr);
        return (process.ExitCode, stderr);
    }

    public async Task DxfToVectorAsync(string dxfPath, string outJson, CancellationToken ct = default)
    {
        var (code, err) = await RunAsync(_opts.Ogr2OgrPath,
            ["-f", "GeoJSON", outJson, dxfPath, "-sql", "SELECT * FROM entities"], ct);
        if (code != 0) throw new Exception($"ogr2ogr 失敗：{err}");
    }

    public async Task<(int Width, int Height)> VectorToGeoTiffAsync(
        string vectorPath, string outTiff, int outputWidth, CancellationToken ct = default)
    {
        // 先取 extent
        var (extCode, extOut) = await RunAsync("ogrinfo",
            ["-ro", "-al", "-so", vectorPath], ct);

        // 預設尺寸（無法取得 extent 時）
        int width = outputWidth;
        int height = outputWidth;

        var (code, err) = await RunAsync(_opts.GdalRasterizePath, [
            "-burn", "0", "-burn", "0", "-burn", "0",
            "-init", "255",
            "-ts", width.ToString(), height.ToString(),
            "-of", "GTiff",
            vectorPath, outTiff], ct);

        if (code != 0) throw new Exception($"gdal_rasterize 失敗：{err}");
        return (width, height);
    }

    public async Task GeoTiffToTilesAsync(
        string tiffPath, string outDir, int minZoom, int maxZoom, CancellationToken ct = default)
    {
        var (code, err) = await RunAsync(_opts.Gdal2TilesPath, [
            "--profile=raster",
            $"-z", $"{minZoom}-{maxZoom}",
            "-w", "none",
            tiffPath, outDir], ct);
        if (code != 0) throw new Exception($"gdal2tiles 失敗：{err}");
    }
}
