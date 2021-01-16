namespace PowerDiagram
{
    using MIConvexHull;
    public struct VVertex : IVertex
    {
        public VVertex(int i, double x, double y, double z)
        {
            ind = i;
            Position = new double[3];
            Position[0] = x;
            Position[1] = y;
            Position[2] = z;
        }
        
        public int ind { get; set; }

        public double[] Position { get; set; }
    }
}
