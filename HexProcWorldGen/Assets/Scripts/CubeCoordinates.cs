using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EconSim {

    public enum HexDirection {
        N, NE, SE, S, SW, NW
    }

    public static class HexDirectionExt {
        public static HexDirection Opposite(this HexDirection dir) {
            return (int)dir < 3 ? (dir + 3) : (dir - 3); 
        }
        public static HexDirection Previous(this HexDirection dir) {
            return dir == HexDirection.N ? HexDirection.NW : (dir - 1);
        }
        public static HexDirection Next(this HexDirection dir) {
            return dir == HexDirection.NW ? HexDirection.N : (dir + 1);
        }
    }

    [System.Serializable]
    public struct CubeCoordinates {

        public int x, y, z;

        public CubeCoordinates(int _x, int _y, int _z) {
            x = _x;
            y = _y;
            z = _z;
        }

        // if there are neighbor lookup problems, just look here lol
        public static CubeCoordinates[] Permutations = {
            new CubeCoordinates(0, -1, 1), // 1 move north
            new CubeCoordinates(1, -1, 0), // 1 move NE
            new CubeCoordinates(1, 0, -1), // 1 move SE
            new CubeCoordinates(0, 1, -1), // 1 move S
            new CubeCoordinates(-1, 1, 0), // 1 move SW
            new CubeCoordinates(-1, 0, 1)  // 1 move NW
        };

        public static CubeCoordinates FromPosition(Vector3 pos) {
            var x = pos.x / (HexMetrics.outerRadius * 2f * 0.75f);
            var z = pos.z / (Mathf.Sqrt(3) * HexMetrics.outerRadius);
            z += (x / 2);
            z -= (x * 0.5f);
            int iX = Mathf.RoundToInt(x);
            int iZ = Mathf.RoundToInt(z);
            return OffsetToCube(iX, iZ);
        }

        // THERES A LOT OF TRUNCATING GOING ON HERE
        public static CubeCoordinates Lerp(CubeCoordinates a, CubeCoordinates b, float h) {
            a.x += (int)((b.x - a.x) * h);
            a.y += (int)((b.y - a.y) * h);
            a.z += (int)((b.z - a.z) * h);
            return a;
        }

        public static int DistanceBetween(CubeCoordinates a, CubeCoordinates b) {
            return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z)) / 2;
        }

        public CubeCoordinates GetNeighbor(HexDirection dir) {
            return GetNeighbor(this, dir);
        }

        public static CubeCoordinates GetNeighbor(CubeCoordinates c, HexDirection dir) {
            return Add(c, Permutations[(int)dir]);
        }

        /*
         * Ring functions
         */
        public static CubeCoordinates[] GetRing(CubeCoordinates center, int radius) {
            var r = new List<CubeCoordinates>();
            var cube = center + Scale(Permutations[4], radius);
            for(int i = 0; i < 6; i++) {
                for(int k = 0; k < radius; k++) {
                    r.Add(cube);
                    cube = cube.GetNeighbor((HexDirection)i);
                }
            }
            return r.ToArray();
        }

        // Scale function
        public static CubeCoordinates Scale(CubeCoordinates c, int f) {
            return new CubeCoordinates(c.x * f, c.y * f, c.z * f);
        }

        /*
         * Add hex vector b to CubeCoordinate a, returning the resultant CubeCoordinate
         * a + b = c
         */
        public static CubeCoordinates Add(CubeCoordinates a, CubeCoordinates b) {
            var c = new CubeCoordinates(a.x + b.x, a.y + b.y, a.z + b.z);
            return c;
        }

        /*
         *  Subtract CubeCoordinates a from b, returning a hex vector
         *  b - a = v
         *  There is no validation check, as a hex vector may not be a valid coordinate address
         *  (v.x + v.y + v.z != 0)
         */
        public static CubeCoordinates Sub(CubeCoordinates a, CubeCoordinates b) {
            return new CubeCoordinates(b.x - a.x, b.y - a.y, b.z - a.z);
        }

        public static CubeCoordinates operator -(CubeCoordinates a, CubeCoordinates b) => Sub(b, a);
        public static CubeCoordinates operator +(CubeCoordinates a, CubeCoordinates b) => Add(a, b);

        public bool Validate() {
            return (x + y + z) == 0;
        }
        public static bool Validate(CubeCoordinates c) {
            return c.Validate();
        }

        public static CubeCoordinates OffsetToCube(int x, int z) {
            return OffsetToCube(new Vector3(x, 0, z));
        }

        // NEEDS VERIFICATION THAT CONVERSION IS CORRECT
        // appears correct
        // NOTE - conversion from Odd-q offset coordinates to cube coordinates
        public static CubeCoordinates OffsetToCube(Vector3Int coords) {
            var x = coords.x;
            var z = coords.z - (coords.x - (coords.x & 1)) / 2;
            var y = -x - z;
            var c = new CubeCoordinates(x, y, z);
            return c.Validate() ? c : new CubeCoordinates();
        }

        public static CubeCoordinates OffsetToCube(Vector3 coords) {
            return OffsetToCube(new Vector3Int(Mathf.RoundToInt(coords.x), Mathf.RoundToInt(coords.y), Mathf.RoundToInt(coords.z)));
        }

        public static Vector3Int CubeToOffset(CubeCoordinates coords) {
            var col = coords.x;
            var row = coords.z + (coords.x - (coords.x & 1)) / 2;
            return new Vector3Int(col, 0, row);
        }

        public Vector3Int ToOffset() {
            return CubeToOffset(this);
        }

        public override string ToString() {
            return "(" + x + ", " + y + ", " + z;
        }

        public string ToStringOnSeparateLines() {
            return x + "\n" + y + "\n" + z;
        }

        public override bool Equals(object obj) {
            if((obj == null) || !this.GetType().Equals(obj.GetType())) {
                return false;
            }
            CubeCoordinates c = (CubeCoordinates)obj;
            return (x == c.x) && (y == c.y) && (z == c.z);
        }

        public override int GetHashCode() {
            return new { x, y, z }.GetHashCode();
        }

    }

}
