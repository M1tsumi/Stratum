using BenchmarkDotNet.Running;
using Stratum.Benchmarks.Benchmarks;

BenchmarkRunner.Run<ObjectUploadBenchmarks>();
BenchmarkRunner.Run<ObjectDownloadBenchmarks>();
BenchmarkRunner.Run<MetadataQueryBenchmarks>();
BenchmarkRunner.Run<ConcurrentOperationsBenchmarks>();
