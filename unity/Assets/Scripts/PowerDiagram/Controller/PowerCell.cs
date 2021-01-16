namespace PowerDiagram
{
    using System;
    using UnityEngine;
    public struct PowerCell
    {
        public PowerCell(int a, int b, Vector2 c, Vector2 d, double e, double f)
        {
            i = a;
            j = b;
            A = c;
            U = d;
            tmin = e;
            tmax = f;
        }
        
        public int i { get; set; }
        public int j { get; set; }
        public Vector2 A { get; set; }
        public Vector2 U { get; set; }
        public double tmin { get; set; }
        public double tmax { get; set; }
    }
}
