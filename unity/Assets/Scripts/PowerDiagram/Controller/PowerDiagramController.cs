namespace PowerDiagram
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Util.Geometry.Polygon;

    /// <summary>
    /// Game controller for the voronoi game.
    /// </summary>
    public class PowerDiagramController : MonoBehaviour
    {
        // prefab instances for click objects
        public GameObject m_Player1Prefab;
        public GameObject m_Player2Prefab;

        // controller parameters
        public bool m_withLookAtOnPlacement = true;
        public int m_turns;

        // names of different victory scenes
        public string m_p1Victory;
        public string m_p2Victory;

        public PowerDiagramGUIManager m_GUIManager;
        public MeshFilter m_meshFilter;

        // variables defining state of turns
        private int m_halfTurnsTaken = 0;
        private bool player1Turn = true;

        private float[] m_playerArea;

        private Polygon2D m_meshRect;

        public List<Vector2> corners;
        public Dictionary<int, List<Vector2>> voronoi_cell_map;
        public Vector2[] S;
        public int[] O;
        public int[][] tri_list;

        // Created stuff
        public class DictionaryPair {
            public EOwnership Ownership;
            public double Radius;
        }

        private Dictionary<Vector2, GameObject> gameObjectList = new Dictionary<Vector2, GameObject>();


        // mapping of vertices to ownership enum
        private readonly Dictionary<Vector2, DictionaryPair> m_ownership = new Dictionary<Vector2, DictionaryPair>();

        public enum EOwnership
        {
            UNOWNED,
            PLAYER1,
            PLAYER2
        }

        // Use this for initialization
        public void Start()
        {
            // create polygon of rectangle window for intersection with voronoi
            float z = Vector2.Distance(m_meshFilter.transform.position, Camera.main.transform.position);
            var bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, z));
            var topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, z));
            m_meshRect = new Polygon2D(
                new List<Vector2>() {
                    new Vector2(bottomLeft.x, bottomLeft.z),
                    new Vector2(bottomLeft.x, topRight.z),
                    new Vector2(topRight.x, topRight.z),
                    new Vector2(topRight.x, bottomLeft.z)
                });
                
            corners = new List<Vector2>() {
                    new Vector2(Math.Min(bottomLeft.x,topRight.x), Math.Min(bottomLeft.z,topRight.z)),
                    new Vector2(Math.Max(bottomLeft.x,topRight.x), Math.Max(bottomLeft.z,topRight.z))
                };

            PowerDiagramDrawer.CreateLineMaterial();
        }

        private void Update()
        {
            if (Input.GetKeyDown("e"))
            {
                PowerDiagramDrawer.EdgesOn = !PowerDiagramDrawer.EdgesOn;
            }

            if (Input.GetKeyDown("v"))
            {
                PowerDiagramDrawer.VoronoiOn = !PowerDiagramDrawer.VoronoiOn;
            }

            if (Input.GetMouseButtonDown(0))
            {
                ProcessTurn();
            }
        }

        private void OnRenderObject()
        {
            GL.PushMatrix();

            // Set transformation matrix for drawing to
            // match our transform
            GL.MultMatrix(transform.localToWorldMatrix);

            if(m_ownership.Count>0) PowerDiagramDrawer.Draw(voronoi_cell_map, S, tri_list);

            GL.PopMatrix();
        }

        /// <summary>
        /// Creates new voronoi and updates mesh and player area.
        /// </summary>
        private void UpdateVoronoi()
        {
            if(m_ownership.Count>0){
                voronoi_cell_map = PowerDiagram.Create(m_ownership, corners, out S, out O, out tri_list);

                UpdateMesh();
                UpdatePlayerAreaOwned();
            }
        }

        /// <summary>
        /// Updates the mesh according to the Voronoi DCEL.
        /// </summary>
        private void UpdateMesh()
        {
            if (m_meshFilter.mesh == null)
            {
                // create initial mesh
                m_meshFilter.mesh = new Mesh
                {
                    subMeshCount = 2
                };
                m_meshFilter.mesh.MarkDynamic();
            }
            else
            {
                // clear old mesh
                m_meshFilter.mesh.Clear();
                m_meshFilter.mesh.subMeshCount = 2;
            }

            // build vertices and triangle list
            var vertices = new List<Vector3>();
            var triangles = new List<int>[2] {
                new List<int>(),
                new List<int>()
            };

            // iterate over vertices and create triangles accordingly
            for (int t=0;t<S.Length;t++)
            {
                var playerIndex = O[t];

                var face = voronoi_cell_map[t];

                // add triangles to correct list
                for (int i=1;i<face.Count-1;i++)
                {
                    int curCount = vertices.Count;

                    // add triangle vertices
                    vertices.Add(new Vector3(face[0].x, 0, face[0].y));
                    vertices.Add(new Vector3(face[i].x, 0, face[i].y));
                    vertices.Add(new Vector3(face[i+1].x, 0, face[i+1].y));

                    // add triangle to mesh according to owner
                    triangles[playerIndex].Add(curCount);
                    triangles[playerIndex].Add(curCount + 1);
                    triangles[playerIndex].Add(curCount + 2);
                }
            }

            // update mesh
            m_meshFilter.mesh.vertices = vertices.ToArray();
            m_meshFilter.mesh.SetTriangles(triangles[0], 0);
            m_meshFilter.mesh.SetTriangles(triangles[1], 1);
            m_meshFilter.mesh.RecalculateBounds();

            // set correct uv
            var newUVs = new List<Vector2>();
            foreach (var vertex in vertices)
            {
                newUVs.Add(new Vector2(vertex.x, vertex.z));
            }
            m_meshFilter.mesh.uv = newUVs.ToArray();
        }

        /// <summary>
        /// Calculates total area owned by each player separately.
        /// </summary>
        private void UpdatePlayerAreaOwned()
        {
            m_playerArea = new float[2] { 0, 0 };

            
            for (int t=0;t<S.Length;t++)
            {
                var playerIndex = O[t];

                var face = voronoi_cell_map[t];

                for (int i=1;i<face.Count-1;i++){
                    m_playerArea[playerIndex] += (float)AreaOfTriangle(face[0],face[i],face[i+1]);
                }
            }

            // update GUI to reflect new player area owned
            m_GUIManager.SetPlayerAreaOwned(m_playerArea[0], m_playerArea[1]);
        }
        
        public double AreaOfTriangle(Vector2 pt1, Vector2 pt2, Vector2 pt3)
        {
            double a = Vector2.Distance(pt1,pt2);
            double b = Vector2.Distance(pt2,pt3);
            double c = Vector2.Distance(pt1,pt3);
            double s = (a + b + c) / 2;
            if(s * (s-a) * (s-b) * (s-c)<0)Debug.Log(s * (s-a) * (s-b) * (s-c));
            return Math.Sqrt(Math.Max(0,s * (s-a) * (s-b) * (s-c)));
        }

        /// <summary>
        /// Process a turn taken
        /// </summary>
        private void ProcessTurn()
        {
            if (m_halfTurnsTaken == 0)
            {
                m_GUIManager.OnStartClicked();
            }
            if (m_halfTurnsTaken >= 2 * m_turns) {
                if (m_playerArea[0] > m_playerArea[1]) {
                    SceneManager.LoadScene(m_p1Victory);
                } else {
                    SceneManager.LoadScene(m_p2Victory);
                }
            } else {
                var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                pos.y = 0;
                var me = new Vector2(pos.x, pos.z);
                EOwnership ownership = player1Turn ? EOwnership.PLAYER1 : EOwnership.PLAYER2;
                List<Vector2> list = new List<Vector2>();
                foreach (KeyValuePair<Vector2, DictionaryPair> vertex in m_ownership) {
                    if (Vector2.Distance(vertex.Key, me) <= vertex.Value.Radius * 0.5) {
                        if (ownership == vertex.Value.Ownership) {
                            list.Add(vertex.Key);
                        }
                    }
                }
                int counter = 0;
                if (list.Count >= 1) {
                    foreach (Vector2 vertex in list) {
                        DictionaryPair vertexDP = m_ownership[vertex];
                        if (vertexDP.Radius < 5) {
                            bool increase = true;
                            foreach(var v in m_ownership){
                                if(!vertex.Equals(v.Key) && (Vector2.Distance(vertex,v.Key)<vertexDP.Radius || Vector2.Distance(vertex,v.Key)<m_ownership[v.Key].Radius * 0.5 )){
                                    increase = false;
                                    break;
                                }
                            }
                            if(increase){
                                GameObject gameObject = gameObjectList[vertex];
                                float size = (float)vertexDP.Radius * 2;
                                gameObject.transform.localScale = new Vector3(size, 0, size);
                                m_ownership[vertex] = new DictionaryPair { Ownership = vertexDP.Ownership, Radius = size };                                       
                                counter++;
                            }
                        }
                    }
                } else {
                    bool insert = true;
                    foreach(var v in m_ownership){
                        if(Vector2.Distance(me,v.Key)<0.5 || Vector2.Distance(me,v.Key)<m_ownership[v.Key].Radius * 0.5 ){
                            insert = false;
                            break;
                        }
                    }
                    if(insert){
                        m_ownership.Add(me, new DictionaryPair { Ownership = ownership, Radius = 1 });
                        var prefab = player1Turn ? m_Player1Prefab : m_Player2Prefab;
                        var onClickObject = Instantiate(prefab, pos, Quaternion.identity) as GameObject;
                        gameObjectList.Add(me, onClickObject);
                        onClickObject.transform.parent = gameObject.transform;
                        counter++;
                    }
                }
                if(counter>0){
                    UpdateVoronoi();
                    player1Turn = !player1Turn;
                    m_halfTurnsTaken += 1;
                }
                m_GUIManager.OnTurnStart(player1Turn);
                if (m_halfTurnsTaken >= 2 * m_turns)
                {
                    m_GUIManager.OnLastMove();
                }
            }
        }
    }
}
