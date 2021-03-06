using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Nito.Collections;
using System.Linq;

namespace EconSim
{
    public class WorldGenerator
    {

        public WorldArgs args;
        public WorldMapData WorldData;
        public float progress;
        public bool isDone;

        private ComputeShader computeShader;

        public WorldGenerator(WorldArgs _args) {
            WorldData = new WorldMapData();
            args = _args;
            progress = 0f;
            isDone = false;

            computeShader = Resources.Load<ComputeShader>("Assets/Scripts/WorldMap/GeneratorComputeSahder.compute");

        }

        public event EventHandler GenerateComplete;

        public IEnumerator GenerateWorld() {

            // apply seed
            ApplySeed();

            progress += 1f / 9f;
            yield return null;

            // Create tile dictionary
            CreateTiles();

            progress += 1f / 9f;
            yield return null;

            // generate heightmap
            GenerateHeightMap();

            progress += 1f / 9f;
            yield return null;

            // generate temperature map
            GenerateTempMap();

            progress += 1f / 9f;
            yield return null;

            // generate humidity map
            GenerateHumidityMap();

            progress += 1f / 9f;
            yield return null;

            // generate wind map
            GenerateWindMap();

            progress += 1f / 9f;
            yield return null;

            // run weather simulation
            SimulatePrecipitation();

            progress += 1f / 9f;
            yield return null;

            // assign climate
            AssignClimate();

            progress += 1f / 9f;
            yield return null;

            // generate rivers
            GenerateRivers();

            progress += 1f / 9f;
            yield return null;

            isDone = true;
            GenerateComplete?.Invoke(this, EventArgs.Empty);

            // everything needed for the climate system has been created at this point.
            // the main things that need done now are rivers, lakes, and forests

        }

        private void CreateTiles() {
            var tiles = new Dictionary<CubeCoordinates, WorldTile>();
            for(int x = 0; x < args.SizeX; x++) {
                for(int z = 0; z < args.SizeZ; z++) {
                    var coords = CubeCoordinates.OffsetToCube(x, z);
                    tiles[coords] = new WorldTile {
                        Coordinates = coords,
                        PlateCoords = new CubeCoordinates(),
                        Elevation = -1000,
                        Temperature = 0,
                        Precipitation = 0,
                    };
                }
            }
            WorldData.WorldDict = tiles;
        }

        /*
         * Generate heighmap, apply to tiles in worlddict
         */
        private void GenerateHeightMap() {
            Dictionary<CubeCoordinates, WorldPlate> tectPlates = new Dictionary<CubeCoordinates, WorldPlate>();
            Dictionary<CubeCoordinates, bool> assigned = new Dictionary<CubeCoordinates, bool>();
            Dictionary<CubeCoordinates, Deque<CubeCoordinates>> ffDeqs = new Dictionary<CubeCoordinates, Deque<CubeCoordinates>>(); // list of deques for the flood fills later
            Dictionary<CubeCoordinates, float> chance = new Dictionary<CubeCoordinates, float>();
            Dictionary<CubeCoordinates, Dictionary<CubeCoordinates, bool>> visited = new Dictionary<CubeCoordinates, Dictionary<CubeCoordinates, bool>>();

            int rX = UnityEngine.Random.Range(0, args.SizeX);
            int rZ = UnityEngine.Random.Range(0, args.SizeZ);

            // create plates
            for (int i = 0; i < args.NumPlates; i++) {
                while (tectPlates.TryGetValue(CubeCoordinates.OffsetToCube(rX, rZ), out WorldPlate _)) {
                    rX = UnityEngine.Random.Range(0, args.SizeX);
                    rZ = UnityEngine.Random.Range(0, args.SizeZ);
                }
                CubeCoordinates plateOrigin = CubeCoordinates.OffsetToCube(rX, rZ);
                tectPlates[plateOrigin] = new WorldPlate(plateOrigin);
                tectPlates[plateOrigin].Oceanic = args.OceanFrequency > UnityEngine.Random.Range(0f, 1f);
                tectPlates[plateOrigin].DesiredElevation = tectPlates[plateOrigin].Oceanic ?
                    UnityEngine.Random.Range(-23, -5) :
                    UnityEngine.Random.Range(8, 20);
                CubeCoordinates driftPoint = plateOrigin;
                while (driftPoint.Equals(plateOrigin)) {
                    rX = UnityEngine.Random.Range(0, args.SizeX);
                    rZ = UnityEngine.Random.Range(0, args.SizeZ);
                    driftPoint = CubeCoordinates.OffsetToCube(rX, rZ);
                }
                tectPlates[plateOrigin].DriftAxis = driftPoint;
                // calculate motion vector
                // get the absolute vector from drift - origin
                // then scale, scaling using lerp rn, lets see if it works
                // might want to add a rotation as well, to simulate plate rotations
                tectPlates[plateOrigin].Motion = CubeCoordinates.Lerp(plateOrigin, driftPoint, args.PlateMotionScaleFactor) - plateOrigin;

                // set up flood fill variables
                ffDeqs[plateOrigin] = new Deque<CubeCoordinates>();
                ffDeqs[plateOrigin].AddToBack(plateOrigin);
                visited[plateOrigin] = new Dictionary<CubeCoordinates, bool>();
                visited[plateOrigin][plateOrigin] = true;
                chance[plateOrigin] = 50f; //100f;

            }

            //int b = args.SizeX * args.SizeZ;
            //int j = 0;
            while (assigned.Count < WorldData.WorldDict.Count) {
                foreach (CubeCoordinates plateOrigin in tectPlates.Keys) {
                    if(UnityEngine.Random.Range(0f, 100f) <= chance[plateOrigin]) {
                        if (ffDeqs[plateOrigin].Count > 0) {
                            var coords = ffDeqs[plateOrigin].RemoveFromFront();
                            if (WorldData.WorldDict[coords].PlateCoords.Equals(new CubeCoordinates())) {
                                WorldData.WorldDict[coords].PlateCoords = plateOrigin;
                                assigned[coords] = true;
                                //if (chance[plateOrigin] >= UnityEngine.Random.Range(0f, 100f)) {
                                for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                                    if (!visited[plateOrigin].TryGetValue(coords.GetNeighbor(d), out bool _) && ValidInMap(coords.GetNeighbor(d))) {
                                        ffDeqs[plateOrigin].AddToBack(coords.GetNeighbor(d));
                                        visited[plateOrigin][coords.GetNeighbor(d)] = true;
                                    }
                                }
                                //}
                                //chance[plateOrigin] *= args.PlateSpreadDecay;
                                // leaving out the chance decay right now, as it may cause this to loop forever without completing
                            }
                        }
                    }
                }
                //j++;
            }

            // find boundaries now that all tiles are assigned
            foreach (WorldTile tile in WorldData.WorldDict.Values) {
                for (HexDirection i = 0; i <= HexDirection.NW; i++) {
                    WorldTile neigh = WorldData.WorldDict.TryGetValue(tile.Coordinates.GetNeighbor(i), out WorldTile w) ? w : null;
                    if (neigh != null) {
                        if (!tile.PlateCoords.Equals(neigh.PlateCoords)) {
                            tectPlates[tile.PlateCoords].BoundaryTiles.Add(tile.Coordinates);
                            //wData.WorldDict[tile].Terrain = TerrainType.Debug3; // debug
                            break;
                        }
                    }
                }
            }

            // calculate elevations
            foreach(WorldPlate plate in tectPlates.Values) {
                foreach (CubeCoordinates tile in plate.BoundaryTiles) {
                    var plateOrigin = WorldData.WorldDict[tile].PlateCoords;

                    Dictionary<HexDirection, CubeCoordinates> plateNeighbors = new Dictionary<HexDirection, CubeCoordinates>();
                    for (HexDirection i = HexDirection.N; i <= HexDirection.NW; i++) {
                        if (ValidInMap(tile.GetNeighbor(i))) {
                            if (!WorldData.WorldDict[tile.GetNeighbor(i)].PlateCoords.Equals(plateOrigin)) {
                                plateNeighbors[i] = WorldData.WorldDict[tile.GetNeighbor(i)].PlateCoords;
                            }
                        }
                    }

                    // calculate pressure

                    // testing some changes here to fix some outlier cases and smooth elevations moer
                    // mainly just preventing elevation from being added to by multiple interactions
                    // a testing seed: 8603485
                    // 5922675

                    foreach (HexDirection nDir in plateNeighbors.Keys) { /* the direction of the neighbor tile, as a hexdirection */
                        var pressure = 0;
                        var plateNeighbor = plateNeighbors[nDir]; // neighbor's plate
                        var neighbor = tile.GetNeighbor(nDir); // neighbor tile
                        var nDirVector = neighbor - tile; // the direction of the neighbor tile, as a vector
                        var r = tectPlates[plateOrigin].Motion - tectPlates[plateNeighbor].Motion;
                        // the component of r along neighbor dir
                        pressure += nDirVector.x != 0 ? r.x : 0;
                        pressure += nDirVector.y != 0 ? r.y : 0;
                        pressure += nDirVector.z != 0 ? r.z : 0;

                        // map pressure to elevation, THIS COULD USE A LOT OF TWEAKING
                        if (pressure > 0) {
                            if (tectPlates[plateOrigin].Oceanic == tectPlates[plateNeighbor].Oceanic) {
                                // same plate type, directly colliding
                                WorldData.WorldDict[tile].Elevation = Mathf.Max(tectPlates[plateOrigin].DesiredElevation, tectPlates[plateNeighbor].DesiredElevation);
                                if (tectPlates[plateOrigin].Oceanic) {
                                    WorldData.WorldDict[tile].Elevation += (int)(pressure * 0.25f);
                                }
                                else {
                                    WorldData.WorldDict[tile].Elevation += (int)(pressure * 1.55f);
                                }
                            }
                            else if (tectPlates[plateOrigin].Oceanic == true && tectPlates[plateNeighbor].Oceanic == false) {
                                // this is ocean, neighbor is land
                                WorldData.WorldDict[tile].Elevation = Mathf.Min(tectPlates[plateOrigin].DesiredElevation, tectPlates[plateNeighbor].DesiredElevation);
                                WorldData.WorldDict[tile].Elevation += (int)(pressure * 0.1f);
                            }
                            else if (tectPlates[plateOrigin].Oceanic == false && tectPlates[plateNeighbor].Oceanic == true) {
                                // this is land, neighbor is ocean
                                WorldData.WorldDict[tile].Elevation = Mathf.Max(tectPlates[plateOrigin].DesiredElevation, tectPlates[plateNeighbor].DesiredElevation);
                                WorldData.WorldDict[tile].Elevation += (int)(pressure * 0.2f);
                            }
                        }
                        else if (pressure < 0 && WorldData.WorldDict[tile].Elevation == -1000) {
                            if (tectPlates[plateOrigin].Oceanic != tectPlates[plateNeighbor].Oceanic) {
                                WorldData.WorldDict[tile].Elevation = (tectPlates[plateOrigin].DesiredElevation + tectPlates[plateNeighbor].DesiredElevation) / 2;
                                WorldData.WorldDict[tile].Elevation += (int)Mathf.Abs(pressure * (tectPlates[plateOrigin].Oceanic ? 0.15f : 0.1f));
                            }
                            else {
                                if (tectPlates[plateOrigin].Oceanic) {
                                    WorldData.WorldDict[tile].Elevation = Mathf.Max(tectPlates[plateOrigin].DesiredElevation, tectPlates[plateNeighbor].DesiredElevation);
                                    WorldData.WorldDict[tile].Elevation += (int)(Mathf.Abs(pressure) * 0.05f);
                                }
                                else {
                                    WorldData.WorldDict[tile].Elevation = (tectPlates[plateOrigin].DesiredElevation + tectPlates[plateNeighbor].DesiredElevation) / 2;
                                    WorldData.WorldDict[tile].Elevation += (int)(pressure * 0.2f);
                                }
                            }
                        }
                        else {
                            WorldData.WorldDict[tile].Elevation = (tectPlates[plateOrigin].DesiredElevation + tectPlates[plateNeighbor].DesiredElevation) / 2;
                        }

                    }
                    // clamp values
                    if (WorldData.WorldDict[tile].Elevation < tectPlates[plateOrigin].MinElevation) {
                        WorldData.WorldDict[tile].Elevation = tectPlates[plateOrigin].MinElevation;
                    }
                    else if (WorldData.WorldDict[tile].Elevation > tectPlates[plateOrigin].MaxElevation) {
                        WorldData.WorldDict[tile].Elevation = tectPlates[plateOrigin].MaxElevation;
                    }

                }
            }

            // smoothing
            // I initially left this out in the refactor
            // But it seems to be necessary with how imprecise the above calculations are
            // If the consistency of the plate interactions can be improved, smoothing may no longer be necessary
            var smoothingPasses = 4;
            while (smoothingPasses > 0) {
                foreach(WorldPlate plate in tectPlates.Values) {
                    foreach (CubeCoordinates tile in plate.BoundaryTiles) {
                        var boundarySmoothedElevation = WorldData.WorldDict[tile].Elevation;
                        var boundarySmoothing = 1;
                        if (WorldData.WorldDict[tile].Elevation >= 0) {
                            for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                                if (WorldData.WorldDict.TryGetValue(tile.GetNeighbor(d), out WorldTile value)) {
                                    if (value.Elevation > WorldData.WorldDict[tile].Elevation || (value.Elevation - WorldData.WorldDict[tile].Elevation) > -52) {
                                        boundarySmoothedElevation += value.Elevation;
                                        boundarySmoothing++;
                                    }
                                }
                            }
                        }
                        boundarySmoothedElevation /= boundarySmoothing;
                        WorldData.WorldDict[tile].Elevation = boundarySmoothedElevation;
                    }
                }
                smoothingPasses--;
            }

            // testing a more efficient solution for this
            // we could improve further by sending the distance calc loop
            // to the gpu to run in parallel
            foreach (CubeCoordinates coords in WorldData.WorldDict.Keys) {
                if(WorldData.WorldDict[coords].Elevation == -1000) {

                    Dictionary<CubeCoordinates, int> distance = new Dictionary<CubeCoordinates, int>();
                    foreach(CubeCoordinates bTile in tectPlates[WorldData.WorldDict[coords].PlateCoords].BoundaryTiles) {
                        distance[bTile] = CubeCoordinates.DistanceBetween(bTile, coords);
                    }

                    var distList = distance.ToList();
                    distList.Sort((x, y) => x.Value.CompareTo(y.Value));

                    int bDistance = 0;
                    int bElevation = 0;
                    for(int i = 0; i < 4; i++) {
                        bDistance += distList[i].Value;
                        bElevation += WorldData.WorldDict[distList[i].Key].Elevation;
                    }

                    bDistance /= 4;
                    bElevation /= 4;

                    WorldData.WorldDict[coords].Elevation = Mathf.RoundToInt(Coserp(tectPlates[WorldData.WorldDict[coords].PlateCoords].DesiredElevation,
                        bElevation, Mathf.Pow(args.UpliftDecay, bDistance)));

                }
            }

            // some clamping, but with neighbor check to allow for small islands, but eliminate outliers
            //foreach (CubeCoordinates coords in WorldData.WorldDict.Keys) {
            //    var plate = tectPlates[WorldData.WorldDict[coords].PlateCoords];
            //    if (plate.Oceanic && WorldData.WorldDict[coords].Elevation > -1) {
            //        var neighborLand = false;
            //        for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
            //            if (WorldData.WorldDict.TryGetValue(coords.GetNeighbor(d), out WorldTile value) && value.Elevation > -1) {
            //                neighborLand = true;
            //                break;
            //            }
            //        }
            //        if (!neighborLand) {
            //            WorldData.WorldDict[coords].Elevation = -1;
            //        }
            //    }
            //}

        }

        private void GenerateTempMap() {
            /*
             * Determine which tiles fall along the equator. 
             * 
             * To do this, we are going to select all tiles that can be reached via a (+2, -1, -1) or (-2, +1, +1)
             * permutation from (0, 0, 0). Then, we will select their neighbors in the NW, SW, NE, SE directions.
             * 
             * Also assign temperatures for equator tiles.
             */
            var equator = new List<CubeCoordinates>();
            var current = new CubeCoordinates(0, 0, 0);
            current = current + CubeCoordinates.Scale(CubeCoordinates.Permutations[0], args.SizeZ / 2);
            while (ValidInMap(current)) {
                equator.Add(current);
                WorldData.WorldDict[current].Temperature = 30f;
                if (ValidInMap(current.GetNeighbor(HexDirection.NE))) {
                    WorldData.WorldDict[current.GetNeighbor(HexDirection.NE)].Temperature = 30f;
                    equator.Add(current.GetNeighbor(HexDirection.NE));
                }
                if (ValidInMap(current.GetNeighbor(HexDirection.SE))) {
                    WorldData.WorldDict[current.GetNeighbor(HexDirection.SE)].Temperature = 30f;
                    equator.Add(current.GetNeighbor(HexDirection.SE));
                }
                current.x += 2;
                current.y--;
                current.z--;
            }

            /*
             * Assign temperatures for all tiles based on distance from the equator as well as elevation.
             */

            foreach (CubeCoordinates tile in WorldData.WorldDict.Keys) {
                if (WorldData.WorldDict[tile].Temperature == 0) {
                    var closestEqTile = equator[0];
                    foreach (CubeCoordinates eq in equator) {
                        if (CubeCoordinates.DistanceBetween(tile, closestEqTile) > CubeCoordinates.DistanceBetween(tile, eq)) {
                            closestEqTile = eq;
                        }
                    }
                    if (CubeCoordinates.DistanceBetween(tile, closestEqTile) < 4) {
                        WorldData.WorldDict[tile].Temperature = 30f;
                    }
                    else {
                        WorldData.WorldDict[tile].Temperature = Mathf.Lerp(-45f, 30f, Mathf.Pow(args.TemperatureDecay, CubeCoordinates.DistanceBetween(tile, closestEqTile)));
                    }
                }

                // elevation effect
                // id like to change this to more accurately reflect real world normal lapse rate
                if (WorldData.WorldDict[tile].Elevation > 36) {
                    var tempChange = Mathf.Lerp(-40f, 0f, Mathf.Pow(args.TemperatureDecayElevation, WorldData.WorldDict[tile].Elevation - 34));
                    WorldData.WorldDict[tile].Temperature = WorldData.WorldDict[tile].Temperature + tempChange;
                }
                else if (WorldData.WorldDict[tile].Elevation < -20) {
                    var tempChange = Mathf.Lerp(-7.5f, 0f, Mathf.Pow(args.TemperatureDecayElevation - 1, Mathf.Abs(WorldData.WorldDict[tile].Elevation)));
                    WorldData.WorldDict[tile].Temperature = WorldData.WorldDict[tile].Temperature + tempChange;
                }

            }

        }

        /*
         * NOTES:
         * 
         * 10C air can hold ~11grams per cubic meter
         * 20C air can hold ~22grams per cubic meter
         * 
         * We will simplify things and use a linear scale then. So, 30C = ~33gpm^3, etc
         * 
         */
        private void GenerateHumidityMap() {
            
            // could be changed later to use WorldTile.MaxHumidity, but id like to keep these separate atm
            foreach(CubeCoordinates tile in WorldData.WorldDict.Keys) {
                var rHumidity = WorldData.WorldDict[tile].IsUnderwater ? 4.5f : 0.175f;
                var hCap = WorldData.WorldDict[tile].Temperature > 5 ? WorldData.WorldDict[tile].Temperature * 1.1f : 5.5f;
                WorldData.WorldDict[tile].Humidity = rHumidity * hCap;
            }

        }

        // generate wind map
        private void GenerateWindMap() {
            var windCellSizeX = args.SizeX / 6;
            var windCellSizeZ = args.SizeZ / 6;
            var WindCellSize = CubeCoordinates.OffsetToCube(new Vector3(0, 0, windCellSizeZ));
            CubeCoordinates[] windCells = {
                new CubeCoordinates(0, 0, 0),
                new CubeCoordinates(0, WindCellSize.y,  WindCellSize.z),
                new CubeCoordinates(0, 2*WindCellSize.y,  2*WindCellSize.z),
                new CubeCoordinates(0, 3*WindCellSize.y,  3*WindCellSize.z),
                new CubeCoordinates(0, 4*WindCellSize.y,  4*WindCellSize.z),
                new CubeCoordinates(0, 5*WindCellSize.y,  5*WindCellSize.z),
            };

            // primary wind calculations
            var magnitudeMult = 1.0f;
            for (int z = 0; z < args.SizeZ; z++) {
                for (int x = 0; x < args.SizeX; x++) {
                    var tile = CubeCoordinates.OffsetToCube(x, z);
                    var windCell = windCells[0];
                    for (int i = 1; i < windCells.Length; i++) {
                        if (windCells[i].ToOffset().z < z) {
                            windCell = windCells[i];
                        }
                        else {
                            break;
                        }
                    }
                    var dir = 0;
                    var mag = 0f;
                    // south polar easterlies
                    if (windCell.Equals(windCells[0])) {
                        var t = Mathf.InverseLerp(windCells[0].ToOffset().z, windCells[1].ToOffset().z, z);
                        dir = (int)Mathf.Lerp(dir, -75, t);
                        var magT = Mathf.InverseLerp(windCells[1].ToOffset().z, windCells[0].ToOffset().z, z);
                        mag = Coserp(0f, 100f, magT) * magnitudeMult;
                    }
                    // southern westerlies
                    else if (windCell.Equals(windCells[1])) {
                        dir = 165;
                        var t = Mathf.InverseLerp(windCells[1].ToOffset().z, windCells[2].ToOffset().z, z);
                        dir = (int)Mathf.Lerp(dir, 105, t);
                        var magT = Mathf.InverseLerp(windCells[1].ToOffset().z, windCells[2].ToOffset().z, z);
                        mag = Coserp(0f, 100f, magT) * magnitudeMult;
                    }
                    // Southeasterly trades
                    else if (windCell.Equals(windCells[2])) {
                        var t = Mathf.InverseLerp(windCells[2].ToOffset().z, windCells[3].ToOffset().z, z);
                        dir = (int)Mathf.Lerp(-15, -75, t);
                        var magT = Mathf.InverseLerp(windCells[3].ToOffset().z, windCells[2].ToOffset().z, z);
                        mag = Coserp(0f, 100f, magT) * magnitudeMult;
                    }
                    //northeasterly trades
                    else if (windCell.Equals(windCells[3])) {
                        var t = Mathf.InverseLerp(windCells[3].ToOffset().z, windCells[4].ToOffset().z, z);
                        dir = 195;
                        dir = (int)Mathf.Lerp(270, dir, t);
                        mag = Coserp(0f, 100f, t) * magnitudeMult;
                    }
                    // northern westerlies
                    else if (windCell.Equals(windCells[4])) {
                        var t = Mathf.InverseLerp(windCells[4].ToOffset().z, windCells[5].ToOffset().z, z);
                        dir = (int)Mathf.Lerp(75, 15, t);
                        var magT = Mathf.InverseLerp(windCells[4].ToOffset().z, windCells[5].ToOffset().z, z);
                        mag = Coserp(0, 100, magT) * magnitudeMult;
                    } // north polar easterlies
                    else if (windCell.Equals(windCells[5])) {
                        var t = Mathf.InverseLerp(windCells[5].ToOffset().z, args.SizeZ, z);
                        dir = 180;
                        dir = (int)Mathf.Lerp(270, dir, t);
                        mag = Coserp(0, 100, t) * magnitudeMult;
                    }
                    else {
                        WorldData.WorldDict[tile].Wind = new Tuple<int, float>(0, 0f);
                    }

                    // modify the wind speed based on elevation
                    var el = WorldData.WorldDict[tile].Elevation;
                    var mod = 0f;
                    if (el < 0) el = 0;
                    if (el < 4) {
                        mod = 1.2f;
                    }
                    else if (el < 30) {
                        mod = 1f + Mathf.InverseLerp(4, 29, el) / 4;
                    }
                    else {
                        mod = 1.25f + Mathf.InverseLerp(30, 60, el) / 2;
                    }
                    if (mod > 0f) {
                        mag *= mod;
                    }
                    WorldData.WorldDict[tile].Wind = new Tuple<int, float>(dir, mag);
                }
            }
        }

        // this is the primary weather/precipitation simulation,
        // making use of the maps generated before
        private void SimulatePrecipitation() {
            for(int i = 0; i < args.WeatherPasses; i++) {
                
                foreach(CubeCoordinates tile in WorldData.WorldDict.Keys) {
                    var dir = WorldData.WorldDict[tile].Wind.Item1;
                    var mag = WorldData.WorldDict[tile].Wind.Item2;
                    var hMax = WorldData.WorldDict[tile].Temperature > 10 ? WorldData.WorldDict[tile].Temperature * 1.1f : 11f;

                    if (mag < 10f) {
                        mag = 10f;
                    }
                    if (mag > 0) {
                        var hChange = 0f;
                        hChange += (mag / 100f) * 0.15f * WorldData.WorldDict[tile].Humidity;
                        WorldData.WorldDict[tile].Humidity -= hChange * Mathf.Lerp(1f, .9f, (mag / 100f)); // doesnt take all humidity, based on wind speed TESTING
                        if (dir >= -90 && dir < -45) {
                            // determine how much to push where
                            var nw = 0.5f;
                            var t = Mathf.InverseLerp(-90, -45, dir) / 2;
                            nw += t;
                            var sw = 1 - nw;
                            var hcNW = nw * hChange;
                            var hcSW = sw * hChange;
                            // push temp and humidity
                            if (ValidInMap(tile.GetNeighbor(HexDirection.NW))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Humidity += hcNW;
                            }
                            if (ValidInMap(tile.GetNeighbor(HexDirection.SW))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Humidity += hcSW;
                            }
                        }
                        else if (dir >= -45 && dir < 0) {
                            var nw = Mathf.InverseLerp(0, -45, dir);
                            var n = Mathf.InverseLerp(-45, 0, dir);
                            var hcNW = nw * hChange;
                            var hcN = n * hChange;
                            if (ValidInMap(tile.GetNeighbor(HexDirection.NW))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Humidity += hcNW;
                            }
                            if (ValidInMap(tile.GetNeighbor(HexDirection.N))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.N)].Humidity += hcN;
                            }
                        }
                        else if (dir >= 0 && dir < 45) {
                            var ne = Mathf.InverseLerp(0, 45, dir);
                            var n = Mathf.InverseLerp(45, 0, dir);
                            var hcNE = ne * hChange;
                            var hcN = n * hChange;
                            if (ValidInMap(tile.GetNeighbor(HexDirection.NE))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.NE)].Humidity += hcNE;
                            }
                            if (ValidInMap(tile.GetNeighbor(HexDirection.N))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.N)].Humidity += hcN;
                            }
                        }
                        else if (dir >= 45 && dir < 90) {
                            // determine how much to push where
                            var ne = 1f;
                            var t = Mathf.InverseLerp(45, 90, dir);
                            ne -= Mathf.Lerp(0f, 0.5f, t);
                            var se = 1 - ne;
                            var hcNE = ne * hChange;
                            var hcSE = se * hChange;
                            // push temp and humidity
                            if (ValidInMap(tile.GetNeighbor(HexDirection.NE))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.NE)].Humidity += hcNE;
                            }
                            if (ValidInMap(tile.GetNeighbor(HexDirection.SE))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.SE)].Humidity += hcSE;
                            }
                        }
                        else if (dir >= 90 && dir < 135) {
                            // determine how much to push where
                            var ne = 0.5f;
                            var t = Mathf.InverseLerp(90, 135, dir);
                            ne -= Mathf.Lerp(0f, 0.5f, t);
                            var se = 1 - ne;
                            var hcNE = ne * hChange;
                            var hcSE = se * hChange;
                            // push temp and humidity
                            if (ValidInMap(tile.GetNeighbor(HexDirection.NE))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.NE)].Humidity += hcNE;
                            }
                            if (ValidInMap(tile.GetNeighbor(HexDirection.SE))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.SE)].Humidity += hcSE;
                            }
                        }
                        else if (dir >= 135 && dir < 180) {
                            var se = Mathf.InverseLerp(180, 135, dir);
                            var s = Mathf.InverseLerp(135, 180, dir);
                            var hcSE = se * hChange;
                            var hcS = s * hChange;
                            if (ValidInMap(tile.GetNeighbor(HexDirection.SE))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.SE)].Humidity += hcSE;
                            }
                            if (ValidInMap(tile.GetNeighbor(HexDirection.S))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.S)].Humidity += hcS;
                            }
                        }
                        else if (dir >= 180 && dir < 225) {
                            var s = Mathf.InverseLerp(225, 180, dir);
                            var sw = Mathf.InverseLerp(180, 225, dir);
                            var hcSW = sw * hChange;
                            var hcS = s * hChange;
                            if (ValidInMap(tile.GetNeighbor(HexDirection.SW))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Humidity += hcSW;
                            }
                            if (ValidInMap(tile.GetNeighbor(HexDirection.S))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.S)].Humidity += hcS;
                            }
                        }
                        else if (dir >= 225 && dir < 270) {
                            var t = Mathf.InverseLerp(225, 270, dir);
                            var sw = Mathf.Lerp(1, 0.5f, t);
                            var nw = 1 - sw;
                            var hcNW = nw * hChange;
                            var hcSW = sw * hChange;
                            if (ValidInMap(tile.GetNeighbor(HexDirection.NW))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Humidity += hcNW;
                            }
                            if (ValidInMap(tile.GetNeighbor(HexDirection.SW))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Humidity += hcSW;
                            }
                        }
                        else if (dir >= 270) {
                            var sw = 0.5f;
                            var nw = 0.5f;
                            var hcNW = nw * hChange;
                            var hcSW = sw * hChange;
                            if (ValidInMap(tile.GetNeighbor(HexDirection.NW))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Humidity += hcNW;
                            }
                            if (ValidInMap(tile.GetNeighbor(HexDirection.SW))) {
                                WorldData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Humidity += hcSW;
                            }
                        }
                    }

                }

                // humidity smoothing
                foreach (CubeCoordinates tile in WorldData.WorldDict.Keys) {
                    var smoothedHumidity = WorldData.WorldDict[tile].Humidity;
                    var smoothing = 1;
                    for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                        if (WorldData.WorldDict.TryGetValue(tile.GetNeighbor(d), out WorldTile wt)) {
                            smoothedHumidity += wt.Humidity;
                            smoothing++;
                        }
                    }
                    smoothedHumidity /= smoothing;
                    WorldData.WorldDict[tile].Humidity = smoothedHumidity;
                }

                // create precipitation
                if(i % 20 == 1) {
                    foreach (CubeCoordinates tile in WorldData.WorldDict.Keys) {
                        var hMax = WorldData.WorldDict[tile].Temperature > 10 ? WorldData.WorldDict[tile].Temperature * 1.1f : 11f;
                        if (!WorldData.WorldDict[tile].IsUnderwater) {
                            if (WorldData.WorldDict[tile].Humidity > hMax) {
                                WorldData.WorldDict[tile].Precipitation += (WorldData.WorldDict[tile].Humidity - hMax) + (hMax * 0.1f);
                                WorldData.WorldDict[tile].Humidity -= ((WorldData.WorldDict[tile].Humidity - hMax) + (hMax * 0.1f));
                            }
                        }
                    }
                }

            }

            // dump remaining humidity
            foreach (CubeCoordinates tile in WorldData.WorldDict.Keys) {
                var hMax = WorldData.WorldDict[tile].Temperature > 10 ? WorldData.WorldDict[tile].Temperature * 1.1f : 11f;
                if (!WorldData.WorldDict[tile].IsUnderwater) {
                    if (WorldData.WorldDict[tile].Humidity > hMax) {
                        WorldData.WorldDict[tile].Precipitation += (WorldData.WorldDict[tile].Humidity - hMax) + (hMax * 0.1f);
                        WorldData.WorldDict[tile].Humidity -= ((WorldData.WorldDict[tile].Humidity - hMax) + (hMax * 0.1f));
                    }
                }
            }

        }

        // climate assignment, based on temperature and precipitation
        // this is very temporary, as I test the overall climate system
        // it is using humidity right now, because I havent seen the precip numbers
        private void AssignClimate() {
            foreach (CubeCoordinates tile in WorldData.WorldDict.Keys) {
                if (WorldData.WorldDict[tile].Temperature > 20) {
                    if (WorldData.WorldDict[tile].Precipitation > 75) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.TropRainForest;
                    }
                    else if (WorldData.WorldDict[tile].Precipitation <= 75 && WorldData.WorldDict[tile].Precipitation > 30) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.TropForest;
                    }
                    else if (WorldData.WorldDict[tile].Precipitation <= 30 && WorldData.WorldDict[tile].Precipitation > 10) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.Savanna;
                    }
                    else if (WorldData.WorldDict[tile].Precipitation <= 10) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.SubtropDesert;
                    }
                }
                else if (WorldData.WorldDict[tile].Temperature <= 20 && WorldData.WorldDict[tile].Temperature > 10) {
                    if (WorldData.WorldDict[tile].Precipitation > 65) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.TempRainForest;
                    }
                    else if (WorldData.WorldDict[tile].Precipitation <= 65 && WorldData.WorldDict[tile].Precipitation > 30) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.TempDecidForest;
                    }
                    else if (WorldData.WorldDict[tile].Precipitation <= 30 && WorldData.WorldDict[tile].Precipitation > 10) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.Woodland;
                    }
                    else if (WorldData.WorldDict[tile].Precipitation <= 10) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.Grassland;
                    }
                }
                else if (WorldData.WorldDict[tile].Temperature <= 10 && WorldData.WorldDict[tile].Temperature > 4) {
                    if (WorldData.WorldDict[tile].Precipitation > 25) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.TempDecidForest;
                    }
                    else if (WorldData.WorldDict[tile].Precipitation <= 25 && WorldData.WorldDict[tile].Precipitation > 10) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.Woodland;
                    }
                    else if (WorldData.WorldDict[tile].Precipitation <= 10) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.Grassland;
                    }
                }
                else if (WorldData.WorldDict[tile].Temperature <= 4 && WorldData.WorldDict[tile].Temperature > -5) {
                    if (WorldData.WorldDict[tile].Precipitation > 25) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.Taiga;
                    }
                    else if (WorldData.WorldDict[tile].Precipitation <= 25 && WorldData.WorldDict[tile].Precipitation > 10) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.Shrubland;
                    }
                    else if (WorldData.WorldDict[tile].Precipitation <= 10) {
                        WorldData.WorldDict[tile].Terrain = TerrainType.Desert;
                    }
                }
                else if (WorldData.WorldDict[tile].Temperature <= -5) {
                    WorldData.WorldDict[tile].Terrain = TerrainType.Tundra;
                }
                if (WorldData.WorldDict[tile].Elevation > 48) {
                    WorldData.WorldDict[tile].Terrain = TerrainType.Mountain;
                }
                else if (WorldData.WorldDict[tile].Elevation <= 48 && WorldData.WorldDict[tile].Elevation > 42) {
                    WorldData.WorldDict[tile].Terrain = TerrainType.Hill;
                }
                else if (WorldData.WorldDict[tile].Elevation < 0) {
                    WorldData.WorldDict[tile].Terrain = TerrainType.Sand;
                }

                if (WorldData.WorldDict[tile].Humidity < 0) {
                    WorldData.WorldDict[tile].Terrain = TerrainType.Debug;
                }

            }
        }

        // rivers
        /*
         * To start, find a set of possible river origin points
         * These would be high elevation, high precipitation tiles
         * From there, we can drop a particle that will travel downhill
         * As it moves, it will pick up precipitation, until it winds up in the ocean,
         * or alternatively, setlling into a lake
         */
        [Range(0, 1)]
        public float RiverCutoff;
        private void GenerateRivers() {

            var sWorld = WorldData.WorldDict.ToList();
            sWorld.Sort((x, y) => y.Value.Elevation.CompareTo(x.Value.Elevation));

            List<Particle> particles = new List<Particle>();
            foreach(KeyValuePair<CubeCoordinates, WorldTile> kv in sWorld) {
                if(kv.Value.Precipitation > 0 && kv.Value.Elevation > -1) {
                    particles.Add(new Particle {
                        pos = kv.Key,
                        path = new Dictionary<CubeCoordinates, float>(),
                        water = 0f
                    });
                }
            }

            Dictionary<CubeCoordinates, float> erosionMap = new Dictionary<CubeCoordinates, float>();
            foreach(WorldTile tile in WorldData.WorldDict.Values) {
                erosionMap[tile.Coordinates] = tile.Elevation;
            }

            foreach (Particle p in particles) {
                while (WorldData.WorldDict[p.pos].Elevation >= 0) {
                    p.water += WorldData.WorldDict[p.pos].Precipitation;
                    p.path[p.pos] = p.water;
                    var el = p.pos;
                    for (HexDirection d = HexDirection.N; d <= HexDirection.NE; d++) {
                        if (!p.path.TryGetValue(p.pos.GetNeighbor(d), out float _) && ValidInMap(p.pos.GetNeighbor(d))) {
                            if (erosionMap[el] > erosionMap[p.pos.GetNeighbor(d)]) {
                                el = p.pos.GetNeighbor(d);
                            }
                            else if (erosionMap[el] == erosionMap[p.pos.GetNeighbor(d)]) {
                                el = WorldData.WorldDict[el].Precipitation > WorldData.WorldDict[p.pos.GetNeighbor(d)].Precipitation ? el : p.pos.GetNeighbor(d);
                                if (el.Equals(p.pos)) el = p.pos.GetNeighbor(d);
                            }
                        }
                    }
                    if (el.Equals(p.pos)) break;
                    erosionMap[p.pos] -= 0.1f;
                    p.pos = el;
                }
            }

            foreach (Particle p in particles) {
                var riverList = p.path.Keys.ToList();
                for(int i = 0; i < riverList.Count; i++) {
                    if(WorldData.WorldDict[riverList[i]].River == null) {
                        WorldData.WorldDict[riverList[i]].River = new River(1);
                    } else {
                        WorldData.WorldDict[riverList[i]].River.size++;
                    }
                    if(i - 1 > 0) {
                        for(HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                            if (riverList[i].GetNeighbor(d).Equals(riverList[i - 1])
                                && !WorldData.WorldDict[riverList[i]].River.flow.TryGetValue(d, out bool _)
                                && WorldData.WorldDict[riverList[i]].River.flow.Count < 2)
                                WorldData.WorldDict[riverList[i]].River.flow[d] = true;
                        }
                    }
                    if(i + 1 < riverList.Count) {
                        for(HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                            if (riverList[i].GetNeighbor(d).Equals(riverList[i + 1])
                                && !WorldData.WorldDict[riverList[i]].River.flow.TryGetValue(d, out bool _)
                                && WorldData.WorldDict[riverList[i]].River.flow.Count < 2)
                                WorldData.WorldDict[riverList[i]].River.flow[d] = false;
                        }
                    }
                }
            }

            // apply erosion
            // this is doing a full elevation smoothing again...
            // not really good
            foreach (CubeCoordinates t in erosionMap.Keys) {
                if(WorldData.WorldDict[t].River != null) {
                    var smoothedE = erosionMap[t];
                    int smoothing = 1;
                    for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                        smoothedE += ValidInMap(t.GetNeighbor(d)) ? erosionMap[t.GetNeighbor(d)] : 0;
                        smoothing += ValidInMap(t.GetNeighbor(d)) ? 1 : 0;
                    }
                    smoothedE /= smoothing;
                    WorldData.WorldDict[t].Elevation = Mathf.RoundToInt(smoothedE);
                }
            }
        }

        class Particle {
            public CubeCoordinates pos;
            public Dictionary<CubeCoordinates, float> path;
            public float water;
        }

        private bool ValidInMap(CubeCoordinates c) {
            //CubeCoordinates max = CubeCoordinates.OffsetToCube(args.SizeX, args.SizeZ);
            //return (c.x < max.x && c.x >= 0) && (c.y > max.y && c.y <= 0) && (c.z < max.z && c.z >= 0);
            return WorldData.WorldDict.TryGetValue(c, out WorldTile _);
        }

        // apply seed according to world args
        private void ApplySeed() {
            if (args.UseStringSeed) {
                args.WorldSeed = args.StringSeed.GetHashCode();
            }
            if (args.RandomizeSeed) {
                args.WorldSeed = UnityEngine.Random.Range(0, 9999999);
            }
            UnityEngine.Random.InitState(args.WorldSeed);
        }
        
        /*
         * Cosine interpolation 
         */
        float Coserp(float a,  float b, float t) {
            float t2;
            t2 = (1 - Mathf.Cos(t * Mathf.PI)) / 2;
            return (a * (1 - t2) + b * t2);
        }

    }
}
