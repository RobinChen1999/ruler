namespace PowerDiagram {
    using System.Collections.Generic;
    using UnityEngine;
    using Util.Algorithms.Triangulation;
    using Util.Geometry;
    using Util.Geometry.DCEL;
    using Util.Geometry.Triangulation;

    /// <summary>
    /// Collection of algorithms related to Voronoi diagrams.
    /// </summary>
    public static class PowerDiagram {

        /// <summary>
        /// Create Voronoi DCEL from a collection of vertices.
        /// First creates a delaunay triangulation and then the corresponding Voronoi diagram
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns>DCEL representation of Voronoi diagram</returns>
        //public static DCEL Create(IEnumerable<Vector2> vertices) {
        //    // create delaunay triangulation
        //    // from this the voronoi diagram can be obtained
        //    var m_Delaunay = Delaunay.Create(vertices);

        //    return Create(m_Delaunay);
        //}

        /// <summary>
        /// Creates a Voronoi DCEL from a triangulation. Triangulation should be Delaunay
        /// </summary>
        /// <param name="m_Delaunay"></param>
        /// <returns></returns>
        public static DCEL Create(Triangulation m_Delaunay, Dictionary<Vector2, PowerDiagramController.DictionaryPair> verticesRadii) {
            if (!Delaunay.IsValid(m_Delaunay)) {
                throw new GeomException("Triangulation should be delaunay for the Voronoi diagram.");
            }

            var dcel = new DCEL();

            // create vertices for each triangles circumcenter and store them in a dictionary
            Dictionary<Triangle, DCELVertex> vertexMap = new Dictionary<Triangle, DCELVertex>();
            foreach (var triangle in m_Delaunay.Triangles) {
                // degenerate triangle, just ignore
                if (!triangle.Circumcenter.HasValue) continue;

                float RadiusP0 = (float) verticesRadii[triangle.P0].Radius;
                float RadiusP1 = (float) verticesRadii[triangle.P1].Radius;
                float RadiusP2 = (float) verticesRadii[triangle.P2].Radius;

                // Calc power line between P0 and P1
                float DistanceP0P1 = (triangle.P0 - triangle.P1).magnitude;
                float P0distanceToK1 = (DistanceP0P1 + (Mathf.Sqrt(RadiusP0) - Mathf.Sqrt(RadiusP1)) / DistanceP0P1) / 2;

                Line lineP0P1 = new Line(triangle.P0, triangle.P1);
                float angleP0P1 = lineP0P1.Angle;

                float adjacentDistanceP0P1 = Mathf.Abs(Mathf.Cos(angleP0P1) * P0distanceToK1);
                float oppositeDistanceP0P1 = Mathf.Abs(Mathf.Sin(angleP0P1) * P0distanceToK1);

                adjacentDistanceP0P1 *= triangle.P1.x - triangle.P0.x > 0 ? 1 : -1;
                oppositeDistanceP0P1 *= triangle.P1.y - triangle.P0.y > 0 ? 1 : -1;

                Vector2 K1 = new Vector2(triangle.P0.x + adjacentDistanceP0P1, triangle.P0.y + oppositeDistanceP0P1);
                Line P0P1PowerLine = new Line(K1, Mathf.PI / 2 + angleP0P1);
                
                // Calc power line between P0 and P2
                float DistanceP0P2 = (triangle.P0 - triangle.P2).magnitude;
                float P0distanceToK2 = (DistanceP0P2 + (Mathf.Sqrt(RadiusP0) - Mathf.Sqrt(RadiusP2)) / DistanceP0P2) / 2;

                Line lineP0P2 = new Line(triangle.P0, triangle.P2);
                float angleP0P2 = lineP0P2.Angle;

                float adjacentDistanceP0P2 = Mathf.Abs(Mathf.Cos(angleP0P2) * P0distanceToK2);
                float oppositeDistanceP0P2 = Mathf.Abs(Mathf.Sin(angleP0P2) * P0distanceToK2);

                adjacentDistanceP0P2 *= triangle.P2.x - triangle.P0.x > 0 ? 1 : -1;
                oppositeDistanceP0P2 *= triangle.P2.y - triangle.P0.y > 0 ? 1 : -1;

                Vector2 K2 = new Vector2(triangle.P0.x + adjacentDistanceP0P2, triangle.P0.y + oppositeDistanceP0P2);
                Line P0P2PowerLine = new Line(K2, Mathf.PI / 2 + angleP0P2);

                // Calc intersection
                Vector2 RadicalCenter = (Vector2) Line.Intersect(P0P1PowerLine, P0P2PowerLine);

                // Store results
                var vertex = new DCELVertex(RadicalCenter);
                dcel.AddVertex(vertex);
                vertexMap.Add(triangle, vertex);

                //Debug.Log("=======");
                //Debug.Log("Point 0: " + triangle.P0);
                //Debug.Log("Point 1: " + triangle.P1);
                //Debug.Log("Point 2: " + triangle.P2);
                //Debug.Log("Angle 1: " + angleP0P1);
                //Debug.Log("Angle 2: " + angleP0P2);
                //Debug.Log("K1: " + K1);
                //Debug.Log("K2: " + K2);
                //Debug.Log("Line 1: " + P0P1PowerLine);
                //Debug.Log("Line 2: " + P0P2PowerLine);
                //Debug.Log("Radical center: " + RadicalCenter);
                //Debug.Log("Voronoi center: " + triangle.Circumcenter.Value);
            }

            // remember which edges where visited
            // since each edge has a twin
            var edgesVisited = new HashSet<TriangleEdge>();

            foreach (var edge in m_Delaunay.Edges) {
                // either already visited twin edge or edge is outer triangle
                if (edgesVisited.Contains(edge) || edge.IsOuter) continue;

                // add edge between the two adjacent triangles vertices
                // vertices at circumcenter of triangle
                if (edge.T != null && edge.Twin.T != null) {
                    var v1 = vertexMap[edge.T];
                    var v2 = vertexMap[edge.Twin.T];

                    dcel.AddEdge(v1, v2);

                    edgesVisited.Add(edge);
                    edgesVisited.Add(edge.Twin);
                }
            }

            return dcel;
        }
    }
}