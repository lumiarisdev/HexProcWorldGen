using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EconSim;
using UnityEngine.UI;

public class HexMeshCell : MonoBehaviour
{

    public CubeCoordinates coordinates;
    public Vector3 origin;
    public HexMapChunk chunk;
    public Vector3[] corners;
    public Text label;
    public Color color;

    public float waterLevel;
    // true is in, out is false
    public Dictionary<HexDirection, bool> rivers;

    public bool HasIncomingRiver {
        get {
            foreach(bool r in rivers.Values) {
                if (r) return r;
            }
            return false;
        }
    }

    public bool HasOutgoingRiver {
        get {
            foreach(bool r in rivers.Values) {
                if (!r) return true;
            }
            return false;
        }
    }

    public bool HasRiverThroughEdge(HexDirection d) {
        return rivers.TryGetValue(d, out bool _);
    }

    public bool IsUnderwater {
        get {
            return waterLevel > Mathf.RoundToInt(transform.localPosition.y / HexMetrics.elevationStep);
        }
    }

    public float WaterSurfaceY {
        get {
            return (waterLevel + (HexMetrics.waterElevationOffset * HexMetrics.elevationStep));
        }
    }

    public float StreamBedY {
        get {
            return transform.localPosition.y + (HexMetrics.streamBedElevationOffset * HexMetrics.elevationStep);
        }
    }

    public float RiverSurfaceY {
        get {
            return (transform.localPosition.y + HexMetrics.riverSurfaceElevationOffset);
        }
    }

    private void Awake() {

        //origin = transform.localPosition;
        //corners = new Vector3[7]; // 7, to allow for wrapping (last element and the first are the same)

        //for(int i = 0; i < 6; i++) {
        //    corners[i] = HexMetrics.GetCorner(origin, HexMetrics.outerRadius, i);
        //}
        //corners[6] = HexMetrics.GetCorner(origin, HexMetrics.outerRadius, 0);

    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // we need to call this to update the cells, at some point, lol
    public void Refresh() {
        if(chunk) {
            chunk.Refresh();
            for(int i = 0; i < 5; i++) {
                HexMeshCell neighbor = HexMap.MeshCellLookup(coordinates.GetNeighbor((HexDirection)i));
                if(neighbor != null && neighbor.chunk != chunk) {
                    neighbor.chunk.Refresh();
                }
            }
        }
    }

    // helper extension of HexMetrics.GetEdgeType()
    // should only be used when you know there is a neighbor, ie not on a border edge
    public HexEdgeType GetEdgeType(HexDirection dir) {
        return HexMetrics.GetEdgeType(transform.localPosition.y,
            HexMap.MeshCellLookup(coordinates.GetNeighbor(dir)).transform.localPosition.y);
    }

    public HexEdgeType GetEdgeType(HexMeshCell cell) {
        return HexMetrics.GetEdgeType(transform.localPosition.y, cell.transform.localPosition.y);
    }

    // helper extension of CubeCoordinates.GetNeighbor()
    public CubeCoordinates GetNeighbor(HexDirection dir) {
        return coordinates.GetNeighbor(dir);
    }

}
