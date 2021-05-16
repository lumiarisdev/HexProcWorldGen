using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EconSim;

public enum HexEdgeType {
    Flat, Slope, Cliff
}

// flat top hexagon metrics, in relation to Vector3 unity coords and units
public static class HexMetrics {

    public const float outerRadius = 7.5f;
    public const float innerRadius = outerRadius * 0.866025404f;
    public const float cornerAngleRadStep = Mathf.PI / 180 * 60;

    public const int chunkSizeX = 5;
    public const int chunkSizeZ = 5;

    public const float solidFactor = 0.8f; // determines inner hexagon size
    public const float blendFactor = 1f - solidFactor;

    public const float elevationStep = 2f;
    public const int terracesPerSlope = 2;
    public const int terraceSteps = terracesPerSlope * 2 + 1;
    public const float horizontalTerraceStepSize = 1f / terraceSteps;
    public const float verticalTerraceStepSize = 1f / (terracesPerSlope + 1);

    public static Texture2D noiseSource;
    public const float cellPerturbStrength = 3f;
    public const float elevationPerturbStrength = 1f;
    public const float noiseScale = 0.004f;

    public const float waterElevationOffset = -0.5f;

    /*
     * Returns a 4D vector containing 4 noise samples in correspondance with a world position
     */
    public static Vector4 SampleNoise(Vector3 pos) {
        return noiseSource.GetPixelBilinear(pos.x * noiseScale, pos.z * noiseScale);
    }

    /*
     * GetEdgeType(float, float) is used to get the edge type between two y values in world space
     * It uses the elevationStep constant to derive the WorldMap elevation level, determining the edge type from that
     */
    public static HexEdgeType GetEdgeType(float y1, float y2) {
        int y1Step = Mathf.RoundToInt(y1 / elevationStep);
        int y2Step = Mathf.RoundToInt(y2 / elevationStep);
        if(y1Step == y2Step) {
            return HexEdgeType.Flat;
        }
        int delta = y2Step - y1Step;
        if(delta == 1 || delta == -1) {
            return HexEdgeType.Slope;
        }
        return HexEdgeType.Cliff;
    }

    /*
     * GetCorner function is used for a lot of things, so some important notes:
     * The index is shifted by +4 when the function starts. This is to align the calculation
     * so that when index = 0, the north direction's first corner is grabbed.
     * The opposite of the calculated angle from the origin is used so that the calculation
     * gets corners moving in the clockwise direction. This is done to make drawing triangles
     * simpler, as their winding order is clockwise.
     * We also use a while statement to wrap the lookups, so that you can enter in any index
     * and it will correspond to 0-5, repeating infinitely.
     */
    public static Vector3 GetCorner(Vector3 origin, int index) {
        return GetCorner(origin, index, outerRadius);
    }

    /*
     * USAGE:
     * For getting the corners of the inner hexagon used for blending regions/etc
     */
    public static Vector3 GetInnerCorner(Vector3 origin, int index) {
        return GetCorner(origin, index, outerRadius * solidFactor);
    }

    /*
     * See notes above GetCorner(Vector3, int)
     */
    private static Vector3 GetCorner(Vector3 origin, int index, float distance) {
        index += 4;
        while(index > 5) {
            index -= 6;
        }
        var rads = Mathf.PI / 180 * -(60 * index);
        return new Vector3(
            origin.x + distance * Mathf.Cos(rads),
            origin.y,
            origin.z + distance * Mathf.Sin(rads)
            );
    }

    /*
     * Get corner offsets for outer corners of hexagon bridges
     */
    public static Vector3 GetBridgeOffset(Vector3 origin, HexDirection dir) {
        var v1 = GetCorner(origin, (int)dir) - origin;
        var v2 = GetCorner(origin, (int)dir + 1) - origin;
        return (v1 + v2) * blendFactor;
    }

}