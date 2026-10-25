# BookBenchmarks results

| Key | Value |
|---|---|
| Unity | 6000.6.0f1 |
| Backend | IL2CPP |
| Editor | False |
| Burst enabled | True |
| Burst safety checks | True |
| Collections checks (managed) | False |
| Development build | False |
| CPU | Apple M3 Ultra x28 |
| RAM (MB) | 98304 |
| OS | Mac OS X 15.6.0 |
| Graphics | Null |
| Date | 2026-10-25 02:58:41 |

| id | variant | median | min | p90 | bytes/op | n | notes |
|---|---|---:|---:|---:|---:|---:|---|
| harness.alloc | nothing (expect 0) | - | - | - | 0 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| harness.alloc | new object() (expect 16-24) | - | - | - | 31.95 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| harness.alloc | new byte[1000] (expect ~1024-1040) | - | - | - | 1365.2 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| harness.empty | empty action (timer overhead) | - | - | 100 ns | - | 101 |  |
| ch1.params | params int[] (3 args) | 36.377 ns | 34.094 ns | 37.29 ns | - | 15 |  |
| ch1.params | overload (int,int,int) | 0.823 ns | 0.806 ns | 0.846 ns | - | 15 |  |
| ch1.params.alloc | params int[] (3 args) | - | - | - | 47.51 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch1.params.alloc | overload (int,int,int) | - | - | - | 0 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch1.newT | new T() (generic struct) | 275.214 ns | 271.6 ns | 277.059 ns | - | 15 |  |
| ch1.newT | default(T) (generic struct) | 0.823 ns | 0.806 ns | 0.849 ns | - | 15 |  |
| ch1.newT.alloc | new T() (generic struct) | - | - | - | 95.85 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch1.newT.alloc | default(T) (generic struct) | - | - | - | 0 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch1.closure | closure (captures local) | 125.434 ns | 121.091 ns | 128.637 ns | - | 15 |  |
| ch1.closure | static lambda + context arg | 1.715 ns | 1.615 ns | 1.772 ns | - | 15 |  |
| ch1.closure.alloc | closure (captures local) | - | - | - | 177.77 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch1.closure.alloc | static lambda + context arg | - | - | - | 0 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch1.boxing | struct passed as interface | 33.134 ns | 31.151 ns | 34.145 ns | - | 15 |  |
| ch1.boxing | generic where T : struct, IValue | 0.868 ns | 0.832 ns | 0.899 ns | - | 15 |  |
| ch1.boxing.alloc | struct passed as interface | - | - | - | 31.95 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch1.boxing.alloc | generic where T : struct, IValue | - | - | - | 0 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch1.foreach | foreach List<int> (1000 items) | 2.111 µs | 2.046 µs | 2.212 µs | - | 15 |  |
| ch1.foreach | foreach IEnumerable<int> (1000 items) | 3.74 µs | 3.623 µs | 3.828 µs | - | 15 |  |
| ch1.foreach.alloc | foreach List<int> | - | - | - | 0 | 1000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch1.foreach.alloc | foreach IEnumerable<int> | - | - | - | 45.06 | 1000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch1.shortcircuit | CalcV1() \|\| CalcV2() (V1 true 90%) | 32.003 ns | 30.965 ns | 32.896 ns | - | 15 |  |
| ch1.shortcircuit | v1 = CalcV1(); v2 = CalcV2(); v1 \|\| v2 | 289.075 ns | 282.271 ns | 293.826 ns | - | 15 |  |
| ch2.tostring | health.ToString() | 27.868 ns | 25.157 ns | 31.531 ns | - | 15 |  |
| ch2.tostring | cached string table | 1.013 ns | 1.01 ns | 1.131 ns | - | 15 |  |
| ch2.tostring.alloc | health.ToString() | - | - | - | 32.36 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch2.tostring.alloc | cached string table | - | - | - | 0 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch2.returnlist.alloc | return new List<int> (256 src) | - | - | - | 1380.35 | 1000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch2.returnlist.alloc | fill passed List<int> | - | - | - | 0 | 1000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch2.returnlist | return new List<int> (256 src) | 1.275 µs | 1.05 µs | 1.606 µs | - | 15 |  |
| ch2.returnlist | fill passed List<int> | 258.1 ns | 255.6 ns | 269.3 ns | - | 15 |  |
| ch2.emptyarray.alloc | new int[0] | - | - | - | 47.51 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch2.emptyarray.alloc | Array.Empty<int>() | - | - | - | 0 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch2.nodelayout | iterate nodes: allocated interleaved with child lists | 0.84 ns | 0.819 ns | 0.865 ns | - | 31 |  |
| ch2.nodelayout | iterate nodes: allocated in two passes | 0.532 ns | 0.53 ns | 0.533 ns | - | 31 |  |
| ch3.sizeof | MyStruct {int,byte,int,byte} default | - | - | - | 16 | 0 | bytesPerOp = size in bytes; sizeof=16 |
| ch3.sizeof | MyStruct reordered {int,int,byte,byte} | - | - | - | 12 | 0 | bytesPerOp = size in bytes; sizeof=12 |
| ch3.sizeof | MyStruct Pack=1 | - | - | - | 10 | 0 | bytesPerOp = size in bytes; sizeof=10 |
| ch3.sizeof | rts UnitAosBad | - | - | - | 44 | 0 | bytesPerOp = size in bytes; book says 44 |
| ch3.sizeof | rts UnitAos | - | - | - | 36 | 0 | bytesPerOp = size in bytes; book says 36 |
| ch3.memcpy | UnsafeUtility.MemCpy 1 MB | 12.922 µs | 12.252 µs | 14.038 µs | - | 21 |  |
| ch3.memcpy | UnsafeUtility.MemMove 1 MB (non-overlapping) | 12.554 µs | 12.272 µs | 13.006 µs | - | 21 |  |
| ch3.ptrwrite | *(int*)ptr = value | 0.134 ns | 0.134 ns | 0.136 ns | - | 21 |  |
| ch3.ptrwrite | byte-by-byte (shift & mask) | 0.265 ns | 0.265 ns | 0.274 ns | - | 21 |  |
| ch3.ptrwrite | BitConverter.TryWriteBytes(Span) | 1.365 ns | 1.323 ns | 1.443 ns | - | 21 |  |
| ch3.hasflag | Enum.HasFlag | 0.055 ns | 0.052 ns | 0.067 ns | - | 21 |  |
| ch3.hasflag | (flags & mask) != 0 | 0.055 ns | 0.05 ns | 0.067 ns | - | 21 |  |
| ch3.hasflag.alloc | Enum.HasFlag | - | - | - | 0 | 10000 | alloc via Profiler.GetMonoUsedSizeLong (GC disabled) |
| ch3.dictkey | Dictionary<long,int> (two ints packed) | 19.398 ns | 18.543 ns | 19.788 ns | - | 15 |  |
| ch3.dictkey | Dictionary<(int,int),int> | 26.42 ns | 25.204 ns | 27.726 ns | - | 15 |  |
| ch4.counter | Interlocked.Increment on shared int | 79.605 ns | 72.236 ns | 80.933 ns | - | 5 | threads=28, ns per increment/operation (total wall time / total ops) |
| ch4.counter | per-thread int[] slots, adjacent (false sharing) | 1.032 ns | 0.993 ns | 1.107 ns | - | 5 | threads=28, ns per increment/operation (total wall time / total ops) |
| ch4.counter | per-thread slots padded to 128 bytes | 0.09 ns | 0.082 ns | 0.101 ns | - | 5 | threads=28, ns per increment/operation (total wall time / total ops) |
| ch4.counter | per-thread local variable, write once (reference) | 0.049 ns | 0.043 ns | 0.051 ns | - | 5 | threads=28, ns per increment/operation (total wall time / total ops) |
| ch4.lock | lock (Monitor) | 75.146 ns | 73.539 ns | 78.692 ns | - | 7 | threads=28, ns per increment/operation (total wall time / total ops) |
| ch4.lock | spin lock: busy Interlocked.Exchange | 2.152 µs | 2.072 µs | 2.23 µs | - | 7 | threads=28, ns per increment/operation (total wall time / total ops) |
| ch4.lock | spin lock: Interlocked.Exchange + SpinWait | 8.12 ns | 5.746 ns | 9.925 ns | - | 7 | threads=28, ns per increment/operation (total wall time / total ops) |
| ch4.lock | System.Threading.SpinLock (reference) | 30.613 ns | 28.608 ns | 32.519 ns | - | 7 | threads=28, ns per increment/operation (total wall time / total ops) |
| ch5.v3v4.copy | copy NativeArray<V3{float3}> 1M | 0.235 ns | 0.231 ns | 0.247 ns | - | 21 |  |
| ch5.v3v4.copy | copy NativeArray<V4{float4}> 1M | 0.3 ns | 0.242 ns | 0.327 ns | - | 21 |  |
| ch5.v3v4.scale | v * k + 1 over V3{float3} 1M | 0.524 ns | 0.48 ns | 0.544 ns | - | 21 |  |
| ch5.v3v4.scale | v * k + 1 over V4{float4} 1M | 0.3 ns | 0.271 ns | 0.354 ns | - | 21 |  |
| ch5.looporder | for job(4096) { for k(128) } sequential | 0.294 ns | 0.28 ns | 0.323 ns | - | 21 |  |
| ch5.looporder | for k(128) { for job(4096) } strided | 0.571 ns | 0.537 ns | 0.657 ns | - | 21 |  |
| ch5.noalias | int* params without [NoAlias] (1024-chunk calls) | 0.062 ns | 0.055 ns | 0.1 ns | - | 21 |  |
| ch5.noalias | int* params with [NoAlias] (1024-chunk calls) | 0.061 ns | 0.056 ns | 0.071 ns | - | 21 |  |
| ch5.mad | math.mad(a, b, c) float4 x 256K (throughput) | 0.629 ns | 0.604 ns | 0.674 ns | - | 21 |  |
| ch5.mad | a * b + c float4 x 256K (throughput) | 0.583 ns | 0.533 ns | 0.62 ns | - | 21 |  |
| ch5.mad.chain | acc = math.mad(acc, k, a[i]) (latency chain) | 2 ns | 1.86 ns | 2.071 ns | - | 21 |  |
| ch5.mad.chain | acc = acc * k + a[i] (latency chain) | 1.972 ns | 1.852 ns | 2.148 ns | - | 21 |  |
| ch5.clamp | if/else clamp 1M (random, 50% clipped) | 0.119 ns | 0.118 ns | 0.127 ns | - | 21 |  |
| ch5.clamp | math.min(math.max()) 1M | 0.071 ns | 0.071 ns | 0.071 ns | - | 21 |  |
| ch5.clamp | math.select 1M | 0.074 ns | 0.071 ns | 0.076 ns | - | 21 |  |
| ch5.clamp.count | if/else clamp + count clipped | 3.614 ns | 3.412 ns | 3.858 ns | - | 21 |  |
| ch5.clamp.count | math.clamp + math.select count | 0.087 ns | 0.084 ns | 0.095 ns | - | 21 |  |
| ch5.bitscan.mixed | naive per-bit loop (1/3 empty, 1/3 full, 1/3 random words) | 11.545 ns | 10.945 ns | 12.213 ns | - | 21 |  |
| ch5.bitscan.mixed | tzcnt loop + 0/full shortcuts (1/3 empty, 1/3 full, 1/3 random words) | 6.719 ns | 6.368 ns | 7.947 ns | - | 21 |  |
| ch5.bitscan.dense50 | naive per-bit loop (50% density) | 11.486 ns | 10.939 ns | 11.797 ns | - | 21 |  |
| ch5.bitscan.dense50 | tzcnt loop (50% density) | 24.154 ns | 23.25 ns | 24.73 ns | - | 21 |  |
| ch5.bitscan.sparse3 | naive per-bit loop (~3% density) | 11.474 ns | 11.006 ns | 12.007 ns | - | 21 |  |
| ch5.bitscan.sparse3 | tzcnt loop (~3% density) | 1.361 ns | 1.353 ns | 1.473 ns | - | 21 |  |
| ch5.list | NativeList<int> indexer sum 1M | 0.075 ns | 0.069 ns | 0.078 ns | - | 21 |  |
| ch5.list | UnsafeList<int> indexer sum 1M | 0.07 ns | 0.069 ns | 0.076 ns | - | 21 |  |
| ch5.schedulebatched | 64 small jobs: ScheduleBatchedJobs after each Schedule | 243.6 µs | 181.3 µs | 277.8 µs | - | 41 |  |
| ch5.schedulebatched | 64 small jobs: one ScheduleBatchedJobs after all | 67.3 µs | 45.5 µs | 83.5 µs | - | 41 |  |
| ch5.schedulebatched | 64 small jobs: no ScheduleBatchedJobs, CompleteAll only | 71.2 µs | 46.5 µs | 89 µs | - | 41 |  |
| ch9.distance | Vector3.Distance(a, b) < r (1M pairs) | 1.216 ns | 1.188 ns | 1.26 ns | - | 15 |  |
| ch9.distance | (a - b).sqrMagnitude < r * r (1M pairs) | 0.363 ns | 0.35 ns | 0.381 ns | - | 15 |  |
| ch9.distance | manual dx*dx+dy*dy+dz*dz < r * r (1M pairs) | 0.355 ns | 0.349 ns | 0.375 ns | - | 15 |  |
| ch9.div.const | x / 2f (1M) | 0.786 ns | 0.759 ns | 0.832 ns | - | 15 |  |
| ch9.div.const | x * 0.5f (1M) | 0.798 ns | 0.782 ns | 0.824 ns | - | 15 |  |
| ch9.div.var | out[i] = x / d (variable, 1M) | 0.16 ns | 0.154 ns | 0.175 ns | - | 15 |  |
| ch9.div.var | out[i] = x * inv (inv = 1f / d precomputed, 1M) | 0.143 ns | 0.131 ns | 0.21 ns | - | 15 |  |
| ch9.vecscalar | dir * dt * 2f (two Vector3 * float) | 0.27 ns | 0.255 ns | 0.341 ns | - | 15 |  |
| ch9.vecscalar | dir * (dt * 2f) (one Vector3 * float) | 0.27 ns | 0.256 ns | 0.29 ns | - | 15 |  |
| ch9.magnorm | len = v.magnitude; dir = v.normalized | 1.841 ns | 1.76 ns | 1.929 ns | - | 15 |  |
| ch9.magnorm | len = v.magnitude; dir = v / len (one sqrt) | 1.855 ns | 1.77 ns | 1.921 ns | - | 15 |  |
| ch9.magnorm | len = sqrt(sqrMag); dir = v * (1 / len) | 1.804 ns | 1.735 ns | 1.864 ns | - | 15 |  |
| ch9.array2d | int[,] m[y, x] sum 1024x1024 | 0.046 ns | 0.043 ns | 0.047 ns | - | 15 |  |
| ch9.array2d | int[] flat[y * w + x] sum 1024x1024 | 0.048 ns | 0.044 ns | 0.051 ns | - | 15 |  |
| ch9.array2d.write | int[,] m[y, x] = v 1024x1024 | 0.093 ns | 0.093 ns | 0.102 ns | - | 15 |  |
| ch9.array2d.write | int[] flat[y * w + x] = v 1024x1024 | 0.098 ns | 0.093 ns | 0.114 ns | - | 15 |  |
| ch10.contains | List<int>.Contains (1000 items, 50% hit) | 639.564 ns | 634.462 ns | 654.831 ns | - | 11 |  |
| ch10.contains | HashSet<int>.Contains (1000 items, 50% hit) | 9.758 ns | 9.54 ns | 10.535 ns | - | 11 |  |
| ch10.sort | Array.Copy only (baseline, subtract) | 0.065 ns | 0.048 ns | 0.072 ns | - | 9 |  |
| ch10.sort | Array.Sort uint[] 1M | 78.989 ns | 78.338 ns | 80.298 ns | - | 9 |  |
| ch10.sort | LSD radix sort (base 256) uint[] 1M | 4.253 ns | 4.044 ns | 4.349 ns | - | 9 |  |
| ch10.dfs | recursive DFS (100k nodes) | 42.297 ns | 30.607 ns | 46.794 ns | - | 21 | tree depth = 3102 |
| ch10.dfs | explicit Stack<T> DFS (100k nodes, reused stack) | 43.655 ns | 28.538 ns | 48.739 ns | - | 21 | tree depth = 3102 |
| ch5.transformroots | 10 000 transforms at scene root | 16.99 ns | 14.45 ns | 20.48 ns | - | 31 | Schedule(TransformAccessArray).Complete(), ns per transform |
| ch5.transformroots | 10 000 transforms under 40 roots x 256 | 10.75 ns | 6.29 ns | 13.47 ns | - | 31 | Schedule(TransformAccessArray).Complete(), ns per transform |
| ch5.transformroots | 10 000 transforms under 1 parent | 31.78 ns | 29.15 ns | 33.23 ns | - | 31 | Schedule(TransformAccessArray).Complete(), ns per transform |
| rts.n1000 | step0 naive (MonoBehaviour.Update + LINQ) | 113.804 ms | 110.185 ms | 115.195 ms | 19986432 | 20 | units=1000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 24 frames: alive=1000, hpSum=99860, hpHash=4FA1E99D, posHash=D8E77F5D |
| rts.n1000 | step0 fixed (UnitManager, no alloc, still O(n^2)) | 34.604 ms | 32.178 ms | 35.308 ms | 0 | 60 | units=1000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 70 frames: alive=1000, hpSum=99620, hpHash=CFDD935D, posHash=8FAB3C83 |
| rts.n1000 | step1 data arrays + grid | 94.8 µs | 86.6 µs | 102.7 µs | 0 | 120 | units=1000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 140 frames: alive=1000, hpSum=99200, hpHash=EA01157D, posHash=3BAE5819 |
| rts.n1000 | step2 threads + double buffer (processorCount-2 workers) | 274.55 µs | 200.4 µs | 307.6 µs | 0 | 120 | units=1000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 140 frames: alive=1000, hpSum=99200, hpHash=EA01157D, posHash=59D51EDE |
| rts.n1000 | step2 threads + double buffer (0 background workers) | 87.25 µs | 83.2 µs | 94.3 µs | 0 | 120 | units=1000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 140 frames: alive=1000, hpSum=99200, hpHash=EA01157D, posHash=59D51EDE; same hp as step2 (processorCount-2 workers): YES, same positions: YES |
| rts.n1000.step4cull | step4 CullAndBuildInstancesJob only (Schedule(n,128).Complete) | 86.1 µs | 47.8 µs | 117.8 µs | - | 51 | units=1000, visible=616, bars=30; ns per frame |
| rts.n1000 | step3 Jobs + Burst (+ IJobParallelForTransform) | 172.75 µs | 95.8 µs | 258.7 µs | 0 | 120 | units=1000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 140 frames: alive=1000, hpSum=99200, hpHash=EA01157D, posHash=59D51EDE; same hp as step2 (book: same algorithm, float bits may differ): YES, same positions: YES |
| rts.n1000 | step4 instanced rendering (full frame) | - | - | - | - | 0 | SKIPPED: no graphics device (-nographics); run in Editor or player without -nographics |
| rts.n10000 | step0 naive (MonoBehaviour.Update + LINQ) | 20718.453 ms | 20691.203 ms | 20745.703 ms | - | 2 | units=10000; ns per frame; bytesPerOp = bytes per frame (not measured); checksum after 2 frames: alive=10000, hpSum=998420, hpHash=03AC2525, posHash=FF81DC29 |
| rts.n10000 | step0 fixed (UnitManager, no alloc, still O(n^2)) | 8211.313 ms | 8203.532 ms | 8438.559 ms | 0 | 3 | units=10000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 5 frames: alive=10000, hpSum=998250, hpHash=2AD00A93, posHash=B13B1E52 |
| rts.n10000 | step1 data arrays + grid | 2.022 ms | 1.778 ms | 2.19 ms | 0 | 120 | units=10000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 140 frames: alive=9955, hpSum=982950, hpHash=5775EB41, posHash=A3AA005F |
| rts.n10000 | step2 threads + double buffer (processorCount-2 workers) | 820.75 µs | 638.4 µs | 924.1 µs | 0 | 120 | units=10000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 140 frames: alive=9952, hpSum=982970, hpHash=3EC15B33, posHash=78C24247 |
| rts.n10000 | step2 threads + double buffer (0 background workers) | 1.942 ms | 1.706 ms | 2.091 ms | 0 | 120 | units=10000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 140 frames: alive=9952, hpSum=982970, hpHash=3EC15B33, posHash=78C24247; same hp as step2 (processorCount-2 workers): YES, same positions: YES |
| rts.n10000.step4cull | step4 CullAndBuildInstancesJob only (Schedule(n,128).Complete) | 677.8 µs | 615.6 µs | 727 µs | - | 51 | units=10000, visible=5889, bars=289; ns per frame |
| rts.n10000 | step3 Jobs + Burst (+ IJobParallelForTransform) | 513.25 µs | 308.6 µs | 707.1 µs | 0 | 120 | units=10000; ns per frame; bytesPerOp = bytes per frame (Profiler.GetMonoUsedSizeLong (GC disabled)); checksum after 140 frames: alive=9952, hpSum=982970, hpHash=3EC15B33, posHash=78C24247; same hp as step2 (book: same algorithm, float bits may differ): YES, same positions: YES |
| rts.n10000 | step4 instanced rendering (full frame) | - | - | - | - | 0 | SKIPPED: no graphics device (-nographics); run in Editor or player without -nographics |
