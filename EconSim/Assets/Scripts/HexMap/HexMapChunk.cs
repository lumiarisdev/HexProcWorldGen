using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EconSim;
using UnityEngine.UI;

public class HexMapChunk : MonoBehaviour
{
    public Text cellLabelPrefab;
    Canvas canvas;
    public HexMesh terrainMesh, waterMesh;
    Dictionary<CubeCoordinates, HexMeshCell> cells;

    public Color color;

    public bool displayUI;

    public bool drawWater;

    private void Awake() {

        canvas = GetComponentInChildren<Canvas>();
        //terrainMesh = GetComponentInChildren<HexMesh>();

        cells = new Dictionary<CubeCoordinates, HexMeshCell>();
        ShowUI(displayUI);
        color = UnityEngine.Random.ColorHSV();

    }

    void Start()  {
        //var cellsArr = new HexMeshCell[cells.Count];
        //cells.Values.CopyTo(cellsArr, 0);
        //terrainMesh.Triangulate(cellsArr);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void LateUpdate() {
        Triangulate(cells);
        enabled = false;
    }

    public void AddCell(HexMeshCell cell) {
        cells[cell.coordinates] = cell;
        cell.transform.SetParent(transform, false);
        cell.label.transform.SetParent(canvas.transform, false);
        cell.chunk = this;
    }

    public HexMeshCell MeshCellLookup(CubeCoordinates c) {
        bool exists = cells.TryGetValue(c, out HexMeshCell cell);
        return exists ? cell : null;
    }

    // IMPLEMENT LATER
    public void Refresh() {
        enabled = true;
    }

    public void ShowUI(bool visible) {
        canvas.gameObject.SetActive(visible);
    }

    public void Triangulate(Dictionary<CubeCoordinates, HexMeshCell> cells) {
        terrainMesh.Clear();
        if(drawWater) {
            waterMesh.Clear();
        }
        foreach (HexMeshCell cell in cells.Values) {
            Triangulate(cell);
        }
        terrainMesh.Apply();
        if(drawWater) {
            waterMesh.Apply();
        }
    }

    /*
     * Triangulates a single cell and its connections to its neighbors
     * NOTE:
     * Each cell is composed of an inner hexagon, and a ring of quads/triangles that make up the outer hexagon,
     * as well as connect the cell to its neighbors.
     */
    private void Triangulate(HexMeshCell cell) {
        for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
            Triangulate(d, cell);
        }
    }

    /*
     * Triangulates the main area of the hexagon and calls the remaining triangulate functions.
     */
    private void Triangulate(HexDirection dir, HexMeshCell cell) {
        Vector3 origin = cell.transform.localPosition; // could be cell.origin, refactor later ???
        // all our vertices for the inner triangle fans
        // each "triangle" of the hexagon is actually a fan of 3 triangles
        EdgeVertices e = new EdgeVertices(
            HexMetrics.GetInnerCorner(origin, (int)dir),
            HexMetrics.GetInnerCorner(origin, (int)dir + 1)
            );

        TriangulateEdgeFan(origin, e, cell.color);

        // connection/bridge (this is a quad and multiple triangles that make up the outer hexagon ring)
        if (dir <= HexDirection.SE) {
            TriangulateConnection(dir, cell, e);
        }

        if(drawWater) {
            if (cell.IsUnderwater) {
                TriangulateWater(dir, cell, origin);
            }
        }

    }

    /*
     * Triangulates the connection between cells
     */
    private void TriangulateConnection(HexDirection dir, HexMeshCell cell, EdgeVertices e1) {
        var origin = cell.transform.localPosition;
        // bail if there's no neighbor, so we dont create a connection
        HexMeshCell neighbor = HexMap.MeshCellLookup(cell.coordinates.GetNeighbor(dir));
        if (neighbor == null) {
            return;
        }

        // create bridge quad and blend colors
        Vector3 bridgeOffset = HexMetrics.GetBridgeOffset(origin, dir);
        bridgeOffset.y = neighbor.transform.localPosition.y - cell.transform.localPosition.y;
        EdgeVertices e2 = new EdgeVertices(
            e1.v1 + bridgeOffset,
            e1.v4 + bridgeOffset
            );
        //v3.y = v4.y = neighbor.transform.localPosition.y; // override height of far end of bridge to match neighbor

        // only draw terraces on connections if it is a slope
        if (cell.GetEdgeType(dir) == HexEdgeType.Slope) {
            TriangulateEdgeTerraces(e1, cell, e2, neighbor);
        }
        else { // otherwise, draw cliffs / flats
            TriangulateEdgeStrip(e1, cell.color, e2, neighbor.color);
        }

        // create next neighbor triangle and blend colors
        HexMeshCell nextNeighbor = HexMap.MeshCellLookup(cell.coordinates.GetNeighbor(dir.Next()));
        if (dir <= HexDirection.NE && nextNeighbor != null) {
            Vector3 v5 = e1.v4 + HexMetrics.GetBridgeOffset(origin, dir.Next());
            v5.y = nextNeighbor.transform.localPosition.y; // override height to match nextNeighbor

            // determine lowest cell
            int cellStep = (int)(cell.transform.localPosition.y / HexMetrics.elevationStep);
            int neighborStep = (int)(neighbor.transform.localPosition.y / HexMetrics.elevationStep);
            int nextNeighborStep = (int)(nextNeighbor.transform.localPosition.y / HexMetrics.elevationStep);
            // the lowest cell is used to determine the draw order of the vertices
            if (cellStep <= neighborStep) {
                if (cellStep <= nextNeighborStep) {
                    TriangulateCorner(e1.v4, cell, e2.v4, neighbor, v5, nextNeighbor);
                }
                else {
                    TriangulateCorner(v5, nextNeighbor, e1.v4, cell, e2.v4, neighbor);
                }
            }
            else if (neighborStep <= nextNeighborStep) {
                TriangulateCorner(e2.v4, neighbor, v5, nextNeighbor, e1.v4, cell);
            }
            else {
                TriangulateCorner(v5, nextNeighbor, e1.v4, cell, e2.v4, neighbor);
            }
        }
    }

    /*
     * Triangulates the terraces on tile bridges
     */
    private void TriangulateEdgeTerraces(EdgeVertices begin, HexMeshCell beginCell,
        EdgeVertices end, HexMeshCell endCell) {
        // lerp to get first step
        EdgeVertices e2 = EdgeVertices.TerraceLerp(begin, end, 1);
        Color c2 = HexMesh.TerraceLerp(beginCell.color, endCell.color, 1);

        // first step
        TriangulateEdgeStrip(begin, beginCell.color, e2, c2);

        // intermediary steps
        for (int i = 2; i < HexMetrics.terraceSteps; i++) {
            // last coords/color become first
            EdgeVertices e1 = e2;
            Color c1 = c2;
            // lerp to get next step
            e2 = EdgeVertices.TerraceLerp(begin, end, i);
            c2 = HexMesh.TerraceLerp(beginCell.color, endCell.color, i);
            // add step
            TriangulateEdgeStrip(e1, c1, e2, c2);
        }

        // last step
        TriangulateEdgeStrip(e2, c2, end, endCell.color);
    }

    /*
     * Triangulates the corners of cell connections
     */
    private void TriangulateCorner(Vector3 v1, HexMeshCell v1Cell, Vector3 v2,
        HexMeshCell v2Cell, Vector3 v3, HexMeshCell v3Cell) {
        HexEdgeType v2EdgeType = v1Cell.GetEdgeType(v2Cell);
        HexEdgeType v3EdgeType = v1Cell.GetEdgeType(v3Cell);

        // shift vertices based on which cell is lowest and edge types
        if (v2EdgeType == HexEdgeType.Slope) {
            if (v3EdgeType == HexEdgeType.Slope) {
                TriangulateCornerTerraces(v1, v1Cell, v2, v2Cell, v3, v3Cell);
            }
            else if (v3EdgeType == HexEdgeType.Flat) {
                TriangulateCornerTerraces(v2, v2Cell, v3, v3Cell, v1, v1Cell);
                return;
            }
            else {
                TriangulateCornerTerracesCliff(v1, v1Cell, v2, v2Cell, v3, v3Cell);
            }
        }
        else if (v3EdgeType == HexEdgeType.Slope) {
            if (v2EdgeType == HexEdgeType.Flat) {
                TriangulateCornerTerraces(v3, v3Cell, v1, v1Cell, v2, v2Cell);
            }
            else {
                TriangulateCornerCliffTerraces(v1, v1Cell, v2, v2Cell, v3, v3Cell);
            }
        }
        else if (v2Cell.GetEdgeType(v3Cell) == HexEdgeType.Slope) {
            if ((int)(v2Cell.transform.localPosition.y / HexMetrics.elevationStep) < (int)(v3Cell.transform.localPosition.y / HexMetrics.elevationStep)) {
                TriangulateCornerCliffTerraces(v3, v3Cell, v1, v1Cell, v2, v2Cell);
            }
            else {
                TriangulateCornerTerracesCliff(v2, v2Cell, v3, v3Cell, v1, v1Cell);
            }
        }
        else {
            terrainMesh.AddTrianglePerturbed(v1, v2, v3);
            terrainMesh.AddTriangleColor(v1Cell.color, v2Cell.color, v3Cell.color);
        }
    }

    /*
     * Triangulates corners with terraces
     */
    private void TriangulateCornerTerraces(Vector3 begin, HexMeshCell beginCell, Vector3 left,
        HexMeshCell leftCell, Vector3 right, HexMeshCell rightCell) {
        // lerp to get first terrace
        Vector3 v3 = HexMesh.TerraceLerp(begin, left, 1);
        Vector3 v4 = HexMesh.TerraceLerp(begin, right, 1);
        Color c3 = HexMesh.TerraceLerp(beginCell.color, leftCell.color, 1);
        Color c4 = HexMesh.TerraceLerp(beginCell.color, rightCell.color, 1);

        // first step is a triangle, all the rest are quads
        terrainMesh.AddTrianglePerturbed(begin, v3, v4);
        terrainMesh.AddTriangleColor(beginCell.color, c3, c4);

        // intermediary steps
        for (int i = 2; i < HexMetrics.terraceSteps; i++) {
            Vector3 v1 = v3;
            Vector3 v2 = v4;
            Color c1 = c3;
            Color c2 = c4;
            v3 = HexMesh.TerraceLerp(begin, left, i);
            v4 = HexMesh.TerraceLerp(begin, right, i);
            c3 = HexMesh.TerraceLerp(beginCell.color, leftCell.color, i);
            c4 = HexMesh.TerraceLerp(beginCell.color, rightCell.color, i);
            terrainMesh.AddQuadPerturbed(v1, v2, v3, v4);
            terrainMesh.AddQuadColor(c1, c2, c3, c4);
        }

        //last step
        terrainMesh.AddQuadPerturbed(v3, v4, left, right);
        terrainMesh.AddQuadColor(c3, c4, leftCell.color, rightCell.color);
    }

    /*
     * Triangulates corner terraces for the Slope-Cliff-Slope and Slope-Cliff-Cliff cell arrangements
     */
    private void TriangulateCornerTerracesCliff(Vector3 begin, HexMeshCell beginCell, Vector3 left,
        HexMeshCell leftCell, Vector3 right, HexMeshCell rightCell) {
        //float b = 1f / (leftCell.transform.localPosition.y - beginCell.transform.localPosition.y);
        Vector3 boundary = Vector3.Lerp(HexMesh.Perturb(begin), HexMesh.Perturb(right), 0.5f); //(begin + right) / 2f;
        Color boundaryColor = Color.Lerp(beginCell.color, rightCell.color, 0.5f);

        TriangulateBoundaryTriangle(begin, beginCell, left, leftCell, boundary, boundaryColor);

        // if theres a slope, add another rotated boundary triangle, otherwise, draw a simple triangle
        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope) {
            TriangulateBoundaryTriangle(left, leftCell, right, rightCell, boundary, boundaryColor);
        }
        else {
            terrainMesh.AddTriangle(HexMesh.Perturb(left), HexMesh.Perturb(right), boundary);
            terrainMesh.AddTriangleColor(leftCell.color, rightCell.color, boundaryColor);
        }
    }

    /*
     * The same as TriangulateCornerTerracesCliff(), but for its mirror (opposite) cases
     */
    private void TriangulateCornerCliffTerraces(Vector3 begin, HexMeshCell beginCell, Vector3 left,
         HexMeshCell leftCell, Vector3 right, HexMeshCell rightCell) {
        //float b = 1f / (leftCell.transform.localPosition.y - beginCell.transform.localPosition.y);
        Vector3 boundary = Vector3.Lerp(HexMesh.Perturb(begin), HexMesh.Perturb(left), 0.5f); //(begin + left) / 2f;
        Color boundaryColor = Color.Lerp(beginCell.color, leftCell.color, 0.5f);

        TriangulateBoundaryTriangle(right, rightCell, begin, beginCell, boundary, boundaryColor);

        // if theres a slope, add another rotated boundary triangle, otherwise, draw a simple triangle
        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope) {
            TriangulateBoundaryTriangle(left, leftCell, right, rightCell, boundary, boundaryColor);
        }
        else {
            terrainMesh.AddTriangle(HexMesh.Perturb(left), HexMesh.Perturb(right), boundary);
            terrainMesh.AddTriangleColor(leftCell.color, rightCell.color, boundaryColor);
        }
    }

    /*
     * Triangulates the boundary triangles, or the triangles that merge slopes and terraces
     */
    private void TriangulateBoundaryTriangle(Vector3 begin, HexMeshCell beginCell, Vector3 left,
        HexMeshCell leftCell, Vector3 boundary, Color boundaryColor) {
        // first step
        Vector3 v2 = HexMesh.Perturb(HexMesh.TerraceLerp(begin, left, 1));
        Color c2 = HexMesh.TerraceLerp(beginCell.color, leftCell.color, 1);

        terrainMesh.AddTriangle(HexMesh.Perturb(begin), v2, boundary);
        terrainMesh.AddTriangleColor(beginCell.color, c2, boundaryColor);

        // intermediary steps
        for (int i = 2; i < HexMetrics.terraceSteps; i++) {
            Vector3 v1 = v2;
            Color c1 = c2;
            v2 = HexMesh.Perturb(HexMesh.TerraceLerp(begin, left, i));
            c2 = HexMesh.TerraceLerp(beginCell.color, leftCell.color, i);
            terrainMesh.AddTriangle(v1, v2, boundary);
            terrainMesh.AddTriangleColor(c1, c2, boundaryColor);
        }

        //last step
        terrainMesh.AddTriangle(v2, HexMesh.Perturb(left), boundary);
        terrainMesh.AddTriangleColor(c2, leftCell.color, boundaryColor);
    }

    /*
     * Triangulation for the fan of triangles from the center of a hex to one of its edges.
     */
    private void TriangulateEdgeFan(Vector3 origin, EdgeVertices edge, Color color) {
        terrainMesh.AddTrianglePerturbed(origin, edge.v1, edge.v2);
        terrainMesh.AddTriangleColor(color);
        terrainMesh.AddTrianglePerturbed(origin, edge.v2, edge.v3);
        terrainMesh.AddTriangleColor(color);
        terrainMesh.AddTrianglePerturbed(origin, edge.v3, edge.v4);
        terrainMesh.AddTriangleColor(color);
    }

    /*
     * Triangulation for the strip of quads between two edges of hexes.
     */
    private void TriangulateEdgeStrip(EdgeVertices e1, Color c1, EdgeVertices e2, Color c2) {
        terrainMesh.AddQuadPerturbed(e1.v1, e1.v2, e2.v1, e2.v2);
        terrainMesh.AddQuadColor(c1, c2);
        terrainMesh.AddQuadPerturbed(e1.v2, e1.v3, e2.v2, e2.v3);
        terrainMesh.AddQuadColor(c1, c2);
        terrainMesh.AddQuadPerturbed(e1.v3, e1.v4, e2.v3, e2.v4);
        terrainMesh.AddQuadColor(c1, c2);
    }

    /*
     * Triangulate water (oceans, rivers, lakes) on tiles
     * Much simpler set of polyons, a basic hex and connections
     */
    private void TriangulateWater(HexDirection dir, HexMeshCell cell, Vector3 origin) {
        origin.y = cell.WaterSurfaceY;
        Vector3 c1 = HexMetrics.GetInnerCorner(origin, (int)dir);
        Vector3 c2 = HexMetrics.GetInnerCorner(origin, (int)dir + 1);

        waterMesh.AddTrianglePerturbed(origin, c1, c2);

        if(dir <= HexDirection.SE) {
            HexMeshCell neighbor = HexMap.MeshCellLookup(cell.coordinates.GetNeighbor(dir));
            if (neighbor == null || !neighbor.IsUnderwater) {
                return;
            }

            Vector3 bridgeOffset = HexMetrics.GetBridgeOffset(origin, dir);
            Vector3 e1 = c1 + bridgeOffset;
            Vector3 e2 = c2 + bridgeOffset;

            waterMesh.AddQuadPerturbed(c1, c2, e1, e2);

            if(dir <= HexDirection.S) {
                HexMeshCell nextNeighbor = HexMap.MeshCellLookup(cell.coordinates.GetNeighbor(dir.Next()));
                if(nextNeighbor == null || !nextNeighbor.IsUnderwater) {
                    return;
                }
                waterMesh.AddTrianglePerturbed(c2, e2, c2 + HexMetrics.GetBridgeOffset(origin, dir));
            }
        }
    }

}
