using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EconSim;
using System;

[Serializable]
public enum DebugMode
{
    None,
    Tectonics,
    Temperature,
    Wind,
    Humidity,
    Precipitation,
}

public class HexMap : MonoBehaviour {

    public static HexMap Instance;

    public Color defaultColor = Color.white;

    public DebugMode debugMode;

    public static Color[] colors = {
        Color.blue,
        Color.HSVToRGB((107f/360f), 0.66f, 0.62f),
        Color.grey,
        Color.HSVToRGB((30f/360f), 1f, 0.59f),
        Color.HSVToRGB((56f/360f), 0.89f, 0.96f),
        // debug1-3
        Color.red,
        Color.magenta,
        Color.HSVToRGB(291, 0.9f, 0.7f),
        Color.cyan,
        Color.white,
    };

    public HexMapChunk hexChunkPrefab;
    public HexMeshCell hexMeshCellPrefab;
    public Text cellLabelPrefab;

    List<HexMapChunk> chunks;

    static Dictionary<CubeCoordinates, HexMeshCell> hexMeshCells = new Dictionary<CubeCoordinates, HexMeshCell>();
    WorldMap worldMap;

    public Texture2D noiseSource; // source for our vertex perturbation noise

    private IEnumerator currentCoroutine;

    private void Awake() {

        Instance = this;

        HexMetrics.noiseSource = noiseSource;

        worldMap = GetComponent<WorldMap>();

        //hexMeshCells = new Dictionary<CubeCoordinates, HexMeshCell>();

    }

    private void OnEnable() {
        HexMetrics.noiseSource = noiseSource;
    }

    private void Start() {
        CreateChunks();
        CreateCells(worldMap.worldData);
    }

    // Update is called once per frame
    void Update()
    {
        
        /*
         * Let's handle some input and map updates right here for now.
         * This will require a lot of refactoring and cleaning up.
         */

        if(Input.GetKey(KeyCode.H)) {
            //Ray inputRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            //RaycastHit hit;
            //if(Physics.Raycast(inputRay, out hit)) {

            //}
            //var mPos = Input.mousePosition;
            //mPos.x /= (2f * HexMetrics.outerRadius * 0.75f);
            //mPos.z = mPos.y;
            //mPos.z /= Mathf.Sqrt(3) * HexMetrics.outerRadius;
            //var mCubePos = CubeCoordinates.OffsetToCube(mPos);
            //Debug.Log("V: " + mPos + " | C: " + mCubePos);

            //if(worldMap.worldTiles.TryGetValue(mCubePos, out WorldTile _)) {
            //    worldMap.worldTiles[mCubePos].Terrain = TerrainType.Ocean;
            //    UpdateCell(mCubePos);
            //}


            Refresh();

        }

    }

    /*
     * Refresh the entire map
     * 
     * Testing this as a coroutine to make the experience a little smoother
     * 
     */
    public void Refresh() {
        if (chunks.Count < worldMap.generatorArgs.SizeChunksX * worldMap.generatorArgs.SizeChunksZ) {
            // BUG: need to delete old chunks
            // attempted fix
            foreach (HexMapChunk chunk in chunks) {
                Destroy(chunk);
            }
            CreateChunks();
        }
        foreach (WorldTile tile in worldMap.worldData.WorldDict.Values) {
            if (MeshCellLookup(tile.Coordinates) != null) {
                UpdateCell(worldMap.worldData, tile);
            }
            else {
                hexMeshCells[tile.Coordinates] = CreateCell(worldMap.worldData, tile);

                var x = tile.Coordinates.ToOffset().x;
                var z = tile.Coordinates.ToOffset().z;


                AddCellToChunk(x, z, hexMeshCells[tile.Coordinates]);
            }
        }
    }

    /*
     * Update a cell based on the current state of its sister tile.
     */
    public void UpdateCell(WorldMapData wData, WorldTile tile) {
        if (debugMode == DebugMode.None) {
            // normal terrain
            hexMeshCells[tile.Coordinates].color = colors[(int)tile.Terrain];
        }
        else if (debugMode == DebugMode.Tectonics) {
            if (tile.PlateCoords.Equals(tile.Coordinates)) {
                hexMeshCells[tile.Coordinates].color = colors[6];
            }
            else {
                foreach (WorldPlate plate in wData.PlateDict.Values) {
                    foreach (CubeCoordinates c in plate.BoundaryTiles) {
                        if (c.Equals(tile.Coordinates)) {
                            hexMeshCells[tile.Coordinates].color = colors[7];
                            goto BFOUND;
                        }
                    }
                }
                hexMeshCells[tile.Coordinates].color = colors[5];
            }
        BFOUND:;
        }
        else if (debugMode == DebugMode.Temperature) {
            if (worldMap.worldData.WorldDict[tile.Coordinates].Temperature == 30f) {
                hexMeshCells[tile.Coordinates].color = colors[7];
            }
            else {
                hexMeshCells[tile.Coordinates].color = Color.Lerp(Color.blue, Color.red, (worldMap.worldData.WorldDict[tile.Coordinates].Temperature - -40f) / (30f - -40f));
            }
        }

        Vector3Int offset = tile.Coordinates.ToOffset();
        var width = 2f * HexMetrics.outerRadius;
        var height = Mathf.Sqrt(3) * HexMetrics.outerRadius;
        Vector3 pos = new Vector3(
            offset.x * (width * 0.75f),
            worldMap.worldData.WorldDict[tile.Coordinates].Elevation * HexMetrics.elevationStep,
            (offset.z + (offset.x * 0.5f) - (offset.x / 2)) * (height));

        hexMeshCells[tile.Coordinates].transform.localPosition = pos;

        // LABELS WILL NOT UPDATE
        // TODO: ADD LABEL UPDATES

        hexMeshCells[tile.Coordinates].Refresh();
    }

    /*
     * Create the grid of hex mesh cells from a WorldTile dictionary.
     * Should only be run after CreateChunks()
     */
    public void CreateCells(WorldMapData wData) {
        foreach(WorldTile tile in wData.WorldDict.Values) {
            hexMeshCells[tile.Coordinates] = CreateCell(wData, tile);
            AddCellToChunk(tile.Coordinates.ToOffset().x, tile.Coordinates.ToOffset().z, hexMeshCells[tile.Coordinates]);
        }
    }

    /*
     * Create a HexMeshTile from an input WorldTile and offset hex coordinates.
     */
    public HexMeshCell CreateCell(WorldMapData wData, WorldTile wTile) {
        Vector3Int offset = wTile.Coordinates.ToOffset(); // when this is a Vector3, there are a lot of problems. TODO: investigate
        var width = 2f * HexMetrics.outerRadius;
        var height = Mathf.Sqrt(3) * HexMetrics.outerRadius;
        Vector3 pos = new Vector3(
            offset.x * (width * 0.75f),
            wTile.Elevation * HexMetrics.elevationStep,
            (offset.z + (offset.x * 0.5f) - (offset.x / 2)) * (height));
        pos.y = pos.y + (HexMetrics.SampleNoise(pos).y * 2f - 1f);

        // TODO: elevation perturbation, could also be achieved elsewhere
        //pos.y += (HexMetrics.SampleNoise(pos).y * 2f - 1f) * HexMetrics.elevationPerturbStrength;

        HexMeshCell cell = Instantiate(hexMeshCellPrefab);
        cell.transform.localPosition = pos;
        cell.coordinates = wTile.Coordinates;

        // initial coloring, based on debug modes and worldtile attributes
        if (debugMode == DebugMode.None) {
            // normal terrain
            cell.color = colors[(int)wTile.Terrain];
        }
        else if (debugMode == DebugMode.Tectonics) {
            if(wTile.PlateCoords.Equals(wTile.Coordinates)) {
                cell.color = colors[6];
            } else {
                foreach (WorldPlate plate in wData.PlateDict.Values) {
                    foreach (CubeCoordinates c in plate.BoundaryTiles) {
                        if (c.Equals(wTile.Coordinates)) {
                            cell.color = colors[7];
                            goto BFOUND;
                        }
                    }
                }
                cell.color = colors[5];
            }
        BFOUND:;
        }
        else if(debugMode == DebugMode.Temperature) {
            if (wTile.Temperature == 30f) {
                cell.color = colors[7];
            }
            else {
                cell.color = Color.Lerp(Color.blue, Color.red, (wTile.Temperature - -40f) / (30f - -40f));
            }
        }

        // water level, will need changes later
        cell.waterLevel = 0;


        // text label
        cell.label = CreateCellLabel(pos, wTile);

        return cell;
    }

    /*
     * Create text label for cells with Cube Coordinates.
     */
    public Text CreateCellLabel(Vector3 pos, WorldTile wTile) {
        Text label = Instantiate(cellLabelPrefab);
        label.rectTransform.anchoredPosition = new Vector2(pos.x, pos.z);
        Vector3 uiPos = label.rectTransform.localPosition;
        uiPos.z = wTile.Elevation * -HexMetrics.elevationStep;
        label.rectTransform.localPosition = uiPos;
        label.text = wTile.Coordinates.ToStringOnSeparateLines();
        return label;
    }

    void AddCellToChunk(int x, int z, HexMeshCell cell) {
        int chunkX = x / HexMetrics.chunkSizeX;
        int chunkZ = z / HexMetrics.chunkSizeZ;
        HexMapChunk chunk = chunks[chunkX + chunkZ * worldMap.generatorArgs.SizeChunksX];

        // cell.color = chunk.color; // SET CELL COLOR TO CHUNK COLOR
        chunk.AddCell(cell);
    }

    public void CreateChunks() {
        chunks = new List<HexMapChunk>();
        for (int z = 0, i = 0; z < worldMap.generatorArgs.SizeChunksZ; z++) {
            for(int x = 0; x < worldMap.generatorArgs.SizeChunksX; x++) {
                HexMapChunk chunk = Instantiate(hexChunkPrefab);
                chunks.Add(chunk);
                chunk.transform.SetParent(transform);
            }
        }
    }

    public static HexMeshCell MeshCellLookup(CubeCoordinates c) {
        var exists = hexMeshCells.TryGetValue(c, out HexMeshCell cell);
        return exists ? cell : null;
    }

}
