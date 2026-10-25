using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnityBook.Triangulation.Tests {

    public class TriangulatorTests {

        [Test]
        public void Square_BothWindings() {
            Check(Points(0, 0, 1, 0, 1, 1, 0, 1));
            Check(Points(0, 0, 0, 1, 1, 1, 1, 0));
        }

        [Test]
        public void Concave_LShape() {
            Check(Points(0, 0, 2, 0, 2, 1, 1, 1, 1, 2, 0, 2));
        }

        [Test]
        public void Concave_Comb() {
            Check(Points(0, 0, 5, 0, 5, 3, 4, 3, 4, 1, 3, 1, 3, 3, 2, 3, 2, 1, 1, 1, 1, 3, 0, 3));
        }

        [Test]
        public void Collinear_PointsDoNotProduceDegenerateTriangles() {
            Check(Points(0, 0, 1, 0, 2, 0, 2, 2, 0, 2));
        }

        [Test]
        public void RandomStarShapedPolygons() {
            var random = new System.Random(1);
            for (int k = 0; k < 200; ++k) {
                var n = 3 + random.Next(40);
                var points = new List<Vector2>(n);
                for (int i = 0; i < n; ++i) {
                    var angle = 2f * Mathf.PI * i / n;
                    var radius = 0.3f + (float)random.NextDouble();
                    points.Add(new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
                }
                if (random.Next(2) == 0) {
                    points.Reverse();
                }
                Check(points);
            }
        }

        // Сумма площадей треугольников должна совпасть с площадью контура,
        // и каждый треугольник должен идти против часовой стрелки с ненулевой площадью.
        private static void Check(List<Vector2> points) {
            var triangles = Triangulator.Triangulate(points);
            Assert.AreEqual(0, triangles.Length % 3);
            var sum = 0.0;
            for (int i = 0; i < triangles.Length; i += 3) {
                var a = points[triangles[i]];
                var b = points[triangles[i + 1]];
                var c = points[triangles[i + 2]];
                var cross = (double)(b.x - a.x) * (c.y - a.y) - (double)(b.y - a.y) * (c.x - a.x);
                Assert.Greater(cross, 0.0);
                sum += cross * 0.5;
            }
            Assert.AreEqual(Area(points), sum, 1e-3);
        }

        private static double Area(List<Vector2> points) {
            var area = 0.0;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++) {
                area += (double)points[j].x * points[i].y - (double)points[i].x * points[j].y;
            }
            return System.Math.Abs(area) * 0.5;
        }

        private static List<Vector2> Points(params float[] xy) {
            var list = new List<Vector2>(xy.Length / 2);
            for (int i = 0; i < xy.Length; i += 2) {
                list.Add(new Vector2(xy[i], xy[i + 1]));
            }
            return list;
        }

    }

}
