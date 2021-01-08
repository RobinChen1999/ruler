namespace PowerDiagram
{
    using System.Collections.Generic;
    using UnityEngine;
    using Util.Geometry;
    using Util.Geometry.Triangulation;

    /// <summary>
    /// Static class responsible for displaying voronoi graph and concepts.
    /// Draws the Voronoi graph, as well as edges of Delaunay triangulation and circumcircles of delaunay triangles.
    /// </summary>
    public static class PowerDiagramDrawer
    {
        // toggle variables for displaying circles, edges, and voronoi graph
        public static bool CircleOn { get; set; }
        public static bool EdgesOn { get; set; }
        public static bool VoronoiOn { get; set; }

        // line material for Unity shader
        private static Material m_lineMaterial;

        public static void CreateLineMaterial()
        {
            // Unity has a built-in shader that is useful for drawing
            // simple colored things.
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            m_lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            // Turn on alpha blending
            m_lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m_lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            // Turn backface culling off
            m_lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            // Turn off depth writes
            m_lineMaterial.SetInt("_ZWrite", 0);
        }

        /// <summary>
        /// Draw edges of the Delaunay triangulation
        /// </summary>
        /// <param name="m_Delaunay"></param>
        private static void DrawEdges(Triangulation m_Delaunay)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.green);

            foreach (var halfEdge in m_Delaunay.Edges)
            {
                // dont draw edges to outer vertices
                if (m_Delaunay.ContainsInitialPoint(halfEdge.T))
                {
                    continue;
                }

                // draw edge
                GL.Vertex3(halfEdge.Point1.x, 0, halfEdge.Point1.y);
                GL.Vertex3(halfEdge.Point2.x, 0, halfEdge.Point2.y);
            }

            GL.End();
        }

        /// <summary>
        /// Draws the circumcircles of the Delaunay triangles
        /// </summary>
        /// <param name="m_Delaunay"></param>
        private static void DrawCircles(Triangulation m_Delaunay)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.blue);

            //const float extra = (360 / 100);

            foreach (Triangle triangle in m_Delaunay.Triangles)
            {
                // dont draw circles for triangles to outer vertices
                if (m_Delaunay.ContainsInitialPoint(triangle) || triangle.Degenerate)
                {
                    continue;
                }

                var center = triangle.Circumcenter.Value;

                // find circle radius
                var radius = Vector2.Distance(center, triangle.P0);

                var prevA = 0f;
                for (var a = 0f; a <= 2 * Mathf.PI; a += 0.05f)
                {
                    //the circle.
                    GL.Vertex3(Mathf.Cos(prevA) * radius + center.x, 0, Mathf.Sin(prevA) * radius + center.y);
                    GL.Vertex3(Mathf.Cos(a) * radius + center.x, 0, Mathf.Sin(a) * radius + center.y);

                    //midpoint of the circle.
                    GL.Vertex3(Mathf.Cos(prevA) * 0.1f + center.x, 0, Mathf.Sin(prevA) * 0.1f + center.y);
                    GL.Vertex3(Mathf.Cos(a) * 0.1f + center.x, 0, Mathf.Sin(a) * 0.1f + center.y);

                    prevA = a;
                }
            }

            GL.End();
        }

        /// <summary>
        /// Draws the voronoi diagram related to delaunay triangulation
        /// </summary>
        /// <param name="m_Delaunay"></param>
        private static void DrawVoronoi(Triangulation m_Delaunay, Dictionary<Vector2, PowerDiagramController.DictionaryPair> m_ownership)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.black);

            foreach (var halfEdge in m_Delaunay.Edges)
            {
                // do not draw edges for outer triangles
                if (m_Delaunay.ContainsInitialPoint(halfEdge.T))
                {
                    continue;
                }

                // find relevant triangles to triangle edge
                Triangle t1 = halfEdge.T;
                Triangle t2 = halfEdge.Twin.T;

                if (t1 != null && !t1.Degenerate &&
                    t2 != null && !t2.Degenerate)
                {
                    float t1RadiusP0 = (float)m_ownership[t1.P0].Radius;
                    float t1RadiusP1 = (float)m_ownership[t1.P1].Radius;
                    float t1RadiusP2 = (float)m_ownership[t1.P2].Radius;

                    // Calc power line between P0 and P1 of t1
                    float t1DistanceP0P1 = (t1.P0 - t1.P1).magnitude;
                    float t1DistanceP0K1 = (t1DistanceP0P1 + (Mathf.Sqrt(t1RadiusP0) - Mathf.Sqrt(t1RadiusP1)) / t1DistanceP0P1) / 2;

                    Line t1LineP0P1 = new Line(t1.P0, t1.P1);
                    float t1AngleP0P1 = t1LineP0P1.Angle;

                    float t1AdjacentDistanceP0P1 = Mathf.Abs(Mathf.Cos(t1AngleP0P1) * t1DistanceP0K1);
                    float t1OppositeDistanceP0P1 = Mathf.Abs(Mathf.Sin(t1AngleP0P1) * t1DistanceP0K1);

                    t1AdjacentDistanceP0P1 *= t1.P1.x - t1.P0.x > 0 ? 1 : -1;
                    t1OppositeDistanceP0P1 *= t1.P1.y - t1.P0.y > 0 ? 1 : -1;

                    Vector2 t1K1 = new Vector2(t1.P0.x + t1AdjacentDistanceP0P1, t1.P0.y + t1OppositeDistanceP0P1);
                    Line t1PowerlineP0P1 = new Line(t1K1, Mathf.PI / 2 + t1AngleP0P1);

                    // Calc power line between P0 and P2 of t1
                    float t1DistanceP0P2 = (t1.P0 - t1.P2).magnitude;
                    float t1DistanceP0K2 = (t1DistanceP0P2 + (Mathf.Sqrt(t1RadiusP0) - Mathf.Sqrt(t1RadiusP2)) / t1DistanceP0P2) / 2;

                    Line t1LineP0P2 = new Line(t1.P0, t1.P2);
                    float t1AngleP0P2 = t1LineP0P2.Angle;

                    float t1AdjacentDistanceP0P2 = Mathf.Abs(Mathf.Cos(t1AngleP0P2) * t1DistanceP0K2);
                    float t1OppositeDistanceP0P2 = Mathf.Abs(Mathf.Sin(t1AngleP0P2) * t1DistanceP0K2);

                    t1AdjacentDistanceP0P2 *= t1.P2.x - t1.P0.x > 0 ? 1 : -1;
                    t1OppositeDistanceP0P2 *= t1.P2.y - t1.P0.y > 0 ? 1 : -1;

                    Vector2 t1K2 = new Vector2(t1.P0.x + t1AdjacentDistanceP0P2, t1.P0.y + t1OppositeDistanceP0P2);
                    Line t1PowerlineP0P2 = new Line(t1K2, Mathf.PI / 2 + t1AngleP0P2);

                    // Calc intersection
                    Vector2 v1 = (Vector2)Line.Intersect(t1PowerlineP0P1, t1PowerlineP0P2);


                    float t2RadiusP0 = (float)m_ownership[t2.P0].Radius;
                    float t2RadiusP1 = (float)m_ownership[t2.P1].Radius;
                    float t2RadiusP2 = (float)m_ownership[t2.P2].Radius;

                    // Calc power line between P0 and P1 of t2
                    float t2DistanceP0P1 = (t2.P0 - t2.P1).magnitude;
                    float t2DistanceP0K1 = (t2DistanceP0P1 + (Mathf.Sqrt(t2RadiusP0) - Mathf.Sqrt(t2RadiusP1)) / t2DistanceP0P1) / 2;

                    Line t2LineP0P1 = new Line(t2.P0, t2.P1);
                    float t2AngleP0P1 = t2LineP0P1.Angle;

                    float t2AdjacentDistanceP0P1 = Mathf.Abs(Mathf.Cos(t2AngleP0P1) * t2DistanceP0K1);
                    float t2OppositeDistanceP0P1 = Mathf.Abs(Mathf.Sin(t2AngleP0P1) * t2DistanceP0K1);

                    t2AdjacentDistanceP0P1 *= t2.P1.x - t2.P0.x > 0 ? 1 : -1;
                    t2OppositeDistanceP0P1 *= t2.P1.y - t2.P0.y > 0 ? 1 : -1;

                    Vector2 t2K1 = new Vector2(t2.P0.x + t2AdjacentDistanceP0P1, t2.P0.y + t2OppositeDistanceP0P1);
                    Line t2PowerlineP0P1 = new Line(t2K1, Mathf.PI / 2 + t2AngleP0P1);

                    // Calc power line between P0 and P2 of t2
                    float t2DistanceP0P2 = (t2.P0 - t2.P2).magnitude;
                    float t2DistanceP0K2 = (t2DistanceP0P2 + (Mathf.Sqrt(t2RadiusP0) - Mathf.Sqrt(t2RadiusP2)) / t2DistanceP0P2) / 2;

                    Line t2LineP0P2 = new Line(t2.P0, t2.P2);
                    float t2AngleP0P2 = t2LineP0P2.Angle;

                    float t2AdjacentDistanceP0P2 = Mathf.Abs(Mathf.Cos(t2AngleP0P2) * t2DistanceP0K2);
                    float t2OppositeDistanceP0P2 = Mathf.Abs(Mathf.Sin(t2AngleP0P2) * t2DistanceP0K2);

                    t2AdjacentDistanceP0P2 *= t2.P2.x - t2.P0.x > 0 ? 1 : -1;
                    t2OppositeDistanceP0P2 *= t2.P2.y - t2.P0.y > 0 ? 1 : -1;

                    Vector2 t2K2 = new Vector2(t2.P0.x + t2AdjacentDistanceP0P2, t2.P0.y + t2OppositeDistanceP0P2);
                    Line t2PowerlineP0P2 = new Line(t2K2, Mathf.PI / 2 + t2AngleP0P2);

                    // Calc intersection
                    Vector2 v2 = (Vector2)Line.Intersect(t2PowerlineP0P1, t2PowerlineP0P2);

                    //draw edge between radical centers
                    GL.Vertex3(v1.x, 0, v1.y);
                    GL.Vertex3(v2.x, 0, v2.y);
                }
            }
            GL.End();
        }

        /// <summary>
        /// Main drawing function that calls other auxiliary functions.
        /// </summary>
        /// <param name="m_Delaunay"></param>
        public static void Draw(Triangulation m_Delaunay, Dictionary<Vector2, PowerDiagramController.DictionaryPair> m_ownership)
        {
            m_lineMaterial.SetPass(0);

            // call functions that are set to true
            if (EdgesOn) DrawEdges(m_Delaunay);
            if (CircleOn) DrawCircles(m_Delaunay);
            if (VoronoiOn) DrawVoronoi(m_Delaunay, m_ownership);
        }
    }
}

