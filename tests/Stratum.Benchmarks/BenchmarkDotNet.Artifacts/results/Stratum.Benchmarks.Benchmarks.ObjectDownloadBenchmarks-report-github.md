```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.8037)
Unknown processor
.NET SDK 10.0.200
  [Host] : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method            | count | Mean | Error |
|------------------ |------ |-----:|------:|
| ReadMultipleFiles | 100   |   NA |    NA |

Benchmarks with issues:
  ObjectDownloadBenchmarks.ReadMultipleFiles: .NET 8.0(Runtime=.NET 8.0) [count=100]
