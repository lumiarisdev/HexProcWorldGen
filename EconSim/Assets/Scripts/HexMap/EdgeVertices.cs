using UnityEngine;

public struct EdgeVertices {

    public Vector3 v1, v2, v3, v4;

    public EdgeVertices(Vector3 c1, Vector3 c2) {
        v1 = c1;
        v2 = Vector3.Lerp(c1, c2, 1f / 3f);
        v3 = Vector3.Lerp(c1, c2, 2f / 3f);
        v4 = c2;
    }

    public static EdgeVertices TerraceLerp(EdgeVertices a, EdgeVertices b, int step) {
        EdgeVertices result;
        result.v1 = HexMesh.TerraceLerp(a.v1, b.v1, step);
        result.v2 = HexMesh.TerraceLerp(a.v2, b.v2, step);
        result.v3 = HexMesh.TerraceLerp(a.v3, b.v3, step);
        result.v4 = HexMesh.TerraceLerp(a.v4, b.v4, step);
        return result;
    }

}