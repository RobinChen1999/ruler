namespace PowerDiagram {
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Util.Algorithms.Triangulation;
    using Util.Geometry;
    using Util.Geometry.DCEL;
    using Util.Geometry.Triangulation;
    using MIConvexHull;

    /// <summary>
    /// Collection of algorithms related to Voronoi diagrams.
    /// </summary>
    public static class PowerDiagram {
        
        public static Vector3 get_triangle_normal(Vector3 A, Vector3 B, Vector3 C){
            return Vector3.Normalize(Vector3.Cross(A, B) + Vector3.Cross(B, C) + Vector3.Cross(C, A));
        }
        
        public static Vector2 get_power_circumcenter(Vector3 A, Vector3 B, Vector3 C){
            Vector3 N = get_triangle_normal(A, B, C);
            return new Vector2((float)((-0.5 / N.z) * N.x), (float)((-0.5 / N.z) * N.y));
        }
        
        public static bool is_ccw_triangle(Vector2 A, Vector2 B, Vector2 C){
            return A.x*B.y+A.y*C.x+B.x*C.y-B.y*C.x-A.y*B.x-A.x*C.y > 0;
        }
        
        public static void get_power_triangulation(Vector2[] S, double[] R, out int[][] tri_list, out Vector2[] V){
            Vector3[] S_lifted = new Vector3[S.Length];
            for(int i=0;i<S.Length;i++){
                S_lifted[i] = new Vector3(S[i].x, S[i].y, (float)(S[i].x*S[i].x+S[i].y*S[i].y-R[i]*R[i]));
            }

            if (S.Length == 3){
                tri_list = new int[1][];
                tri_list[0] = new int[3];
                V = new Vector2[1];
                
                if (is_ccw_triangle(S[0], S[1], S[2])){
                    tri_list[0][0]=0;
                    tri_list[0][1]=1;
                    tri_list[0][2]=2;
                    V[0]=get_power_circumcenter(S_lifted[0],S_lifted[1],S_lifted[2]);
                }
                else{
                    tri_list[0][0]=0;
                    tri_list[0][1]=2;
                    tri_list[0][2]=1;
                    V[0]=get_power_circumcenter(S_lifted[0],S_lifted[2],S_lifted[1]);
                }
                return;
            }

            VVertex[] vertices = new VVertex[S_lifted.Length];
            for (int i = 0; i < S_lifted.Length; i++){
                vertices[i] = new VVertex(i, S_lifted[i].x, S_lifted[i].y, S_lifted[i].z);
            }
            
            var convexHull = ConvexHull.Create(vertices);
            int counter = 0;
            foreach(var f in convexHull.Result.Faces){
                if(f.Normal[2] <= 0) {
                    counter++;
                }
            }
            tri_list = new int[counter][];
            V = new Vector2[counter];
            counter = 0;
            foreach(var f in convexHull.Result.Faces){
                if(f.Normal[2] <= 0) {
                    tri_list[counter] = new int[3];
                    if(is_ccw_triangle(S[f.Vertices[0].ind], S[f.Vertices[1].ind], S[f.Vertices[2].ind])){
                        tri_list[counter][0]=f.Vertices[0].ind;
                        tri_list[counter][1]=f.Vertices[1].ind;
                        tri_list[counter][2]=f.Vertices[2].ind;
                    }
                    else {
                        tri_list[counter][0]=f.Vertices[0].ind;
                        tri_list[counter][1]=f.Vertices[2].ind;
                        tri_list[counter][2]=f.Vertices[1].ind;
                    }
                    counter++;
                }
            }
            for (int i = 0; i < counter; i++){
                V[i] = get_power_circumcenter(S_lifted[tri_list[i][0]], S_lifted[tri_list[i][1]], S_lifted[tri_list[i][2]]);
            }
        }
        
        public static void AddEdge(Dictionary<int, Dictionary<int,List<int>>> edge_map, int a, int b, int i){
            if(edge_map.ContainsKey(a)){
                if(edge_map[a].ContainsKey(b)){
                    edge_map[a][b].Add(i);
                }
                else{
                    edge_map[a].Add(b, new List<int>());
                    edge_map[a][b].Add(i);
                }
            }
            else{
                edge_map.Add(a, new Dictionary<int,List<int>>());
                edge_map[a].Add(b, new List<int>());
                edge_map[a][b].Add(i);
            }
        }
        
        public static Dictionary<int, List<Vector2>> get_voronoi_cells(Vector2[] S, Vector2[] V, int[][] tri_list, List<Vector2> corners){
            Dictionary<int, Dictionary<int,List<int>>> edge_map = new Dictionary<int, Dictionary<int,List<int>>>();
            for (int i=0;i<tri_list.Length;i++){
                AddEdge(edge_map, tri_list[i][0], tri_list[i][1], i);
                AddEdge(edge_map, tri_list[i][1], tri_list[i][0], i);
                AddEdge(edge_map, tri_list[i][0], tri_list[i][2], i);
                AddEdge(edge_map, tri_list[i][2], tri_list[i][0], i);
                AddEdge(edge_map, tri_list[i][1], tri_list[i][2], i);
                AddEdge(edge_map, tri_list[i][2], tri_list[i][1], i);
            }

            Dictionary<int, List<PowerCell>> voronoi_cell_map = new Dictionary<int, List<PowerCell>>();
            

            for (int i=0;i<tri_list.Length;i++){
                for (int t=0;t<3;t++){
                    int u= tri_list[i][t];
                    int v= tri_list[i][(t+1)%3];
                    int w= tri_list[i][(t+2)%3];
                    if (edge_map[u][v].Count == 2){
                        int j = edge_map[u][v][0];
                        int k = edge_map[u][v][1];
                        if (k == i){
                            j = j+k;
                            k = j-k;
                            j = j-k;
                        }
                        Vector2 U = V[k] - V[j];
                        double U_norm = U.magnitude;	
                        if(!voronoi_cell_map.ContainsKey(u)){
                            voronoi_cell_map.Add(u, new List<PowerCell>());
                        }
                        voronoi_cell_map[u].Add(new PowerCell(j, k, V[j], U / (float)U_norm, 0, U_norm));
                    }
                    else{
                        Vector2 A = S[u];
                        Vector2 B = S[v];
                        Vector2 C = S[w];
                        Vector2 D = V[i];
                        Vector2 U = (B - A).normalized;
                        Vector2 I = A + Vector2.Dot(D - A, U) * U;
                        Vector2 W = (I - D).normalized;
                        if (Vector2.Dot(W, I - C) < 0){
                            W = -W;
                        }
                        
                        if(!voronoi_cell_map.ContainsKey(u)){
                            voronoi_cell_map.Add(u, new List<PowerCell>());
                        }	
                        if(!voronoi_cell_map.ContainsKey(v)){
                            voronoi_cell_map.Add(v, new List<PowerCell>());
                        }
                        voronoi_cell_map[u].Add(new PowerCell(edge_map[u][v][0], -1, D, W, 0,1000));			
                        voronoi_cell_map[v].Add(new PowerCell(-1, edge_map[u][v][0], D, W, 1000,0));
                    }	
                }                        
            }
            
            Dictionary<int, List<PowerCell>> sorted = new Dictionary<int, List<PowerCell>>();
            foreach (var f in voronoi_cell_map){
                sorted.Add(f.Key, order_segment_list(f.Value));
            }
            Dictionary<int, List<Vector2>> cut = new Dictionary<int, List<Vector2>>();
            foreach (var f in sorted){
                cut.Add(f.Key, cutOff(f.Value, corners));
            }
            addCorner(cut, new Vector2(corners[0].x,corners[0].y), corners);
            addCorner(cut, new Vector2(corners[0].x,corners[1].y), corners);
            addCorner(cut, new Vector2(corners[1].x,corners[0].y), corners);
            addCorner(cut, new Vector2(corners[1].x,corners[1].y), corners);
            return cut;
        }
        
        public static void addCorner(Dictionary<int, List<Vector2>> cut, Vector2 corner, List<Vector2> corners){
            int cell=-1;
            int index=-1;
            double min=1000000;
            foreach(var f in cut){
                for(int i=0;i<f.Value.Count;i++){
                    Vector2 v1= f.Value[i];
                    Vector2 v2= f.Value[(i+1)%f.Value.Count];
                    bool b1 = compare(v1.x,v2.x,corners[0].x);
                    bool b2 = compare(v1.y,v2.y,corners[0].y);
                    bool b3 = compare(v1.x,v2.x,corners[1].x);
                    bool b4 = compare(v1.y,v2.y,corners[1].y);
                    if(((b1&&b2)||(b1&&b3)||(b1&&b4)||(b2&&b3)||(b2&&b4)||(b3&&b4))&&
                            min>new LineSegment(v1,v2).DistanceToPoint(corner)){
                        cell=f.Key;
                        index=i+1;
                        min=new LineSegment(v1,v2).DistanceToPoint(corner);
                    }
                }
            }
            if(min>0.0001){
                cut[cell].Insert(index,corner);
            }
        }
        
        public static bool compare(double a, double b, double c){
            double r=0.0001;
            return (Math.Abs(a-c)<r&&Math.Abs(b-c)>r)||(Math.Abs(a-c)>r&&Math.Abs(b-c)<r);
        }
        
        public static List<Vector2> cutOff(List<PowerCell> segment_list, List<Vector2> corners){
            List<Vector2> list = new List<Vector2>();
            foreach(var f in segment_list){
                Vector2 v1 = getv1(f);
                Vector2 v2 = getv2(f);
                if(list.Count==0||list[list.Count-1]!=v1){
                    list.Add(v1);
                }
                if(list.Count==0||list[list.Count-1]!=v2){
                    list.Add(v2);
                }
            }
            List<Vector2> cut = new List<Vector2>();
            for(int i=0;i<list.Count;i++){
                Vector2 v1= list[i];
                Vector2 v2= list[(i+1)%list.Count];
                if(v1.x>=corners[0].x&&v1.y>=corners[0].y&&v1.x<=corners[1].x&&v1.y<=corners[1].y){
                    if(cut.Count==0||cut[cut.Count-1]!=v1){
                        cut.Add(v1);
                    }
                }
                Vector2? a=LineSegment.Intersect(new LineSegment(v1,v2),new LineSegment(new Vector2(corners[0].x,corners[0].y),new Vector2(corners[0].x,corners[1].y)));
                Vector2? b=LineSegment.Intersect(new LineSegment(v1,v2),new LineSegment(new Vector2(corners[0].x,corners[0].y),new Vector2(corners[1].x,corners[0].y)));
                Vector2? c=LineSegment.Intersect(new LineSegment(v1,v2),new LineSegment(new Vector2(corners[0].x,corners[1].y),new Vector2(corners[1].x,corners[1].y)));
                Vector2? d=LineSegment.Intersect(new LineSegment(v1,v2),new LineSegment(new Vector2(corners[1].x,corners[0].y),new Vector2(corners[1].x,corners[1].y)));
                List<Vector2> inter = new List<Vector2>();
                if(a.HasValue) inter.Add(a.Value);
                if(b.HasValue) inter.Add(b.Value);
                if(c.HasValue) inter.Add(c.Value);
                if(d.HasValue) inter.Add(d.Value);
                if(inter.Count>1&&Vector2.Distance(v1,inter[1])<Vector2.Distance(v1,inter[0])){
                    Vector2 temp = inter[0];
                    inter[0] = inter[1];
                    inter[1] = temp;
                }
                for(int j=0;j<inter.Count;j++){
                    if(cut.Count==0||cut[cut.Count-1]!=inter[j]){
                        cut.Add(inter[j]);
                    }
                }
            }
            return cut;
        }
        
        public static List<PowerCell> order_segment_list(List<PowerCell> segment_list){
            int min = 1000000;
            Dictionary<int, PowerCell> dic = new Dictionary<int, PowerCell>();
            for(int i=0;i<segment_list.Count;i++){
                dic.Add(segment_list[i].i, segment_list[i]);
                min = Math.Min(min, segment_list[i].i);
            }
            
            List<PowerCell> list = new List<PowerCell>();
            while(list.Count<segment_list.Count){
                list.Add(dic[min]);
                min = dic[min].j;
            }
            return list;
        }
        
        public static Dictionary<int, List<Vector2>> Create(Dictionary<Vector2, PowerDiagramController.DictionaryPair> verticesRadii, List<Vector2> corners, out Vector2[] S, out int[] O, out int[][] tri_list){
            if (verticesRadii.Count >= 3){
                S = new Vector2[verticesRadii.Count];
                double[] R = new double[verticesRadii.Count];
                O = new int[verticesRadii.Count];
                int i = 0;
                foreach(var f in verticesRadii){
                    S[i]=f.Key;
                    R[i]=f.Value.Radius/2;
                    O[i]= f.Value.Ownership == PowerDiagramController.EOwnership.PLAYER1 ? 0 : 1;
                    i++;
                }
                
                Vector2[] V;
                get_power_triangulation(S, R, out tri_list, out V);
                return get_voronoi_cells(S, V, tri_list, corners);
            }            
            else if(verticesRadii.Count == 2){
                S = new Vector2[verticesRadii.Count];
                double[] R = new double[verticesRadii.Count];
                O = new int[verticesRadii.Count];
                int i = 0;
                foreach(var f in verticesRadii){
                    S[i]=f.Key;
                    R[i]=f.Value.Radius/2;
                    O[i]= f.Value.Ownership == PowerDiagramController.EOwnership.PLAYER1 ? 0 : 1;
                    i++;
                }
                tri_list = new int[1][];
                tri_list[0] = new int[3];
                tri_list[0][0]=0;
                tri_list[0][1]=1;
                tri_list[0][2]=0;
                
                //dist=a+b
                //a2-r2=b2-R2
                //(a-b)(a+b)=r2-R2
                //(a-b)dist=r2-R2
                //2a-dist=(r2-R2)/dist
                //a=((r2-R2)/dist+dist)/2
                Dictionary<int, List<Vector2>> voronoi_cell_map = new Dictionary<int, List<Vector2>>();
                double dist = Vector2.Distance(S[0],S[1]);
                double d = (dist*dist+R[0]*R[0]-R[1]*R[1])/2/dist;
                Vector2 v = S[0]+(S[1]-S[0])*(float)(d/dist);
                Line l = new Line(v,new Line(S[0],S[1]).Angle+(float)Math.PI/2);
                
                List<Vector2> list = new List<Vector2>();
                list.Add(new Vector2(corners[0].x,corners[0].y));
                list.Add(new Vector2(corners[0].x,corners[1].y));
                list.Add(new Vector2(corners[1].x,corners[1].y));
                list.Add(new Vector2(corners[1].x,corners[0].y));
                List<Vector2> list1 = new List<Vector2>();
                List<Vector2> list2 = new List<Vector2>();
                bool first=true;
                for(i=0;i<4;i++){
                    if(first)list1.Add(list[i]);
                    else list2.Add(list[i]);
                    if(LineSegment.Intersect(new LineSegment(list[i],list[(i+1)%4]),l).HasValue){
                        list1.Add(LineSegment.Intersect(new LineSegment(list[i],list[(i+1)%4]),l).Value);
                        list2.Add(LineSegment.Intersect(new LineSegment(list[i],list[(i+1)%4]),l).Value);
                        first=!first;
                    }
                }
                
                if(Math.Pow(Vector2.Distance(list[0],S[0]),2)-R[0]*R[0]<Math.Pow(Vector2.Distance(list[0],S[1]),2)-R[1]*R[1]){
                    voronoi_cell_map.Add(0,list1);
                    voronoi_cell_map.Add(1,list2);
                }
                else{
                    voronoi_cell_map.Add(0,list2);
                    voronoi_cell_map.Add(1,list1);
                }
                
                return voronoi_cell_map;
            }
            else { //if(verticesRadii.Count == 1)
                S = new Vector2[verticesRadii.Count];
                double[] R = new double[verticesRadii.Count];
                O = new int[verticesRadii.Count];
                int i = 0;
                foreach(var f in verticesRadii){
                    S[i]=f.Key;
                    R[i]=f.Value.Radius/2;
                    O[i]= f.Value.Ownership == PowerDiagramController.EOwnership.PLAYER1 ? 0 : 1;
                    i++;
                }
                tri_list = new int[1][];
                tri_list[0] = new int[3];
                tri_list[0][0]=0;
                tri_list[0][1]=0;
                tri_list[0][2]=0;
                Dictionary<int, List<Vector2>> voronoi_cell_map = new Dictionary<int, List<Vector2>>();
                List<Vector2> list = new List<Vector2>();
                list.Add(new Vector2(corners[0].x,corners[0].y));
                list.Add(new Vector2(corners[0].x,corners[1].y));
                list.Add(new Vector2(corners[1].x,corners[1].y));
                list.Add(new Vector2(corners[1].x,corners[0].y));
                voronoi_cell_map.Add(0,list);
                
                return voronoi_cell_map;
            }
        }
        
        public static Vector2 getv1(PowerCell f){
            return new Vector2((f.A + (float)f.tmin * f.U).x, (f.A + (float)f.tmin * f.U).y);
        }
        public static Vector2 getv2(PowerCell f){
            return new Vector2((f.A + (float)f.tmax * f.U).x, (f.A + (float)f.tmax * f.U).y);
        }

    }
}