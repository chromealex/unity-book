using System.Collections.Generic;
using UnityEngine;

namespace UnityBook.Triangulation {

    /// <summary>
    /// Триангуляция простого многоугольника методом отрезания ушей (ear clipping).
    /// Контур может быть выпуклым или вогнутым, с обходом по или против часовой стрелки.
    /// Самопересечения и дыры не поддерживаются. Сложность O(n²).
    /// </summary>
    public static class Triangulator {

        /// <summary>
        /// Возвращает индексы треугольников (по три на треугольник) в порядке обхода против часовой стрелки.
        /// Вершины, лежащие на одной прямой с соседями, выбрасываются без вырожденных треугольников.
        /// </summary>
        public static int[] Triangulate(IReadOnlyList<Vector2> points) {
            var count = points.Count;
            if (count < 3) {
                return System.Array.Empty<int>();
            }

            // Список ещё не отрезанных вершин, всегда против часовой стрелки:
            // тогда «выпуклая вершина» везде означает положительное векторное произведение.
            var indices = new List<int>(count);
            if (SignedArea(points) >= 0f) {
                for (int i = 0; i < count; ++i) {
                    indices.Add(i);
                }
            } else {
                for (int i = count - 1; i >= 0; --i) {
                    indices.Add(i);
                }
            }

            var result = new List<int>((count - 2) * 3);
            var current = 0;
            var misses = 0;
            while (indices.Count > 3) {
                var n = indices.Count;
                if (current >= n) {
                    current = 0;
                }
                var a = indices[(current + n - 1) % n];
                var b = indices[current];
                var c = indices[(current + 1) % n];
                var cross = Cross(points[a], points[b], points[c]);

                if (Mathf.Abs(cross) <= EPSILON) {
                    // b лежит на прямой ac: треугольник нулевой площади не нужен, просто убираем вершину
                    indices.RemoveAt(current);
                    misses = 0;
                    continue;
                }

                if (cross > 0f && ContainsOtherVertex(points, indices, a, b, c) == false) {
                    // b — «ухо»: отрезаем треугольник abc, контур становится на вершину короче
                    result.Add(a);
                    result.Add(b);
                    result.Add(c);
                    indices.RemoveAt(current);
                    misses = 0;
                    continue;
                }

                ++current;
                ++misses;
                if (misses > n) {
                    // Обошли весь контур и не нашли ни одного уха: контур самопересекается
                    // или задан неправильно. Возвращаем то, что успели построить.
                    return result.ToArray();
                }
            }

            if (Mathf.Abs(Cross(points[indices[0]], points[indices[1]], points[indices[2]])) > EPSILON) {
                result.Add(indices[0]);
                result.Add(indices[1]);
                result.Add(indices[2]);
            }
            return result.ToArray();
        }

        /// <summary>
        /// Строит плоский меш в плоскости XY: вершины — точки контура, индексы — результат Triangulate.
        /// </summary>
        public static Mesh BuildMesh(IReadOnlyList<Vector2> points) {
            var vertices = new Vector3[points.Count];
            for (int i = 0; i < points.Count; ++i) {
                vertices[i] = new Vector3(points[i].x, points[i].y, 0f);
            }
            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = Triangulate(points);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private const float EPSILON = 1e-6f;

        // Удвоенная площадь со знаком (формула шнурков): > 0 — обход против часовой стрелки.
        private static float SignedArea(IReadOnlyList<Vector2> points) {
            var area = 0f;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++) {
                area += points[j].x * points[i].y - points[i].x * points[j].y;
            }
            return area;
        }

        // Векторное произведение (b - a) × (c - b): > 0 — поворот налево, вершина b выпуклая.
        private static float Cross(Vector2 a, Vector2 b, Vector2 c) {
            return (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
        }

        // Есть ли внутри треугольника abc (или на его границе) другая вершина контура.
        // Если есть, отрезать abc нельзя: треугольник накроет часть контура.
        private static bool ContainsOtherVertex(IReadOnlyList<Vector2> points, List<int> indices, int a, int b, int c) {
            var pa = points[a];
            var pb = points[b];
            var pc = points[c];
            for (int i = 0; i < indices.Count; ++i) {
                var index = indices[i];
                if (index == a || index == b || index == c) {
                    continue;
                }
                var p = points[index];
                // Совпадающие точки (например, повтор первой точки в конце) не мешают
                if (p == pa || p == pb || p == pc) {
                    continue;
                }
                if (Cross(pa, pb, p) >= 0f && Cross(pb, pc, p) >= 0f && Cross(pc, pa, p) >= 0f) {
                    return true;
                }
            }
            return false;
        }

    }

}
