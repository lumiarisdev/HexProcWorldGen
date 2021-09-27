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
        Debug5,
        TropRainForest,
        TropForest,
        Savanna,
        SubtropDesert,
        TempRainForest,
        TempDecidForest,
        Woodland,
        Grassland,
        Shrubland,
        Taiga,
        Desert,
        Tundra
    }

    public class River {
        public int size;
        public Tuple<HexDirection, HexDirection> flow;
        public River(int s) {
            size = s;
        }
        public River(int s, HexDirection i, HexDirection o) {
            size = s;
            flow = new Tuple<HexDirection, HexDirection>(i, o);
        }
        public River(int s, Tuple<HexDirection, HexDirection> f) {
            size = s;
            flow = f;
        }
    }


    public class WorldTile {

        // this is to try coloring for debugging but it doesnt work well
        public static float MaxPrecipitation;

        public CubeCoordinates Coordinates;

        public int Elevation;
        public float Temperature;
        public float Humidity;
        float precipitation;
        public float Precipitation {
            get {
                return precipitation;
            }
            set {
                precipitation = value;
                if(value > MaxPrecipitation && !IsUnderwater) {
                    MaxPrecipitation = value;
                }
            }
        }
        public Tuple<int, float> Wind;
        public CubeCoordinates PlateCoords;
        public bool IsUnderwater {
            get {
                return Elevation < 0;
            }
        }
        public float MaxHumidity {
            get {
                var t = Mathf.InverseLerp(-40f, 30f, Temperature);
                return IsUnderwater ? 300f * (1f + Mathf.Lerp(-0.3f, 0.4f, t)) : 100f * (1f + Mathf.Lerp(-0.3f, 0.4f, t));
            }
        }

        public River River;

        // public float Fertility;
        public TerrainType Terrain;
        // public int BuildArea;
        // public Agent Owner;
        // public int[] Resources;

        // for testing
        public Tuple<CubeCoordinates, CubeCoordinates> MotionVector;

    }

}
