using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EconSim;

public class HexMap : MonoBehaviour {

    public Color defaultColor = Color.white;

    public static Color[] colors = {
        Color.blue,
        Color.HSVToRGB((145f/360f), 0.77f, 0.80f),
        Color.grey,
        Color.HSVToRGB((30f/360f), 1f, 0.59f),
        Color.HSVToRGB((56f/360f), 0.89f, 0.96f),
        // debug1-3
        Color.red,
        Color.magenta,
        Color.HSVToRGB(291, 0.9f, 0.7f),
    };

    private int cellCountX;
    private int cellCountZ;
    public int chunkCountX;
    public int chunkCountZ;
    public HexMapChunk hexChunkPrefab;
    public HexMeshCell hexMeshCellPrefab;
    public Text cellLabelPrefab;

    HexMapChunk[] chunks;

    static Dictionary<CubeCoordinates, HexMeshCell> hexMeshCells = new Dictionary<CubeCoordinates, HexMeshCell>();
    WorldMap worldMap;

    public Texture2D noiseSource; // source for our vertex perturbation noise

    private void Awake() {

        HexMetrics.noiseSource = noiseSource;

        worldMap = GetComponent<WorldMap>();

        cellCountX = chunkCountX * HexMetrics.chunkSizeX;
        cellCountZ = chunkCountZ * HexMetrics.chunkSizeZ;
        //hexMeshCells = new Dictionary<CubeCoordinates, HexMeshCell>();
        chunks = new HexMapChunk[chunkCountX * chunkCountZ];

        //CreateChunks();
        //CreateCells();

    }

    private void OnEnable() {
        HexMetrics.noiseSource = noiseSource;
    }

    private void Start() {
        CreateChunks();
        CreateCells();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CreateCells() {

        for (int z = 0; z < cellCountZ; z++) {
            for (int x = 0; x < cellCountX; x++) {
                CreateCell(x, z);
            }
        }
    }

    public void CreateCell(int x, int z) {
        Vector3 pos;
        var cubeCoords = CubeCoordinates.OffsetToCube(new Vector3(x, 0, z));
        var worldTile = worldMap.worldTiles[cubeCoords];
        var width = 2f * HexMetrics.outerRadius;
        var height = Mathf.Sqrt(3) * HexMetrics.outerRadius;
        pos.x = x * (width * 0.75f);
        pos.y = worldTile.Elevation * HexMetrics.elevationStep; // take y as the worldElevation parameter
        pos.z = (z + x * 0.5f - x / 2) * (height);
        // NOTE: this perturbation was causing a bunch of issues with the Triangulation functions
        // This is because of the way they are deriving the edge types, which is ultimately based off of their world position
        // When we perturb the world position, it must be registering as a different elevation level and not properly
        // drawing the cell connections
        // DISABLED FOR NOW
        //pos.y += (HexMetrics.SampleNoise(pos).y * 2f - 1f) * HexMetrics.elevationPerturbStrength;

        HexMeshCell cell = hexMeshCells[cubeCoords] = Instantiate(hexMeshCellPrefab);
        cell.transform.localPosition = pos;
        cell.coordinates = cubeCoords;
        cell.color = colors[(int)worldTile.Terrain];
        cell.waterLevel = 0; // random water level, for testing lol

        Text label = Instantiate(cellLabelPrefab);
        // positioning for labels
        label.rectTransform.anchoredPosition = new Vector2(pos.x, pos.z);
        Vector3 uiPos = label.rectTransform.localPosition;
        uiPos.z = worldTile.Elevation * -HexMetrics.elevationStep;
        label.rectTransform.localPosition = uiPos;
        // draw labeel
        label.text = cubeCoords.ToStringOnSeparateLines();
        cell.label = label;

        AddCellToChunk(x, z, cell);
    }

    void AddCellToChunk(int x, int z, HexMeshCell cell) {
        int chunkX = x / HexMetrics.chunkSizeX;
        int chunkZ = z / HexMetrics.chunkSizeZ;
        HexMapChunk chunk = chunks[chunkX + chunkZ * chunkCountX];

        // cell.color = chunk.color; // SET CELL COLOR TO CHUNK COLOR
        chunk.AddCell(cell);
    }

    public void CreateChunks() {
        chunks = new HexMapChunk[chunkCountX * chunkCountZ];

        for(int z = 0, i = 0; z < chunkCountZ; z++) {
            for(int x = 0; x < chunkCountX; x++) {
                HexMapChunk chunk = chunks[i++] = Instantiate(hexChunkPrefab);
                chunk.transform.SetParent(transform);
            }
        }
    }

    public static HexMeshCell MeshCellLookup(CubeCoordinates c) {
        var exists = hexMeshCells.TryGetValue(c, out HexMeshCell cell);
        return exists ? cell : null;
    }

}
