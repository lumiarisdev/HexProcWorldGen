using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EconSim;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMesh : MonoBehaviour
{

    Mesh mesh;
    MeshCollider meshCollider;

    [NonSerialized] List<Vector3> vertices;
    [NonSerialized] List<int> triangles;
    [NonSerialized] List<Color> colors;
    [NonSerialized] List<Vector2> uvs;

    public bool useCollider, useColors, useUVCoordinates;

    private void Awake() {

        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        if(useCollider) {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        mesh.name = "mesh";

    }   

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Clear() {
        mesh.Clear();
        vertices = ListPool<Vector3>.Get();
        if(useColors) {
            colors = ListPool<Color>.Get();
        }
        if(useUVCoordinates) {
            uvs = ListPool<Vector2>.Get();
        }
        triangles = ListPool<int>.Get();
    }

    public void Apply() {
        mesh.SetVertices(vertices);
        ListPool<Vector3>.Add(vertices);
        if(useColors) {
            mesh.SetColors(colors);
            ListPool<Color>.Add(colors);
        }
        if(useUVCoordinates) {
            mesh.SetUVs(0, uvs);
            ListPool<Vector2>.Add(uvs);
        }
        mesh.SetTriangles(triangles, 0);
        ListPool<int>.Add(triangles);
        mesh.RecalculateNormals();
        if(useCollider) {
            meshCollider.sharedMesh = mesh;
        }
    }

    public void AddTrianglePerturbed(Vector3 v1, Vector3 v2, Vector3 v3) {
        AddTriangle(Perturb(v1), Perturb(v2), Perturb(v3));
    }

    public void AddTriangle(Vector3 v1, Vector3 v2, Vector3 v3) {
        int vertexIndex = vertices.Count;
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 2);
    }

    public void AddTriangleColor(Color c) {
        AddTriangleColor(c, c, c);
    }

    public void AddTriangleColor(Color c1, Color c2, Color c3) {
        colors.Add(c1);
        colors.Add(c2);
        colors.Add(c3);
    }

    public void AddTriangleUV(Vector2 uv1, Vector2 uv2, Vector2 uv3) {
        uvs.Add(uv1);
        uvs.Add(uv2);
        uvs.Add(uv3);
    }

    public void AddQuadPerturbed(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4) {
        AddQuad(Perturb(v1), Perturb(v2), Perturb(v3), Perturb(v4));
    }

    public void AddQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4) {
        int vertexIndex = vertices.Count;
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);
        vertices.Add(v4);
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 3);
    }

    public void AddQuadColor(Color c1, Color c2) {
        AddQuadColor(c1, c1, c2, c2);
    }

    public void AddQuadColor(Color c1, Color c2, Color c3, Color c4) {
        colors.Add(c1);
        colors.Add(c2);
        colors.Add(c3);
        colors.Add(c4);
    }
    
    public void AddQuadUV(float uMin, float uMax, float vMin, float vMax) {
        AddQuadUV(new Vector2(uMin, vMin), new Vector2(uMax, vMin),
            new Vector2(uMin, vMax), new Vector2(uMax, vMax));
    }
    
    public void AddQuadUV(Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4) {
        uvs.Add(uv1);
        uvs.Add(uv2);
        uvs.Add(uv3);
        uvs.Add(uv4);
    }

    /*
     * SOME STATIC HELPER FUNCTIONS
     */

    /*
     * Linear interpolation for the terrace steps, both Vector3s and colors
     */
    public static Vector3 TerraceLerp(Vector3 a, Vector3 b, int step) {
        float h = step * HexMetrics.horizontalTerraceStepSize;
        a.x += (b.x - a.x) * h;
        a.z += (b.z - a.z) * h;
        float v = ((step + 1) / 2) * HexMetrics.verticalTerraceStepSize;
        a.y += (b.y - a.y) * v;
        return a;
    }

    public static Color TerraceLerp(Color a, Color b, int step) {
        float h = step * HexMetrics.horizontalTerraceStepSize;
        return Color.Lerp(a, b, h);
    }

    public static Vector3 Perturb(Vector3 pos) {
        Vector4 sample = HexMetrics.SampleNoise(pos);
        pos.x += (sample.x * 2f - 1f) * HexMetrics.cellPerturbStrength;
        //pos.y += (sample.y * 2f - 1f);
        pos.z += (sample.z * 2f - 1f) * HexMetrics.cellPerturbStrength;
        return pos;
    }

}
