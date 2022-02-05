using UnityEngine;

public struct EdgeVertices {

    public Vector3 v1, v2, v3, v4, v5;

    public EdgeVertices(Vector3 c1, Vector3 c2) {
        v1 = c1;
        v2 = Vector3.Lerp(c1, c2, 0.25f);
        v3 = Vector3.Lerp(c1, c2, 0.5f);
        v4 = Vector3.Lerp(c1, c2, 0.75f);
        v5 = c2;
    }

    public EdgeVertices(Vector3 c1, Vector3 c2, float outerStep) {
        v1 = c1;
        v2 = Vector3.Lerp(c1, c2, outerStep);
        v3 = Vector3.Lerp(c1, c2, 0.5f);
        v4 = Vector3.Lerp(c1, c2, 1f - outerStep);
        v5 = c2;
    }

    public static EdgeVertices TerraceLerp(EdgeVertices a, EdgeVertices b, int step) {
        EdgeVertices result;
        result.v1 = HexMesh.TerraceLerp(a.v1, b.v1, step);
        result.v2 = HexMesh.TerraceLerp(a.v2, b.v2, step);
        result.v3 = HexMesh.TerraceLerp(a.v3, b.v3, step);
        result.v4 = HexMesh.TerraceLerp(a.v4, b.v4, step);
        result.v5 = HexMesh.TerraceLerp(a.v5, b.v5, step);
        return result;
    }

}