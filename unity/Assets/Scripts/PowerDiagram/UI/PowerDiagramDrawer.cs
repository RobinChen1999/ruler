namespace PowerDiagram
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Static class responsible for displaying voronoi graph and concepts.
    /// Draws the Voronoi graph, as well as edges of regular triangulation.
    /// </summary>
    public static class PowerDiagramDrawer
    {
        // toggle variables for displaying circles, edges, and voronoi graph
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
        /// Draw edges of the regular triangulation
        /// </summary>
        private static void DrawEdges(Vector2[] S, int[][] tri_list)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.green);
            
            
            foreach(var f in tri_list){
                GL.Vertex3(S[f[0]].x, 0, S[f[0]].y);
                GL.Vertex3(S[f[1]].x, 0, S[f[1]].y);
                GL.Vertex3(S[f[1]].x, 0, S[f[1]].y);
                GL.Vertex3(S[f[2]].x, 0, S[f[2]].y);
                GL.Vertex3(S[f[2]].x, 0, S[f[2]].y);
                GL.Vertex3(S[f[0]].x, 0, S[f[0]].y);
            }
            GL.End();
        }

        /// <summary>
        /// Draws the power diagram
        /// </summary>
        private static void DrawVoronoi(Dictionary<int, List<Vector2>> voronoi_cell_map)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.blue);

            foreach (var segment_list in voronoi_cell_map.Values){
                for(int i=0;i<segment_list.Count;i++){
                    GL.Vertex3(segment_list[i].x, 0, segment_list[i].y);
                    GL.Vertex3(segment_list[(i+1)%segment_list.Count].x, 0, segment_list[(i+1)%segment_list.Count].y);
                }
            }
            
            GL.End();
        }

        /// <summary>
        /// Main drawing function that calls other auxiliary functions.
        /// </summary>
        public static void Draw(Dictionary<int, List<Vector2>> voronoi_cell_map, Vector2[] S, int[][] tri_list)
        {
            m_lineMaterial.SetPass(0);

            if (EdgesOn) DrawEdges(S,tri_list);
            if (VoronoiOn) DrawVoronoi(voronoi_cell_map);
        }
    }
}

