using MathNet.Numerics.LinearAlgebra;



public class Embedding : List<double>
{
    public Embedding() { }

    public Embedding(IEnumerable<double> data)
    {
        AddRange(data);
    }

    public Vector<double> Vector => Vector<double>.Build.DenseOfArray(ToArray());

    public double CosineSimilarity(Embedding compareTo)
    {
        return CosineSimilarity(Vector, compareTo.Vector);
    }

    private static double CosineSimilarity(Vector<double> v1, Vector<double> v2)
    {
        return v1.DotProduct(v2) / (v1.L2Norm() * v2.L2Norm());
    }
}