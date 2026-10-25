using System;
using UnityEngine;

namespace BookBench {

    /// <summary>Глава 9: математика (управляемый код, UnityEngine.Vector3).</summary>
    public static class Ch09Math {

        private const string Chapter = "9";
        private const int Count = 1 << 20;

        public static void Run() {
            var rnd = new System.Random(20);
            var a = new Vector3[Count];
            var b = new Vector3[Count];
            for (int i = 0; i < Count; ++i) {
                a[i] = new Vector3((float)rnd.NextDouble() * 100f, (float)rnd.NextDouble() * 100f, (float)rnd.NextDouble() * 100f);
                b[i] = new Vector3((float)rnd.NextDouble() * 100f, (float)rnd.NextDouble() * 100f, (float)rnd.NextDouble() * 100f);
            }
            RunDistance(a, b);
            RunDivision(a);
            RunVectorScalarOrder(a);
            RunMagnitudeNormalized(a);
            RunArrays2D();
        }

        private static void RunDistance(Vector3[] a, Vector3[] b) {
            const float radius = 50f;
            Bench.Measure("ch9.distance", Chapter, "Vector3.Distance(a, b) < r (1M pairs)", () => {
                var count = 0;
                for (int i = 0; i < a.Length; ++i) {
                    if (Vector3.Distance(a[i], b[i]) < radius) ++count;
                }
                Bench.SinkInt = count;
            }, 3, 15, Count);
            Bench.Measure("ch9.distance", Chapter, "(a - b).sqrMagnitude < r * r (1M pairs)", () => {
                var count = 0;
                const float radiusSq = radius * radius;
                for (int i = 0; i < a.Length; ++i) {
                    if ((a[i] - b[i]).sqrMagnitude < radiusSq) ++count;
                }
                Bench.SinkInt = count;
            }, 3, 15, Count);
            Bench.Measure("ch9.distance", Chapter, "manual dx*dx+dy*dy+dz*dz < r * r (1M pairs)", () => {
                var count = 0;
                const float radiusSq = radius * radius;
                for (int i = 0; i < a.Length; ++i) {
                    var dx = a[i].x - b[i].x;
                    var dy = a[i].y - b[i].y;
                    var dz = a[i].z - b[i].z;
                    if (dx * dx + dy * dy + dz * dz < radiusSq) ++count;
                }
                Bench.SinkInt = count;
            }, 3, 15, Count);
        }

        private static float divisor = 3f;

        private static void RunDivision(Vector3[] a) {
            Bench.Measure("ch9.div.const", Chapter, "x / 2f (1M)", () => {
                var sum = 0f;
                for (int i = 0; i < a.Length; ++i) {
                    sum += a[i].x / 2f;
                }
                Bench.SinkFloat = sum;
            }, 3, 15, Count);
            Bench.Measure("ch9.div.const", Chapter, "x * 0.5f (1M)", () => {
                var sum = 0f;
                for (int i = 0; i < a.Length; ++i) {
                    sum += a[i].x * 0.5f;
                }
                Bench.SinkFloat = sum;
            }, 3, 15, Count);
            // Независимые деления, без цепочки через sum: видна пропускная способность div
            var output = new float[Count];
            Bench.Measure("ch9.div.var", Chapter, "out[i] = x / d (variable, 1M)", () => {
                var d = divisor;
                for (int i = 0; i < a.Length; ++i) {
                    output[i] = a[i].x / d;
                }
                Bench.SinkFloat = output[17];
            }, 3, 15, Count);
            Bench.Measure("ch9.div.var", Chapter, "out[i] = x * inv (inv = 1f / d precomputed, 1M)", () => {
                var inv = 1f / divisor;
                for (int i = 0; i < a.Length; ++i) {
                    output[i] = a[i].x * inv;
                }
                Bench.SinkFloat = output[17];
            }, 3, 15, Count);
        }

        private static void RunVectorScalarOrder(Vector3[] a) {
            var output = new Vector3[Count];
            var dt = 0.016f;
            Bench.Measure("ch9.vecscalar", Chapter, "dir * dt * 2f (two Vector3 * float)", () => {
                for (int i = 0; i < a.Length; ++i) {
                    output[i] = a[i] * dt * 2f;
                }
                Bench.SinkFloat = output[3].x;
            }, 3, 15, Count);
            Bench.Measure("ch9.vecscalar", Chapter, "dir * (dt * 2f) (one Vector3 * float)", () => {
                for (int i = 0; i < a.Length; ++i) {
                    output[i] = a[i] * (dt * 2f);
                }
                Bench.SinkFloat = output[3].x;
            }, 3, 15, Count);
        }

        private static void RunMagnitudeNormalized(Vector3[] a) {
            var output = new Vector3[Count];
            var lengths = new float[Count];
            Bench.Measure("ch9.magnorm", Chapter, "len = v.magnitude; dir = v.normalized", () => {
                for (int i = 0; i < a.Length; ++i) {
                    var v = a[i];
                    lengths[i] = v.magnitude;
                    output[i] = v.normalized;
                }
                Bench.SinkFloat = output[3].x + lengths[3];
            }, 3, 15, Count);
            Bench.Measure("ch9.magnorm", Chapter, "len = v.magnitude; dir = v / len (one sqrt)", () => {
                for (int i = 0; i < a.Length; ++i) {
                    var v = a[i];
                    var len = v.magnitude;
                    lengths[i] = len;
                    output[i] = len > 1e-5f ? v / len : Vector3.zero;
                }
                Bench.SinkFloat = output[3].x + lengths[3];
            }, 3, 15, Count);
            Bench.Measure("ch9.magnorm", Chapter, "len = sqrt(sqrMag); dir = v * (1 / len)", () => {
                for (int i = 0; i < a.Length; ++i) {
                    var v = a[i];
                    var len = Mathf.Sqrt(v.sqrMagnitude);
                    lengths[i] = len;
                    output[i] = len > 1e-5f ? v * (1f / len) : Vector3.zero;
                }
                Bench.SinkFloat = output[3].x + lengths[3];
            }, 3, 15, Count);
        }

        private const int Side = 1024;

        private static void RunArrays2D() {
            var multi = new int[Side, Side];
            var flat = new int[Side * Side];
            var rnd = new System.Random(21);
            for (int y = 0; y < Side; ++y) {
                for (int x = 0; x < Side; ++x) {
                    var v = rnd.Next(100);
                    multi[y, x] = v;
                    flat[y * Side + x] = v;
                }
            }
            Bench.Measure("ch9.array2d", Chapter, "int[,] m[y, x] sum 1024x1024", () => {
                long sum = 0;
                for (int y = 0; y < Side; ++y) {
                    for (int x = 0; x < Side; ++x) {
                        sum += multi[y, x];
                    }
                }
                Bench.SinkLong = sum;
            }, 3, 15, Side * Side);
            Bench.Measure("ch9.array2d", Chapter, "int[] flat[y * w + x] sum 1024x1024", () => {
                long sum = 0;
                for (int y = 0; y < Side; ++y) {
                    var row = y * Side;
                    for (int x = 0; x < Side; ++x) {
                        sum += flat[row + x];
                    }
                }
                Bench.SinkLong = sum;
            }, 3, 15, Side * Side);
            Bench.Measure("ch9.array2d.write", Chapter, "int[,] m[y, x] = v 1024x1024", () => {
                for (int y = 0; y < Side; ++y) {
                    for (int x = 0; x < Side; ++x) {
                        multi[y, x] = x ^ y;
                    }
                }
                Bench.SinkInt = multi[5, 5];
            }, 3, 15, Side * Side);
            Bench.Measure("ch9.array2d.write", Chapter, "int[] flat[y * w + x] = v 1024x1024", () => {
                for (int y = 0; y < Side; ++y) {
                    var row = y * Side;
                    for (int x = 0; x < Side; ++x) {
                        flat[row + x] = x ^ y;
                    }
                }
                Bench.SinkInt = flat[5 * Side + 5];
            }, 3, 15, Side * Side);

            RunArrays2DExtra(multi, flat);
            multi = null;
            flat = null;
            RunArraysScale();
        }

        private const string JaggedNote = "int[][]: each row is a separate heap object, m[y][x] = load row ref + row bounds check + x bounds check";
        private const string ColumnNote = "column order: x outer, y inner -> stride 4 KB per access (1024 ints), a new cache line on every read";

        /// <summary>
        /// Дополнительно к книжным вариантам: зубчатый int[][] (книга: «зубчатый [][] дороже обоих»)
        /// и обход по столбцам (x снаружи, y внутри) для всех трех раскладок.
        /// Данные те же, что у int[,] и int[]; существующие варианты выше не меняются.
        /// </summary>
        private static void RunArrays2DExtra(int[,] multi, int[] flat) {
            var jagged = new int[Side][];
            for (int y = 0; y < Side; ++y) {
                jagged[y] = new int[Side];
                for (int x = 0; x < Side; ++x) {
                    jagged[y][x] = multi[y, x];
                }
            }

            // ----- чтение, по строкам (как в книге) -----
            var r = Bench.Measure("ch9.array2d", Chapter, "int[][] jagged m[y][x] sum 1024x1024", () => {
                long sum = 0;
                for (int y = 0; y < Side; ++y) {
                    for (int x = 0; x < Side; ++x) {
                        sum += jagged[y][x];
                    }
                }
                Bench.SinkLong = sum;
            }, 3, 15, Side * Side);
            r.notes = JaggedNote;

            // ----- чтение, по столбцам -----
            r = Bench.Measure("ch9.array2d", Chapter, "int[,] m[y, x] sum, column order (x outer, y inner)", () => {
                long sum = 0;
                for (int x = 0; x < Side; ++x) {
                    for (int y = 0; y < Side; ++y) {
                        sum += multi[y, x];
                    }
                }
                Bench.SinkLong = sum;
            }, 3, 15, Side * Side);
            r.notes = ColumnNote;
            r = Bench.Measure("ch9.array2d", Chapter, "int[] flat[y * w + x] sum, column order (x outer, y inner)", () => {
                long sum = 0;
                for (int x = 0; x < Side; ++x) {
                    for (int y = 0; y < Side; ++y) {
                        sum += flat[y * Side + x];
                    }
                }
                Bench.SinkLong = sum;
            }, 3, 15, Side * Side);
            r.notes = ColumnNote;
            r = Bench.Measure("ch9.array2d", Chapter, "int[][] jagged m[y][x] sum, column order (x outer, y inner)", () => {
                long sum = 0;
                for (int x = 0; x < Side; ++x) {
                    for (int y = 0; y < Side; ++y) {
                        sum += jagged[y][x];
                    }
                }
                Bench.SinkLong = sum;
            }, 3, 15, Side * Side);
            r.notes = ColumnNote + "; rows are separate objects, so also a row-reference load per access";

            // ----- запись, по строкам -----
            r = Bench.Measure("ch9.array2d.write", Chapter, "int[][] jagged m[y][x] = v 1024x1024", () => {
                for (int y = 0; y < Side; ++y) {
                    for (int x = 0; x < Side; ++x) {
                        jagged[y][x] = x ^ y;
                    }
                }
                Bench.SinkInt = jagged[5][5];
            }, 3, 15, Side * Side);
            r.notes = JaggedNote;

            // ----- запись, по столбцам -----
            r = Bench.Measure("ch9.array2d.write", Chapter, "int[,] m[y, x] = v, column order (x outer, y inner)", () => {
                for (int x = 0; x < Side; ++x) {
                    for (int y = 0; y < Side; ++y) {
                        multi[y, x] = x ^ y;
                    }
                }
                Bench.SinkInt = multi[5, 5];
            }, 3, 15, Side * Side);
            r.notes = ColumnNote;
            r = Bench.Measure("ch9.array2d.write", Chapter, "int[] flat[y * w + x] = v, column order (x outer, y inner)", () => {
                for (int x = 0; x < Side; ++x) {
                    for (int y = 0; y < Side; ++y) {
                        flat[y * Side + x] = x ^ y;
                    }
                }
                Bench.SinkInt = flat[5 * Side + 5];
            }, 3, 15, Side * Side);
            r.notes = ColumnNote;
            r = Bench.Measure("ch9.array2d.write", Chapter, "int[][] jagged m[y][x] = v, column order (x outer, y inner)", () => {
                for (int x = 0; x < Side; ++x) {
                    for (int y = 0; y < Side; ++y) {
                        jagged[y][x] = x ^ y;
                    }
                }
                Bench.SinkInt = jagged[5][5];
            }, 3, 15, Side * Side);
            r.notes = ColumnNote + "; rows are separate objects, so also a row-reference load per access";
        }
        /// <summary>
        /// Размер имеет значение: 1024x1024 int = 4 МБ и целиком помещается в L2 (у M3 Ultra 16 МБ на кластер).
        /// Поэтому те же обходы на 4096x4096 (64 МБ, больше L2) и 8192x8192 (256 МБ, больше любого кэша, идет в RAM).
        /// </summary>
        private static void RunArraysScale() {
            var sides = new[] { 1024, 4096, 8192 };
            foreach (var side in sides) {
                System.GC.Collect();
                var n = side;
                var mb = (long)n * n * 4 / (1024 * 1024);
                var multi = new int[n, n];
                var flat = new int[n * n];
                var jagged = new int[n][];
                for (int y = 0; y < n; ++y) {
                    jagged[y] = new int[n];
                    for (int x = 0; x < n; ++x) {
                        var v = (x * 31 + y * 17) & 127;
                        multi[y, x] = v;
                        flat[y * n + x] = v;
                        jagged[y][x] = v;
                    }
                }
                var warm = n >= 4096 ? 1 : 3;
                var samples = n >= 4096 ? 7 : 15;
                var size = n + "x" + n + " (" + mb + " MB)";
                var note = "ns per element; " + size + "; L2 on M3 Ultra = 16 MB per cluster";

                var r = Bench.Measure("ch9.array2d.scale", Chapter, "rows: int[,] " + size, () => {
                    long sum = 0;
                    for (int y = 0; y < n; ++y) {
                        for (int x = 0; x < n; ++x) {
                            sum += multi[y, x];
                        }
                    }
                    Bench.SinkLong = sum;
                }, warm, samples, n * n);
                r.notes = note;
                r = Bench.Measure("ch9.array2d.scale", Chapter, "rows: int[] flat " + size, () => {
                    long sum = 0;
                    for (int y = 0; y < n; ++y) {
                        var row = y * n;
                        for (int x = 0; x < n; ++x) {
                            sum += flat[row + x];
                        }
                    }
                    Bench.SinkLong = sum;
                }, warm, samples, n * n);
                r.notes = note;
                r = Bench.Measure("ch9.array2d.scale", Chapter, "rows: int[][] " + size, () => {
                    long sum = 0;
                    for (int y = 0; y < n; ++y) {
                        var row = jagged[y];
                        for (int x = 0; x < n; ++x) {
                            sum += row[x];
                        }
                    }
                    Bench.SinkLong = sum;
                }, warm, samples, n * n);
                r.notes = note + "; row reference hoisted (var row = m[y]), as people usually write it";
                r = Bench.Measure("ch9.array2d.scale", Chapter, "rows: int[][] m[y][x] " + size, () => {
                    long sum = 0;
                    for (int y = 0; y < n; ++y) {
                        for (int x = 0; x < n; ++x) {
                            sum += jagged[y][x];
                        }
                    }
                    Bench.SinkLong = sum;
                }, warm, samples, n * n);
                r.notes = note + "; m[y][x] in the inner loop";

                r = Bench.Measure("ch9.array2d.scale", Chapter, "columns: int[,] " + size, () => {
                    long sum = 0;
                    for (int x = 0; x < n; ++x) {
                        for (int y = 0; y < n; ++y) {
                            sum += multi[y, x];
                        }
                    }
                    Bench.SinkLong = sum;
                }, warm, samples, n * n);
                r.notes = note + "; column order (x outer, y inner)";
                r = Bench.Measure("ch9.array2d.scale", Chapter, "columns: int[] flat " + size, () => {
                    long sum = 0;
                    for (int x = 0; x < n; ++x) {
                        for (int y = 0; y < n; ++y) {
                            sum += flat[y * n + x];
                        }
                    }
                    Bench.SinkLong = sum;
                }, warm, samples, n * n);
                r.notes = note + "; column order (x outer, y inner)";
                r = Bench.Measure("ch9.array2d.scale", Chapter, "columns: int[][] " + size, () => {
                    long sum = 0;
                    for (int x = 0; x < n; ++x) {
                        for (int y = 0; y < n; ++y) {
                            sum += jagged[y][x];
                        }
                    }
                    Bench.SinkLong = sum;
                }, warm, samples, n * n);
                r.notes = note + "; column order (x outer, y inner); rows are separate heap objects";

                multi = null;
                flat = null;
                jagged = null;
            }
            System.GC.Collect();
        }
    }
}
