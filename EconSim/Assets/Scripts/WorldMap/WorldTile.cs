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

        public static float MaxPrecipitation = 0f;

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
                return Elevation <= 0;
            }
        }
        public float MaxHumidity {
            get {
                var t = Mathf.InverseLerp(-40f, 30f, Temperature);
                return IsUnderwater ? 300f * (1f + Mathf.Lerp(-0.3f, 0.4f, t)) : 100f * (1f + Mathf.Lerp(-0.3f, 0.4f, t));
            }
        }

        // public float Fertility;
        public TerrainType Terrain;
        // public int BuildArea;
        // public Agent Owner;
        // public int[] Resources;

        // for testing
        public Tuple<CubeCoordinates, CubeCoordinates> MotionVector;

    }

}
