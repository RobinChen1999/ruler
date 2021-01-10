namespace PowerDiagram
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Util.Algorithms.DCEL;
    using Util.Algorithms.Polygon;
    using Util.Algorithms.Triangulation;
    using Util.Geometry.DCEL;
    using Util.Geometry.Polygon;
    using Util.Geometry.Triangulation;

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

        private Triangulation m_delaunay;
        //private FishManager m_fishManager;
        private Polygon2D m_meshRect;

        // voronoi dcel
        // calculated after every turn
        private DCEL m_DCEL;

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
            // create initial delaunay triangulation (three far-away points)
            m_delaunay = Delaunay.Create();

            // add auxiliary vertices as unowned
            foreach (var vertex in m_delaunay.Vertices)
            {
                m_ownership.Add(vertex, new DictionaryPair { Ownership = EOwnership.UNOWNED, Radius = 1 });
            }

            //m_fishManager = new FishManager();

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

            PowerDiagramDrawer.CreateLineMaterial();
        }

        private void Update()
        {
            if (Input.GetKeyDown("c"))
            {
                PowerDiagramDrawer.CircleOn = !PowerDiagramDrawer.CircleOn;
            }

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

            PowerDiagramDrawer.Draw(m_delaunay, m_ownership);

            GL.PopMatrix();
        }

        /// <summary>
        /// Creates new voronoi and updates mesh and player area.
        /// </summary>
        private void UpdateVoronoi()
        {
            // create voronoi diagram from delaunay triangulation
            //m_DCEL = Voronoi.Create(m_delaunay);
            
            m_DCEL = PowerDiagram.Create(m_delaunay, m_ownership);

            UpdateMesh();
            UpdatePlayerAreaOwned();
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
            foreach (var inputNode in m_delaunay.Vertices)
            {
                // dont draw anything for unowned vertices
                if (m_ownership[inputNode].Ownership == EOwnership.UNOWNED) continue;

                // get ownership of node
                var playerIndex = m_ownership[inputNode].Ownership == EOwnership.PLAYER1 ? 0 : 1;

                var face = m_DCEL.GetContainingFace(inputNode);

                // cant triangulate outer face
                if (face.IsOuter) continue;

                // triangulate face polygon
                var triangulation = Triangulator.Triangulate(face.Polygon.Outside);

                // add triangles to correct list
                foreach (var triangle in triangulation.Triangles)
                {
                    int curCount = vertices.Count;

                    // add triangle vertices
                    vertices.Add(new Vector3(triangle.P0.x, 0, triangle.P0.y));
                    vertices.Add(new Vector3(triangle.P1.x, 0, triangle.P1.y));
                    vertices.Add(new Vector3(triangle.P2.x, 0, triangle.P2.y));

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

            foreach (var inputNode in m_delaunay.Vertices)
            {
                // get dcel face containing input node
                var face = m_DCEL.GetContainingFace(inputNode);

                if (m_ownership[inputNode].Ownership != EOwnership.UNOWNED)
                {
                    // update player area with face that intersects with window
                    var playerIndex = m_ownership[inputNode].Ownership == EOwnership.PLAYER1 ? 0 : 1;
                    m_playerArea[playerIndex] += Intersector.IntersectConvex(m_meshRect, face.Polygon.Outside).Area;
                }
            }

            // update GUI to reflect new player area owned
            m_GUIManager.SetPlayerAreaOwned(m_playerArea[0], m_playerArea[1]);
        }

        /// <summary>
        /// Process a turn taken
        /// </summary>
        private void ProcessTurn()
        {
            if (m_halfTurnsTaken == 0)
            {
                // game has just been started
                m_GUIManager.OnStartClicked();
            }

            // load victory if screen clicked after every player has taken turn
            if (m_halfTurnsTaken >= 2 * m_turns) {
                if (m_playerArea[0] > m_playerArea[1]) {
                    SceneManager.LoadScene(m_p1Victory);
                } else {
                    SceneManager.LoadScene(m_p2Victory);
                }
            } else {
                // obtain mouse position vector
                var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                pos.y = 0;
                var me = new Vector2(pos.x, pos.z);

                EOwnership ownership = player1Turn ? EOwnership.PLAYER1 : EOwnership.PLAYER2;

                List<KeyValuePair<Vector2, DictionaryPair>> list = new List<KeyValuePair<Vector2, DictionaryPair>>();

                // check if vertex already in graph to avoid degenerate cases
                foreach (KeyValuePair<Vector2, DictionaryPair> vertex in m_ownership) {
                    // Check if selected point lies on existing vertex and see to which team it belongs
                    // Note: Radius * 0.5 since circle sprite has a radius of 0.5 initially
                    if (Vector2.Distance(vertex.Key, me) < vertex.Value.Radius * 0.5) {
                        if (ownership == vertex.Value.Ownership) {
                            list.Add(vertex);
                        } else {
                            return;
                        }
                    }
                }

                // If there exists only one vertex that's applicable
                if (list.Count == 1) {
                    // Check if clicked vertex can increase in size
                    if (list[0].Value.Radius < 5) {

                        // Go over all vertices
                        List<Vector2> vertexKeys = new List<Vector2>(m_ownership.Keys);

                        foreach (Vector2 vertex in vertexKeys) {
                            DictionaryPair vertexDP = m_ownership[vertex];

                            // Only vertices of the same player
                            if (vertexDP.Ownership == list[0].Value.Ownership) {
                                if (vertexDP.Radius < 5) {
                                    Debug.Log("Increase size of vertex");

                                    // Increase size
                                    GameObject gameObject = gameObjectList[vertex];

                                    float size = (float)vertexDP.Radius * 2;

                                    gameObject.transform.localScale = new Vector3(size, 0, size);

                                    m_ownership[vertex] = new DictionaryPair { Ownership = vertexDP.Ownership, Radius = size };
                                } else {
                                    Debug.Log("Size too big!");
                                }
                            }
                        }
                    } else {
                        Debug.Log("Size too big!");
                        return;
                    }
                } else if (list.Count > 1) {
                    Debug.Log("Point lies in multiple circles");
                    return;
                } else
                {
                    Debug.Log("Create new circle");

                    // store owner of vertex
                    m_ownership.Add(me, new DictionaryPair { Ownership = ownership, Radius = 1 });

                    Delaunay.AddVertex(m_delaunay, me);

                    // instantiate the relevant game object at click position
                    var prefab = player1Turn ? m_Player1Prefab : m_Player2Prefab;
                    var onClickObject = Instantiate(prefab, pos, Quaternion.identity) as GameObject;

                    if (onClickObject == null)
                    {
                        throw new InvalidProgramException("Couldn't instantiate m_PlayerPrefab!");
                    }

                    gameObjectList.Add(me, onClickObject);

                    // set parent to this game object for better nesting
                    onClickObject.transform.parent = gameObject.transform;

                    // add object to the fish manager
                    // m_fishManager.AddFish(onClickObject.transform, player1Turn, m_withLookAtOnPlacement);
                }

                UpdateVoronoi();

                // update player turn
                player1Turn = !player1Turn;
                m_GUIManager.OnTurnStart(player1Turn);

                //Update turn counter
                m_halfTurnsTaken += 1;
                if (m_halfTurnsTaken >= 2 * m_turns)
                {
                    m_GUIManager.OnLastMove();
                }
            }
        }
    }
}
