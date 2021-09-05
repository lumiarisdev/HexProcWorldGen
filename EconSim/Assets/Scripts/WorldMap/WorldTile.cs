using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace EconSim {

    public enum TerrainType {
        None = -1,
        Ocean,
        Land,
        Mountain,
        Hill,
        Sand,
        Debug,
        Debug2,
        Debug3,
        Debug4,
        Debug5
    }

    public class WorldTile {

        public CubeCoordinates Coordinates;

        public int Elevation;
        public float Temperature;
        public float Precipitation;
        public Tuple<int, float> Wind;
        public CubeCoordinates PlateCoords;

        // public float Fertility;
        public TerrainType Terrain;
        // public int BuildArea;
        // public Agent Owner;
        // public int[] Resources;

        // for testing
        public Tuple<CubeCoordinates, CubeCoordinates> MotionVector;

    }

}
