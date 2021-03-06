using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EconSim
{
    public class WorldPlate
    {

        public WorldPlate(CubeCoordinates origin) {
            Origin = origin;
            Tiles = new List<CubeCoordinates>();
            BoundaryTiles = new List<CubeCoordinates>();
        }

        public CubeCoordinates Origin;

        public List<CubeCoordinates> Tiles;
        public List<CubeCoordinates> BoundaryTiles;

        public bool Oceanic;

        public int DesiredElevation;
        public int Elevation;
        public int MinElevation {
            get {
                return Oceanic ? -75 : 0;
            }
        }
        public int MaxElevation {
            get {
                return 80;
            }
        }

        public CubeCoordinates DriftAxis;
        public CubeCoordinates Motion; // THIS IS A VECTOR

    }

}
