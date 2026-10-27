# Trip engine performance

Synthetic national network (`NationalWorld`): 20 atolls, ~190 islands, 14 airports. 400 random island-to-island searches (both time modes, all tolerances), Release build, after warm-up. Includes snapshot indexing (cached), reliability lookup, search, assembly, ranking and backup-option searches.

| Density | Runs (3 days) | p50 | p95 | p99 | max |
|---|---|---|---|---|---|
| 1 (about the real national size) | 4 744 | 6.6 ms | 40.2 ms | 67.2 ms | 106 ms |
| 3 (3x departures) | 14 838 | 45.0 ms | 177.2 ms | 212.6 ms | 228 ms |

Target p95 < 300 ms: met at both densities. Reproduce: `dotnet run -c Release --project apps/api/tests/Dhathuru.TripEngine.Benchmarks -- p95` (BenchmarkDotNet suite: `-- --filter "*"`). The first search after a cold start costs about 300 ms of JIT. Backup searches are capped at 120 ms (`AlternativeSearchBudgetMs`).
