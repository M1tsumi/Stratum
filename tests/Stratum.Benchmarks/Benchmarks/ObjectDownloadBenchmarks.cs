using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Stratum.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class ObjectDownloadBenchmarks
{
    private string _dataDirectory = "./benchmark-data";
    private Dictionary<string, int> _testFiles = new();

    [GlobalSetup]
    public void Setup()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
        Directory.CreateDirectory(_dataDirectory);

        // Create test files of various sizes
        var sizes = new[] { 1024, 1024 * 1024, 10 * 1024 * 1024 };
        foreach (var size in sizes)
        {
            var data = new byte[size];
            Random.Shared.NextBytes(data);
            var key = $"test-{size}-{Guid.NewGuid()}";

            var filePath = Path.Combine(_dataDirectory, key);
            File.WriteAllBytes(filePath, data);
            _testFiles[key] = size;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
    }

    [IterationSetup(Target = nameof(ReadMultipleFiles))]
    public void SetupReadMultipleFiles()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
        Directory.CreateDirectory(_dataDirectory);
        _testFiles.Clear();

        // Pre-populate with files
        for (int i = 0; i < 10000; i++)
        {
            var data = new byte[1024];
            Random.Shared.NextBytes(data);
            var key = $"test-{i}";
            var filePath = Path.Combine(_dataDirectory, key);
            File.WriteAllBytes(filePath, data);
            _testFiles[key] = 1024;
        }
    }

    [IterationCleanup(Target = nameof(ReadMultipleFiles))]
    public void CleanupReadMultipleFiles()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, true);
        }
        _testFiles.Clear();
    }

    [Benchmark]
    [Arguments(1024)] // 1KB
    [Arguments(1024 * 1024)] // 1MB
    [Arguments(10 * 1024 * 1024)] // 10MB
    public async Task ReadFile(int size)
    {
        var key = _testFiles.FirstOrDefault(k => k.Value == size).Key;
        if (string.IsNullOrEmpty(key))
        {
            var data = new byte[size];
            Random.Shared.NextBytes(data);
            key = $"test-{size}-{Guid.NewGuid()}";

            var newPath = Path.Combine(_dataDirectory, key);
            File.WriteAllBytes(newPath, data);
            _testFiles[key] = size;
        }

        var readPath = Path.Combine(_dataDirectory, key);
        await File.ReadAllBytesAsync(readPath);
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task ReadMultipleFiles(int count)
    {
        var tasks = new List<Task>();

        for (int i = 0; i < count; i++)
        {
            var key = _testFiles.Keys.ElementAt(i % _testFiles.Count);
            tasks.Add(Task.Run(async () =>
            {
                var filePath = Path.Combine(_dataDirectory, key);
                await File.ReadAllBytesAsync(filePath);
            }));
        }

        await Task.WhenAll(tasks);
    }
}
