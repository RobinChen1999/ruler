namespace PowerDiagram {
    using System;
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
        
        private static Line getLine(Vector2 v1, Vector2 v2, Dictionary<Vector2, PowerDiagramController.DictionaryPair> verticesRadii){
            double dist = Vector2.Distance(v1,v2);

            double radiusV1 = verticesRadii[v1].Radius * 0.5;
            double radiusV2 = verticesRadii[v2].Radius * 0.5;

            double d = (dist * dist + radiusV1 * radiusV1 - radiusV2 * radiusV2) / 2 / dist;

            Vector2 v = new Vector2((float)(d * (v1.x - v2.x) / dist), (float)(d * (v1.y - v2.y) / dist));

            Vector2 p1 = new Vector2((float)(v1.x - v.x), (float)(v1.y - v.y));
            double dif1 = Math.Abs(Vector2.Distance(v1, p1) * Vector2.Distance(v1, p1) - radiusV1 * radiusV1
                - Vector2.Distance(v2, p1) * Vector2.Distance(v2, p1) + radiusV2 * radiusV2);

            Vector2 p2 = new Vector2((float)(v1.x + v.x), (float)(v1.y + v.y));
            double dif2 = Math.Abs(Vector2.Distance(v1, p2) * Vector2.Distance(v1, p2) - radiusV1 * radiusV1
                - Vector2.Distance(v2, p2) * Vector2.Distance(v2, p2) + radiusV2 * radiusV2);

            if (dif1 < dif2 && dif1 < 0.1) {
                //Debug.Log(dif1);
                return new Line(p1, new Line(v1, v2).Angle + (float)Math.PI / 2);
            } else if (dif2 < dif1 && dif2 < 0.1) {
                //Debug.Log(dif2);
                return new Line(p2, new Line(v1, v2).Angle + (float)Math.PI / 2);
            }

            return null;
        }

        /// <summary>
        /// Compute radical center of a triangle
        /// </summary>
        public static Vector2 ComputeRadicalCenter(Triangle triangle, Dictionary<Vector2, PowerDiagramController.DictionaryPair> verticesRadii)
        {
            float radiusP0 = (float)verticesRadii[triangle.P0].Radius;
            float radiusP1 = (float)verticesRadii[triangle.P1].Radius;
            float radiusP2 = (float)verticesRadii[triangle.P2].Radius;

            // Calc power line between P0 and P1
            float distanceP0P1 = (triangle.P0 - triangle.P1).magnitude;
            float distanceP0K1 = (distanceP0P1 + (Mathf.Sqrt(radiusP0) - Mathf.Sqrt(radiusP1)) / distanceP0P1) / 2;

            Line lineP0P1 = new Line(triangle.P0, triangle.P1);
            float angleP0P1 = lineP0P1.Angle;

            float adjacentDistanceP0P1 = Mathf.Abs(Mathf.Cos(angleP0P1) * distanceP0K1);
            float oppositeDistanceP0P1 = Mathf.Abs(Mathf.Sin(angleP0P1) * distanceP0K1);

            adjacentDistanceP0P1 *= triangle.P1.x - triangle.P0.x > 0 ? 1 : -1;
            oppositeDistanceP0P1 *= triangle.P1.y - triangle.P0.y > 0 ? 1 : -1;

            Vector2 K1 = new Vector2(triangle.P0.x + adjacentDistanceP0P1, triangle.P0.y + oppositeDistanceP0P1);
            Line powerlineP0P1 = new Line(K1, Mathf.PI / 2 + angleP0P1);

            // Calc power line between P0 and P2
            float distanceP0P2 = (triangle.P0 - triangle.P2).magnitude;
            float distanceP0K2 = (distanceP0P2 + (Mathf.Sqrt(radiusP0) - Mathf.Sqrt(radiusP2)) / distanceP0P2) / 2;

            Line lineP0P2 = new Line(triangle.P0, triangle.P2);
            float angleP0P2 = lineP0P2.Angle;

            float adjacentDistanceP0P2 = Mathf.Abs(Mathf.Cos(angleP0P2) * distanceP0K2);
            float oppositeDistanceP0P2 = Mathf.Abs(Mathf.Sin(angleP0P2) * distanceP0K2);

            adjacentDistanceP0P2 *= triangle.P2.x - triangle.P0.x > 0 ? 1 : -1;
            oppositeDistanceP0P2 *= triangle.P2.y - triangle.P0.y > 0 ? 1 : -1;

            Vector2 K2 = new Vector2(triangle.P0.x + adjacentDistanceP0P2, triangle.P0.y + oppositeDistanceP0P2);
            Line powerlineP0P2 = new Line(K2, Mathf.PI / 2 + angleP0P2);

            // Calc intersection
            return (Vector2)Line.Intersect(powerlineP0P1, powerlineP0P2);            
        }

        /// <summary>
        /// Creates a Voronoi DCEL from a triangulation. Triangulation should be Delaunay
        /// </summary>
        public static DCEL Create(Triangulation m_Delaunay, Dictionary<Vector2, PowerDiagramController.DictionaryPair> verticesRadii) {
            if (!Delaunay.IsValid(m_Delaunay)) {
                throw new GeomException("Triangulation should be delaunay for the Voronoi diagram.");
            }

            var dcel = new DCEL();

            // create vertices for each triangles radical center and store them in a dictionary
            Dictionary<Triangle, DCELVertex> vertexMap = new Dictionary<Triangle, DCELVertex>();
            foreach (var triangle in m_Delaunay.Triangles) {
                Line l1 = getLine(triangle.P0,triangle.P1, verticesRadii);
                Line l2 = getLine(triangle.P0,triangle.P2, verticesRadii);
                Line l3 = getLine(triangle.P1,triangle.P2, verticesRadii);
                
                Vector2 v1=Line.Intersect(l1,l2).Value;
                Vector2 v2=Line.Intersect(l1,l3).Value;
                Vector2 v3=Line.Intersect(l2,l3).Value;
                double s11=Vector2.Distance(v1,triangle.P0)*Vector2.Distance(v1,triangle.P0)-verticesRadii[triangle.P0].Radius*0.5*verticesRadii[triangle.P0].Radius*0.5;
                double s12=Vector2.Distance(v1,triangle.P1)*Vector2.Distance(v1,triangle.P1)-verticesRadii[triangle.P1].Radius*0.5*verticesRadii[triangle.P1].Radius*0.5;
                double s13=Vector2.Distance(v1,triangle.P2)*Vector2.Distance(v1,triangle.P2)-verticesRadii[triangle.P2].Radius*0.5*verticesRadii[triangle.P2].Radius*0.5;
                double s1=Math.Max(Math.Max(s11,s12),s13)-Math.Min(Math.Min(s11,s12),s13);
                double s21=Vector2.Distance(v2,triangle.P0)*Vector2.Distance(v2,triangle.P0)-verticesRadii[triangle.P0].Radius*0.5*verticesRadii[triangle.P0].Radius*0.5;
                double s22=Vector2.Distance(v2,triangle.P1)*Vector2.Distance(v2,triangle.P1)-verticesRadii[triangle.P1].Radius*0.5*verticesRadii[triangle.P1].Radius*0.5;
                double s23=Vector2.Distance(v2,triangle.P2)*Vector2.Distance(v2,triangle.P2)-verticesRadii[triangle.P2].Radius*0.5*verticesRadii[triangle.P2].Radius*0.5;
                double s2=Math.Max(Math.Max(s21,s22),s23)-Math.Min(Math.Min(s21,s22),s23);
                double s31=Vector2.Distance(v3,triangle.P0)*Vector2.Distance(v3,triangle.P0)-verticesRadii[triangle.P0].Radius*0.5*verticesRadii[triangle.P0].Radius*0.5;
                double s32=Vector2.Distance(v3,triangle.P1)*Vector2.Distance(v3,triangle.P1)-verticesRadii[triangle.P1].Radius*0.5*verticesRadii[triangle.P1].Radius*0.5;
                double s33=Vector2.Distance(v3,triangle.P2)*Vector2.Distance(v3,triangle.P2)-verticesRadii[triangle.P2].Radius*0.5*verticesRadii[triangle.P2].Radius*0.5;
                double s3=Math.Max(Math.Max(s31,s32),s33)-Math.Min(Math.Min(s31,s32),s33);
                
                Vector2 radicalCenter = v1;
                if(s2<=s1&&s2<=s3)radicalCenter = v2;
                if(s3<=s1&&s3<=s2)radicalCenter = v3; 
                //Vector2 radicalCenter = ComputeRadicalCenter(triangle, verticesRadii);

                // Store results
                var vertex = new DCELVertex(radicalCenter);
                dcel.AddVertex(vertex);
                vertexMap.Add(triangle, vertex);
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