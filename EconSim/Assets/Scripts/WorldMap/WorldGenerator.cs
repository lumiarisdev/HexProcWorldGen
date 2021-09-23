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

        private WorldArgs args;
        public WorldMapData WorldData;
        public float progress;
        public bool isDone;

        public WorldGenerator(WorldArgs _args) {
            WorldData = new WorldMapData();
            args = _args;
            progress = 0f;
            isDone = false;
        }

        public static WorldMapData GenerateWorld(WorldArgs _args) {
            var gen = new WorldGenerator(_args);
            gen.GenerateWorld();
            return gen.WorldData;
        }

        public IEnumerator GenerateWorld() {

            // apply seed
            ApplySeed();

            progress += 1f / 8f;
            yield return null;

            // Create tile dictionary
            CreateTiles();

            progress += 1f / 8f;
            yield return null;

            // generate heightmap
            GenerateHeightMap();

            progress += 1f / 8f;
            yield return null;

            // generate temperature map
            GenerateTempMap();

            progress += 1f / 8f;
            yield return null;

            // generate humidity map
            GenerateHumidityMap();

            progress += 1f / 8f;
            yield return null;

            // generate wind map
            GenerateWindMap();

            progress += 1f / 8f;
            yield return null;

            // run weather simulation
            SimulatePrecipitation();

            progress += 1f / 8f;
            yield return null;

            // assign climate
            AssignClimate();

            progress += 1f / 8f;
            yield return null;

            isDone = true;

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
                    UnityEngine.Random.Range(-20, -5) :
                    UnityEngine.Random.Range(5, 20);
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
                chance[plateOrigin] = 60f; //100f;

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
                                WorldData.WorldDict[tile].Elevation = Mathf.Max(tectPlates[plateOrigin].DesiredElevation, tectPlates[plateNeighbor].DesiredElevation);
                                WorldData.WorldDict[tile].Elevation += (int)(-pressure * 0.3f);
                            }
                            else if (tectPlates[plateOrigin].Oceanic == false && tectPlates[plateNeighbor].Oceanic == true) {
                                // this is land, neighbor is ocean
                                WorldData.WorldDict[tile].Elevation = Mathf.Min(tectPlates[plateOrigin].DesiredElevation, tectPlates[plateNeighbor].DesiredElevation);
                                WorldData.WorldDict[tile].Elevation += (int)(pressure * 0.3f);
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

            // this is very inefficient (no longer super, just very)
            // now it only checks its own plate, not every tile, at least lol
            // still could be way better
            // this is a very significant part of the loading time, and could be improved drastically
            foreach (CubeCoordinates coords in WorldData.WorldDict.Keys) {
                if(WorldData.WorldDict[coords].Elevation == -1000) {

                    var first = tectPlates[WorldData.WorldDict[coords].PlateCoords].BoundaryTiles[0];
                    CubeCoordinates[] closestBTiles = new CubeCoordinates[] {
                            first,
                            first,
                            first,
                            first
                        };
                    foreach (CubeCoordinates bTile in tectPlates[WorldData.WorldDict[coords].PlateCoords].BoundaryTiles) {
                        if(WorldData.WorldDict[bTile].PlateCoords.Equals(WorldData.WorldDict[coords].PlateCoords)) {
                            if (CubeCoordinates.DistanceBetween(bTile, coords) < CubeCoordinates.DistanceBetween(closestBTiles[0], coords)) {
                                closestBTiles[0] = bTile;
                            }
                        }
                    }

                    foreach (CubeCoordinates bTile in tectPlates[WorldData.WorldDict[coords].PlateCoords].BoundaryTiles) {
                        if (WorldData.WorldDict[bTile].PlateCoords.Equals(WorldData.WorldDict[coords].PlateCoords)) {
                            if (!bTile.Equals(closestBTiles[0])) {
                                if (CubeCoordinates.DistanceBetween(bTile, coords) < CubeCoordinates.DistanceBetween(closestBTiles[1], coords)) {
                                    closestBTiles[1] = bTile;
                                }
                            }
                        }
                    }

                    foreach (CubeCoordinates bTile in tectPlates[WorldData.WorldDict[coords].PlateCoords].BoundaryTiles) {
                        if (WorldData.WorldDict[bTile].PlateCoords.Equals(WorldData.WorldDict[coords].PlateCoords)) {
                            if (!bTile.Equals(closestBTiles[0]) && !bTile.Equals(closestBTiles[1])) {
                                if (CubeCoordinates.DistanceBetween(bTile, coords) < CubeCoordinates.DistanceBetween(closestBTiles[2], coords)) {
                                    closestBTiles[2] = bTile;
                                }
                            }
                        }
                    }

                    foreach (CubeCoordinates bTile in tectPlates[WorldData.WorldDict[coords].PlateCoords].BoundaryTiles) {
                        if (WorldData.WorldDict[bTile].PlateCoords.Equals(WorldData.WorldDict[coords].PlateCoords)) {
                            if (!bTile.Equals(closestBTiles[0]) && !bTile.Equals(closestBTiles[1]) && !bTile.Equals(closestBTiles[2])) {
                                if (CubeCoordinates.DistanceBetween(bTile, coords) < CubeCoordinates.DistanceBetween(closestBTiles[3], coords)) {
                                    closestBTiles[3] = bTile;
                                }
                            }
                        }
                    }

                    var bDistance = (CubeCoordinates.DistanceBetween(closestBTiles[0], coords)
                        + CubeCoordinates.DistanceBetween(closestBTiles[1], coords)
                        + CubeCoordinates.DistanceBetween(closestBTiles[2], coords)
                        + CubeCoordinates.DistanceBetween(closestBTiles[3], coords)) / 4;
                    var bElevation = (WorldData.WorldDict[closestBTiles[0]].Elevation
                        + WorldData.WorldDict[closestBTiles[1]].Elevation
                        + WorldData.WorldDict[closestBTiles[2]].Elevation +
                        WorldData.WorldDict[closestBTiles[3]].Elevation) / 4f;

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
                if (WorldData.WorldDict[tile].Elevation > 30) {
                    var tempChange = Mathf.Lerp(-40f, 0f, Mathf.Pow(args.TemperatureDecayElevation, WorldData.WorldDict[tile].Elevation - 11));
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
                var rHumidity = WorldData.WorldDict[tile].IsUnderwater ? 6f : 0.15f;
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
                        dir = 180;
                        var t = Mathf.InverseLerp(windCells[1].ToOffset().z, windCells[2].ToOffset().z, z);
                        dir = (int)Mathf.Lerp(dir, 105, t);
                        var magT = Mathf.InverseLerp(windCells[1].ToOffset().z, windCells[2].ToOffset().z, z);
                        mag = Coserp(0f, 100f, magT) * magnitudeMult;
                    }
                    // Southeasterly trades
                    else if (windCell.Equals(windCells[2])) {
                        var t = Mathf.InverseLerp(windCells[2].ToOffset().z, windCells[3].ToOffset().z, z);
                        dir = (int)Mathf.Lerp(dir, -75, t);
                        var magT = Mathf.InverseLerp(windCells[3].ToOffset().z, windCells[2].ToOffset().z, z);
                        mag = Coserp(0f, 100f, magT) * magnitudeMult;
                    }
                    //northeasterly trades
                    else if (windCell.Equals(windCells[3])) {
                        var t = Mathf.InverseLerp(windCells[3].ToOffset().z, windCells[4].ToOffset().z, z);
                        dir = 180;
                        dir = (int)Mathf.Lerp(270, dir, t);
                        mag = Coserp(0f, 100f, t) * magnitudeMult;
                    }
                    // northern westerlies
                    else if (windCell.Equals(windCells[4])) {
                        var t = Mathf.InverseLerp(windCells[4].ToOffset().z, windCells[5].ToOffset().z, z);
                        dir = (int)Mathf.Lerp(75, dir, t);
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
                        hChange += (mag / 100f) * 0.65f * WorldData.WorldDict[tile].Humidity;
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
                if(i % 3 == 1) {
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
                if (WorldData.WorldDict[tile].Elevation > 35) {
                    WorldData.WorldDict[tile].Terrain = TerrainType.Mountain;
                }
                else if (WorldData.WorldDict[tile].Elevation <= 35 && WorldData.WorldDict[tile].Elevation > 32) {
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

        [Obsolete("Use Func<IEnumerator> GenerateWorld() instead")]
        public WorldMapData GenerateWorldOld() {

            var wData = new WorldMapData();
            
            // seeding
            if(args.UseStringSeed) {
                args.WorldSeed = args.StringSeed.GetHashCode();
            }
            if(args.RandomizeSeed) {
                args.WorldSeed = UnityEngine.Random.Range(0, 9999999);
            }
            UnityEngine.Random.InitState(args.WorldSeed);

            // create initial tiles
            for (int z = 0; z < args.SizeZ; z++) {
                for (int x = 0; x < args.SizeX; x++) {
                    var coords = CubeCoordinates.OffsetToCube(new Vector3(x, 0, z));
                    wData.WorldDict[coords] = new WorldTile {
                        Coordinates = coords,
                        PlateCoords = new CubeCoordinates(), // default this, so that we can detect it later on ??
                        Terrain = TerrainType.None,
                        Elevation = -100
                    };
                }
            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Tiles Created.");

            /*
             * PLATE TECTONICS
             */

            // generate points for tectonic plate origins
            for (int i = 0; i < args.NumPlates; i++) {
                int randX = UnityEngine.Random.Range(0, args.SizeX);
                int randZ = UnityEngine.Random.Range(0, args.SizeZ);
                var _origin = CubeCoordinates.OffsetToCube(new Vector3(randX, 0f, randZ));
                // ensure that the point we pick has not yet been assigned to a plate
                while(!wData.WorldDict[_origin].PlateCoords.Equals(new CubeCoordinates())) {
                    randX = UnityEngine.Random.Range(0, args.SizeX);
                    randZ = UnityEngine.Random.Range(0, args.SizeZ);
                    _origin = CubeCoordinates.OffsetToCube(new Vector3(randX, 0f, randZ));
                }
                wData.PlateDict[_origin] = new WorldPlate(_origin) {
                    // classify plate as tectonic or oceanic
                    Oceanic = args.OceanFrequency > UnityEngine.Random.Range(0f, 1f),
                };

                /*
                 * Assign tiles to plates using a lazy flood fill, with a supplimentary check to ensure
                 * that all of the tiles get assigned to a plate. 
                 * 
                 * Attempting to cap the growth on the plates
                 *
                */
                int maxPlateSize = (int)(args.SizeX * args.SizeZ / (args.NumPlates * 0.25f));
                int plateSize = 1;
                Deque<CubeCoordinates> ffDeq = new Deque<CubeCoordinates>();
                Dictionary<CubeCoordinates, bool> visited = new Dictionary<CubeCoordinates, bool>();
                var chance = 100f;
                var decay = args.PlateSpreadDecay;
                ffDeq.AddToBack(_origin);
                visited[_origin] = true;
                while (ffDeq.Count > 0) {
                    var coords = ffDeq.RemoveFromFront();
                    if (wData.WorldDict[coords].PlateCoords.Equals(new CubeCoordinates())) {
                        wData.WorldDict[coords].PlateCoords = _origin;
                        wData.PlateDict[_origin].Tiles.Add(coords);
                        plateSize++;
                        wData.WorldDict[coords].Terrain = TerrainType.Debug; // debug
                        //if(plateSize >= maxPlateSize) {
                        //    break;
                        //}
                        if (chance >= UnityEngine.Random.Range(0f, 100f)) {
                            for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                                if (!visited.TryGetValue(coords.GetNeighbor(d), out bool _)) {
                                    if (wData.WorldDict.TryGetValue(coords.GetNeighbor(d), out WorldTile _)) {
                                        ffDeq.AddToBack(coords.GetNeighbor(d));
                                        visited[coords.GetNeighbor(d)] = true;
                                    }
                                }
                            }
                        }
                        chance *= decay;
                    }
                }

            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Plates Created (flood fill).");

            // this is the supplimentary check
            /*
             * This needs to be more robust. Doing the supplimentary assignment based only off of distance creates
             * edge cases where the closest plate origin is not the logical closest plate, because the plates
             * origin is not close to the center.
             * 
             * In order to fix this, we should only assign to the closest plate if we're sure that its the
             * correct one. So we should check our neighbors, and compare their plateCoords.
             * 
             * If they have no neighbors with plate coords, it is probably best to skip that tile and come back.
             */
            Queue<WorldTile> unassignedTiles = new Queue<WorldTile>();
            foreach (WorldTile tile in wData.WorldDict.Values) {
                if(tile.PlateCoords.Equals(new CubeCoordinates())) {
                    unassignedTiles.Enqueue(tile);
                }
            }
            Debug.Log(unassignedTiles.Count + " / " + args.SizeX * args.SizeZ);
            while (unassignedTiles.Count > 0) {
                var tile = unassignedTiles.Dequeue();
                var neighborsWithPlates = new List<WorldTile>();
                for(HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                    if(wData.WorldDict.TryGetValue(tile.Coordinates.GetNeighbor(d), out WorldTile value) && !value.PlateCoords.Equals(new CubeCoordinates())) {
                        neighborsWithPlates.Add(value);
                    }
                }
                if(neighborsWithPlates.Count == 0) {
                    unassignedTiles.Enqueue(tile);
                    continue;
                }
                if(neighborsWithPlates.Count > 0) {
                    var plateCounts = new Dictionary<CubeCoordinates, int>();
                    foreach(WorldTile neighbor in neighborsWithPlates) {
                        if(plateCounts.TryGetValue(neighbor.PlateCoords, out int value)) {
                            plateCounts[neighbor.PlateCoords]++;
                        } else {
                            plateCounts.Add(neighbor.PlateCoords, 1);
                        }
                    }
                    CubeCoordinates mostCommonPlate = new CubeCoordinates();
                    plateCounts[mostCommonPlate] = 0;
                    foreach(CubeCoordinates key in plateCounts.Keys) {
                        if(plateCounts[key] > plateCounts[mostCommonPlate]) {
                            mostCommonPlate = key;
                        }
                    }
                    tile.PlateCoords = mostCommonPlate;
                    wData.PlateDict[tile.PlateCoords].Tiles.Add(tile.Coordinates);
                } 
            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Supplimentary Plate/Tile Assignment Complete.");

            // find plate boundaries
            /*
             * First, start at the plate origin. Move north until you reach a tile who's northern neighbor
             * is on a different plate. Mark this tile as our first boundary tile.
             * Then, look clockwise around the tile from north until you find a neighbor who is on the same plate.
             * Move to that neighbor, and now look clockwise around the tile to find any neighbors that are on a different plate.
             * Repeat until you get all the way back to the starting boundary tile.
             * 
             * Alternatively, we can implement a simpler algorithm that searches through every tile in a plate.
             * This is fine for now, but given that the cost of finding the plate boundaries is O(mapArea) then,
             * it may cause performance problems. The cost of the less straightforward solution is O((boundaryTiles + radius) * plateCount)
             * 
             * TESTED: it works
             */
            foreach (WorldPlate plate in wData.PlateDict.Values) {
                foreach (CubeCoordinates tile in plate.Tiles) {
                    for (HexDirection i = 0; i <= HexDirection.NW; i++) {
                        if (wData.WorldDict.TryGetValue(tile.GetNeighbor(i), out WorldTile value)) {
                            if (!wData.WorldDict[tile].PlateCoords.Equals(value.PlateCoords)) {
                                plate.BoundaryTiles.Add(tile);
                                wData.WorldDict[tile].Terrain = TerrainType.Debug3; // debug
                                break;
                            }
                        }
                    } 
                }
            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Plate Boundaries Calculated.");

            // generate random desired elevations for plates
            // also generate plate motion
            foreach (WorldPlate plate in wData.PlateDict.Values) {

                // desired elevation
                if (plate.Oceanic) {
                    plate.DesiredElevation = UnityEngine.Random.Range(-15, -5);
                }
                else {
                    plate.DesiredElevation = UnityEngine.Random.Range(3, 15);
                }

                // plate motion
                // start by selecting random points to serves as drift "axes" for each plate
                var drift = plate.Origin;
                while (drift.Equals(plate.Origin)) {
                    int randX = UnityEngine.Random.Range(0, args.SizeX);
                    int randZ = UnityEngine.Random.Range(0, args.SizeZ);
                    drift = CubeCoordinates.OffsetToCube(new Vector3(randX, 0f, randZ));
                }
                plate.DriftAxis = drift;

                // calculate motion vector
                // get the absolute vector from drift - origin
                // then scale, scaling using lerp rn, lets see if it works
                // might want to add a rotation as well, to simulate plate rotations
                plate.Motion = CubeCoordinates.Lerp(plate.Origin, plate.DriftAxis, args.PlateMotionScaleFactor) - plate.Origin;

                // for testing
                // this is how im drawing gizmos for plates, temporarily, remove later
                wData.WorldDict[plate.Origin].MotionVector = new Tuple<CubeCoordinates, CubeCoordinates>(plate.Origin, CubeCoordinates.Lerp(plate.Origin, plate.DriftAxis, args.PlateMotionScaleFactor));

            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Plate Motion Generated.");

            // calculate elevations for boundary tiles based on plate motions
            /*
             * NOTES:
             * 
             * Each plate has a motion vector that determines how it is "moving" on the surface of the "planet".
             * Each plate has a desired elevation, that ideally the entire plate would rest on, however this will not be the case.
             * At the plate boundaries, the elevations will be greatly impacted by the interacting motion vectors of the plates.
             * To determine the most important force for calculating uplift (perpendicular force, pressure, or otherwise named)
             * we need to first subtract the two plates/tiles motion vectors to receive a resultant vector. We can find our
             * perpendicular force now by taking the component of the resultant vector that is perpendicular to the boundary line.
             * 
             * Ideally, we could set neighbor elevation at the same time as the selected tile's. But Im not sure how
             * to avoid calculating the same tiles over again anyways, so for now, I will just do it one by one.
             * 
             */
            foreach (WorldPlate plate in wData.PlateDict.Values) {

                foreach (CubeCoordinates tile in plate.BoundaryTiles) {
                    // zero out elevation
                    wData.WorldDict[tile].Elevation = 0;

                    List<CubeCoordinates> plateNeighbors = new List<CubeCoordinates>();
                    for (HexDirection i = HexDirection.N; i <= HexDirection.NW; i++) {
                        if (wData.WorldDict.TryGetValue(tile.GetNeighbor(i), out WorldTile value)) {
                            if (!value.PlateCoords.Equals(plate.Origin)) {
                                plateNeighbors.Add(tile.GetNeighbor(i));
                            }
                        }
                    }

                    // calculate pressure

                    // testing some changes here to fix some outlier cases and smooth elevations moer
                    // mainly just preventing elevation from being added to by multiple interactions
                    // a testing seed: 8603485
                    // 5922675

                    foreach (CubeCoordinates neighbor in plateNeighbors) {
                        var pressure = 0;
                        var neighborVector = wData.PlateDict[wData.WorldDict[neighbor].PlateCoords].Motion;
                        var r = plate.Motion - neighborVector;
                        var neighborDir = (neighbor - tile);
                        // the component of r along neighbor dir
                        pressure += neighborDir.x != 0 ? r.x : 0;
                        pressure += neighborDir.y != 0 ? r.y : 0;
                        pressure += neighborDir.z != 0 ? r.z : 0;

                        var neighborPlate = wData.PlateDict[wData.WorldDict[neighbor].PlateCoords];

                        // map pressure to elevation, THIS COULD USE A LOT OF TWEAKING
                        if (pressure > 0) {
                            if (plate.Oceanic == neighborPlate.Oceanic) {
                                // same plate type, directly colliding
                                if (plate.Oceanic) {
                                    wData.WorldDict[tile].Elevation = Mathf.Max(plate.DesiredElevation, neighborPlate.DesiredElevation);
                                    wData.WorldDict[tile].Elevation += (int)(pressure * 0.25f);
                                } else {
                                    wData.WorldDict[tile].Elevation = Mathf.Max(plate.DesiredElevation, neighborPlate.DesiredElevation);
                                    wData.WorldDict[tile].Elevation += (int)(pressure * 1.45f);
                                }
                            }
                            else if (plate.Oceanic == true && neighborPlate.Oceanic == false) {
                                // this is ocean, neighbor is land
                                wData.WorldDict[tile].Elevation = Mathf.Max(plate.DesiredElevation, neighborPlate.DesiredElevation);
                                wData.WorldDict[tile].Elevation += (int)(-pressure * 0.3f);
                            }
                            else if (plate.Oceanic == false && neighborPlate.Oceanic == true) {
                                // this is land, neighbor is ocean
                                wData.WorldDict[tile].Elevation = Mathf.Min(plate.DesiredElevation, neighborPlate.DesiredElevation);
                                wData.WorldDict[tile].Elevation += (int)(pressure * 0.25f);
                            }
                        }
                        else if (pressure < 0 && wData.WorldDict[tile].Elevation == 0) {
                            if (plate.Oceanic != neighborPlate.Oceanic) {
                                wData.WorldDict[tile].Elevation = (plate.DesiredElevation + neighborPlate.DesiredElevation) / 2;
                                wData.WorldDict[tile].Elevation += (int)Mathf.Abs(pressure * (plate.Oceanic ? 0.15f : 0.1f));
                            }
                            else {
                                if(plate.Oceanic) {
                                    wData.WorldDict[tile].Elevation = Mathf.Max(plate.DesiredElevation, neighborPlate.DesiredElevation);
                                    wData.WorldDict[tile].Elevation += (int)(Mathf.Abs(pressure) * 0.05f);
                                } else {
                                    wData.WorldDict[tile].Elevation = (plate.DesiredElevation + neighborPlate.DesiredElevation) / 2;
                                    wData.WorldDict[tile].Elevation += (int)(pressure * 0.2f);
                                }
                            }
                        }

                    }
                    // clamp values
                    if(wData.WorldDict[tile].Elevation < plate.MinElevation) {
                        wData.WorldDict[tile].Elevation = plate.MinElevation;
                    } else if(wData.WorldDict[tile].Elevation > plate.MaxElevation) {
                        wData.WorldDict[tile].Elevation = plate.MaxElevation;
                    }

                }

                var smoothingPasses = 4;
                while(smoothingPasses > 0) {
                    // testing some smoothing on the boundary elevations
                    foreach (CubeCoordinates tile in plate.BoundaryTiles) {
                        var boundarySmoothedElevation = wData.WorldDict[tile].Elevation;
                        var boundarySmoothing = 1;
                        if (wData.WorldDict[tile].Elevation >= 0) {
                            for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                                if (wData.WorldDict.TryGetValue(tile.GetNeighbor(d), out WorldTile value)) {
                                    if (value.Elevation > wData.WorldDict[tile].Elevation || (value.Elevation - wData.WorldDict[tile].Elevation) > -72) {
                                        boundarySmoothedElevation += value.Elevation;
                                        boundarySmoothing++;
                                    }
                                }
                            }
                        }
                        boundarySmoothedElevation /= boundarySmoothing;
                        wData.WorldDict[tile].Elevation = boundarySmoothedElevation;
                    }
                    smoothingPasses--;
                }

                /*
                 * Calculate inner tile elevations based on boundaries and fault types
                 * Ideally this looks logarithmic
                 * 
                 * Idea: warning, potentially computationally expensive
                 * Find both the closest and furthest boundary point, and lerp between them to determine elevation.
                 * 
                 */
                foreach(CubeCoordinates coords in plate.Tiles) {

                    if(wData.WorldDict[coords].Elevation == -100) {
                        // find closest boundary
                        CubeCoordinates[] closestBTiles = new CubeCoordinates[] {
                            plate.BoundaryTiles[0],
                            plate.BoundaryTiles[0],
                            plate.BoundaryTiles[0],
                            plate.BoundaryTiles[0]
                        };
                        foreach(CubeCoordinates bTile in plate.BoundaryTiles) {
                            if(CubeCoordinates.DistanceBetween(bTile, coords) < CubeCoordinates.DistanceBetween(closestBTiles[0], coords)) {
                                closestBTiles[0] = bTile;
                            }
                        }

                        foreach(CubeCoordinates bTile in plate.BoundaryTiles) {
                            if(!bTile.Equals(closestBTiles[0])) {
                                if (CubeCoordinates.DistanceBetween(bTile, coords) < CubeCoordinates.DistanceBetween(closestBTiles[1], coords)) {
                                    closestBTiles[1] = bTile;
                                }
                            }
                        }

                        foreach (CubeCoordinates bTile in plate.BoundaryTiles) {
                            if (!bTile.Equals(closestBTiles[0]) && !bTile.Equals(closestBTiles[1])) {
                                if (CubeCoordinates.DistanceBetween(bTile, coords) < CubeCoordinates.DistanceBetween(closestBTiles[2], coords)) {
                                    closestBTiles[2] = bTile;
                                }
                            }
                        }

                        foreach (CubeCoordinates bTile in plate.BoundaryTiles) {
                            if (!bTile.Equals(closestBTiles[0]) && !bTile.Equals(closestBTiles[1]) && !bTile.Equals(closestBTiles[2])) {
                                if (CubeCoordinates.DistanceBetween(bTile, coords) < CubeCoordinates.DistanceBetween(closestBTiles[3], coords)) {
                                    closestBTiles[3] = bTile;
                                }
                            }
                        }

                        var bDistance = (CubeCoordinates.DistanceBetween(closestBTiles[0], coords) 
                            + CubeCoordinates.DistanceBetween(closestBTiles[1], coords) 
                            + CubeCoordinates.DistanceBetween(closestBTiles[2], coords) 
                            + CubeCoordinates.DistanceBetween(closestBTiles[3], coords)) / 4;
                        //var bElevation = (world[closestBTiles[0]].Elevation + world[closestBTiles[1]].Elevation + world[closestBTiles[2]].Elevation) / 3;
                        //world[coords].Elevation = Mathf.RoundToInt(Coserp(plate.DesiredElevation, bElevation, Mathf.Pow(args.UpliftDecay, bDistance)));
                        var bElevation = (wData.WorldDict[closestBTiles[0]].Elevation 
                            + wData.WorldDict[closestBTiles[1]].Elevation 
                            + wData.WorldDict[closestBTiles[2]].Elevation + 
                            wData.WorldDict[closestBTiles[3]].Elevation) / 4f;
                        wData.WorldDict[coords].Elevation = Mathf.RoundToInt(Coserp(plate.DesiredElevation, 
                            bElevation, Mathf.Pow(args.UpliftDecay, bDistance)));
                        //world[coords].Elevation = Mathf.RoundToInt(bElevation * Mathf.Pow(args.UpliftDecay, bDistance));
                        //if(plate.Oceanic) {
                        //    world[coords].Elevation -= 1;
                        //}

                    }

                }

                foreach(CubeCoordinates coords in plate.Tiles) {
                    // some clamping, but with neighbor check to allow for small islands, but eliminate outliers
                    if (plate.Oceanic && wData.WorldDict[coords].Elevation > -1) {
                        var neighborLand = false;
                        for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                            if (wData.WorldDict.TryGetValue(coords.GetNeighbor(d), out WorldTile value) && value.Elevation > -1) {
                                neighborLand = true;
                                break;
                            }
                        }
                        if (!neighborLand) {
                            wData.WorldDict[coords].Elevation = -1;
                        }
                    }
                }

            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Elevations Calculated.");

            //// for the visuals
            //foreach (WorldTile tile in wData.WorldDict.Values) {
            //    //if (tile.Elevation < 0) {
            //    //    tile.Terrain = TerrainType.Ocean;
            //    //}
            //    if (tile.Elevation > 22) {
            //        tile.Terrain = TerrainType.Mountain;
            //    }
            //    else if (tile.Elevation > 15 && tile.Elevation <= 22) {
            //        tile.Terrain = TerrainType.Hill;
            //    }
            //    else if (tile.Elevation > 2 && tile.Elevation <= 15) {
            //        tile.Terrain = TerrainType.Land;
            //    }
            //    else {
            //        tile.Terrain = TerrainType.Sand;
            //    }
            //}

            /*
            * WEATHER
            */

            // cool seed: 8437681

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
            current = current + CubeCoordinates.Scale(CubeCoordinates.Permutations[0], args.SizeZ/2);
            while(wData.WorldDict.TryGetValue(current, out WorldTile _)) {
                equator.Add(current);
                wData.TempDict[current] = wData.WorldDict[current].Temperature = 30f;
                if (wData.WorldDict.TryGetValue(current.GetNeighbor(HexDirection.NE), out WorldTile _)) {
                    wData.TempDict[current.GetNeighbor(HexDirection.NE)] = wData.WorldDict[current.GetNeighbor(HexDirection.NE)].Temperature = 30f;
                    equator.Add(current.GetNeighbor(HexDirection.NE));
                }
                if (wData.WorldDict.TryGetValue(current.GetNeighbor(HexDirection.SE), out WorldTile _)) {
                    wData.TempDict[current.GetNeighbor(HexDirection.SE)] = wData.WorldDict[current.GetNeighbor(HexDirection.SE)].Temperature = 30f;
                    equator.Add(current.GetNeighbor(HexDirection.SE));
                }
                current.x += 2;
                current.y--;
                current.z--;
            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Equator Calculated.");

            /*
             * Assign temperatures for all tiles based on distance from the equator as well as elevation.
             */

            foreach (CubeCoordinates tile in wData.WorldDict.Keys) {
                if(!wData.TempDict.TryGetValue(tile, out float _)) {
                    var closestEqTile = equator[0];
                    foreach(CubeCoordinates eq in equator) {
                        if(CubeCoordinates.DistanceBetween(tile, closestEqTile) > CubeCoordinates.DistanceBetween(tile, eq)) {
                            closestEqTile = eq;
                        }
                    }
                    if(CubeCoordinates.DistanceBetween(tile, closestEqTile) < 3) {
                        wData.TempDict[tile] = 30f;
                    } else {
                        wData.TempDict[tile] = Coserp(-40f, 30f, Mathf.Pow(args.TemperatureDecay, CubeCoordinates.DistanceBetween(tile, closestEqTile)));
                    }
                    wData.WorldDict[tile].Temperature = wData.TempDict[tile];
                }
            }

            // elevation
            // a seed: 8319346
            foreach (CubeCoordinates tile in wData.TempDict.Keys) {
                var el = wData.WorldDict[tile].Elevation;
                // elevation should be able to drop the temp by about -20 to -40 degrees
                // so, as el gets closer to maxEl, we drop the temperature further, on some kind of exponential curve
                if(el > 18) {
                    var tempChange = Mathf.Lerp(-40f, 0f, Mathf.Pow(args.TemperatureDecayElevation, el - 9));
                    wData.WorldDict[tile].Temperature = wData.WorldDict[tile].Temperature + tempChange;
                } else if (el < -10) {
                    var tempChange = Mathf.Lerp(-7.5f, 0f, Mathf.Pow(args.TemperatureDecayElevation-1, Mathf.Abs(el)));
                    wData.WorldDict[tile].Temperature = wData.WorldDict[tile].Temperature + tempChange;
                }
            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Initial Temperature Assigned.");

            /*
             * It's wind time, this should be fun
             * 
             * First we need to divide the world into the appropriate air circulation cells
             * just like on earth. So we need to divide the world into 6 even sections.
             * We can most 
             */
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
            for(int z = 0; z < args.SizeZ; z++) {
                for(int x = 0; x < args.SizeX; x++) {
                    var tile = CubeCoordinates.OffsetToCube(x, z);
                    var windCell = windCells[0];
                    for(int i = 1; i < windCells.Length; i++) {
                        if(windCells[i].ToOffset().z < z) {
                            windCell = windCells[i];
                        } else {
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
                        mag = Coserp(0, 100, magT) * magnitudeMult;
                    }
                    // southern westerlies
                    else if (windCell.Equals(windCells[1])) {
                        dir = 180;
                        var t = Mathf.InverseLerp(windCells[1].ToOffset().z, windCells[2].ToOffset().z, z);
                        dir = (int)Mathf.Lerp(dir, 105, t);
                        var magT = Mathf.InverseLerp(windCells[1].ToOffset().z, windCells[2].ToOffset().z, z);
                        mag = Coserp(0, 100, magT) * magnitudeMult;
                    }
                    // Southeasterly trades
                    else if (windCell.Equals(windCells[2])) {
                        var t = Mathf.InverseLerp(windCells[2].ToOffset().z, windCells[3].ToOffset().z, z);
                        dir = (int)Mathf.Lerp(dir, -75, t);
                        var magT = Mathf.InverseLerp(windCells[3].ToOffset().z, windCells[2].ToOffset().z, z);
                        mag = Coserp(0f, 100f, magT) * magnitudeMult;
                    }
                    //northeasterly trades
                    else if (windCell.Equals(windCells[3])) {
                        var t = Mathf.InverseLerp(windCells[3].ToOffset().z, windCells[4].ToOffset().z, z);
                        dir = 180;
                        dir = (int)Mathf.Lerp(270, dir, t);
                        mag = Coserp(0f, 100f, t) * magnitudeMult;
                    }
                    // northern westerlies
                    else if (windCell.Equals(windCells[4])) {
                        var t = Mathf.InverseLerp(windCells[4].ToOffset().z, windCells[5].ToOffset().z, z);
                        dir = (int)Mathf.Lerp(75, dir, t);
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
                        wData.WindDict[tile] = new Tuple<int, float>(0, 0f);
                        wData.WorldDict[tile].Wind = wData.WindDict[tile];
                    }

                    // modify the wind speed based on elevation
                    var el = wData.WorldDict[tile].Elevation;
                    var mod = 0f;
                    if (el < 0) el = 0;
                    if (el < 4) {
                        mod = 1.3f;
                    }
                    else if (el < 20) {
                        mod = 1f + Mathf.InverseLerp(4, 19, el)/2;
                    }
                    else {
                        mod = 1.5f + Mathf.InverseLerp(20, 40, el);
                    }
                    if(mod != 0f) {
                        mag *= mod;
                    }
                    wData.WorldDict[tile].Wind = wData.WorldDict[tile].Wind = new Tuple<int, float>(dir, mag);
                }
            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Wind Patterns Created.");

            // Create initial humidity map, based on tempature and water
            foreach(CubeCoordinates tile in wData.WorldDict.Keys) {
                if(wData.WorldDict[tile].IsUnderwater) {
                    var t = Mathf.InverseLerp(-10, 30, wData.WorldDict[tile].Temperature);
                    wData.WorldDict[tile].Humidity = Coserp(0f, 50f, t);
                } else {
                    var t = Mathf.InverseLerp(-10, 30, wData.WorldDict[tile].Temperature);
                    wData.WorldDict[tile].Humidity = Coserp(2.5f, 10f, t);
                }
            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Humidity Created.");

            // use wind to move humidity and temperature around

            var passes = 20;
            for (int i = 0; i < passes; i++) {

                if (i < 15) {
                    foreach (CubeCoordinates tile in wData.WorldDict.Keys) {
                        if (wData.WorldDict[tile].IsUnderwater) {
                            var t = Mathf.InverseLerp(-10, 30, wData.WorldDict[tile].Temperature);
                            wData.WorldDict[tile].Humidity += Coserp(0f, 15f, t);
                        }
                    }
                }

                foreach (CubeCoordinates tile in wData.WorldDict.Keys) {
                    if(wData.WorldDict[tile].Humidity > wData.WorldDict[tile].MaxHumidity) {
                        wData.WorldDict[tile].Precipitation += wData.WorldDict[tile].Humidity * 0.25f;
                        wData.WorldDict[tile].Humidity -= wData.WorldDict[tile].Humidity * 0.25f;
                    }
                    var dir = wData.WorldDict[tile].Wind.Item1;
                    var mag = wData.WorldDict[tile].Wind.Item2;
                    if(mag < 0.05f) {
                        mag = 0.05f;
                    }
                    if (mag > 0) {
                        //var tempChange = (mag / 100) * 0.1f * wData.WorldDict[tile].Temperature;
                        var tempChange = 0;
                        var humidityChange = wData.WorldDict[tile].Humidity > wData.WorldDict[tile].MaxHumidity * 1.35f ? 
                            (wData.WorldDict[tile].Humidity - wData.WorldDict[tile].MaxHumidity) + (mag / 100f) * 0.45f * wData.WorldDict[tile].Humidity : (mag / 100f) * 0.65f * wData.WorldDict[tile].Humidity;
                        wData.WorldDict[tile].Temperature -= tempChange;
                        wData.WorldDict[tile].Humidity -= humidityChange;
                        if (dir >= -90 && dir < -45) {
                            // determine how much to push where
                            var nw = 0.5f;
                            var t = Mathf.InverseLerp(-90, -45, dir);
                            nw += Mathf.Lerp(0f, 0.5f, t);
                            var sw = 1 - nw;
                            var tcNW = nw * tempChange;
                            var tcSW = sw * tempChange;
                            var hcNW = nw * humidityChange;
                            var hcSW = sw * humidityChange;
                            // push temp and humidity
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.NW), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Temperature += tcNW;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Humidity += hcNW;
                            }
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.SW), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Temperature += tcSW;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Humidity += hcSW;
                            }
                        }
                        else if (dir >= -45 && dir < 0) {
                            var nw = Mathf.InverseLerp(0, -45, dir);
                            var n = Mathf.InverseLerp(-45, 0, dir);
                            var tcNW = nw * tempChange;
                            var tcN = n * tempChange;
                            var hcNW = nw * humidityChange;
                            var hcN = n * humidityChange;
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.NW), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Temperature += tcNW;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Humidity += hcNW;
                            }
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.N), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.N)].Temperature += tcN;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.N)].Humidity += hcN;
                            }
                        }
                        else if (dir >= 0 && dir < 45) {
                            var ne = Mathf.InverseLerp(0, 45, dir);
                            var n = Mathf.InverseLerp(45, 0, dir);
                            var tcNE = ne * tempChange;
                            var tcN = n * tempChange;
                            var hcNE = ne * humidityChange;
                            var hcN = n * humidityChange;
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.NE), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NE)].Temperature += tcNE;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NE)].Humidity += hcNE;
                            }
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.N), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.N)].Temperature += tcN;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.N)].Humidity += hcN;
                            }
                        }
                        else if (dir >= 45 && dir < 90) {
                            // determine how much to push where
                            var ne = 1f;
                            var t = Mathf.InverseLerp(45, 90, dir);
                            ne -= Mathf.Lerp(0f, 0.5f, t);
                            var se = 1 - ne;
                            var tcNE = ne * tempChange;
                            var tcSE = se * tempChange;
                            var hcNE = ne * humidityChange;
                            var hcSE = se * humidityChange;
                            // push temp and humidity
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.NE), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NE)].Temperature += tcNE;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NE)].Humidity += hcNE;
                            }
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.SE), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SE)].Temperature += tcSE;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SE)].Humidity += hcSE;
                            }
                        }
                        else if (dir >= 90 && dir < 135) {
                            // determine how much to push where
                            var ne = 0.5f;
                            var t = Mathf.InverseLerp(90, 135, dir);
                            ne -= Mathf.Lerp(0f, 0.5f, t);
                            var se = 1 - ne;
                            var tcNE = ne * tempChange;
                            var tcSE = se * tempChange;
                            var hcNE = ne * humidityChange;
                            var hcSE = se * humidityChange;
                            // push temp and humidity
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.NE), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NE)].Temperature += tcNE;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NE)].Humidity += hcNE;
                            }
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.SE), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SE)].Temperature += tcSE;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SE)].Humidity += hcSE;
                            }
                        }
                        else if (dir >= 135 && dir < 180) {
                            var se = Mathf.InverseLerp(180, 135, dir);
                            var s = Mathf.InverseLerp(135, 180, dir);
                            var tcSE = se * tempChange;
                            var tcS = s * tempChange;
                            var hcSE = se * humidityChange;
                            var hcS = s * humidityChange;
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.SE), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SE)].Temperature += tcSE;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SE)].Humidity += hcSE;
                            }
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.S), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.S)].Temperature += tcS;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.S)].Humidity += hcS;
                            }
                        }
                        else if (dir >= 180 && dir < 225) {
                            var s = Mathf.InverseLerp(225, 180, dir);
                            var sw = Mathf.InverseLerp(180, 225, dir);
                            var tcSW = sw * tempChange;
                            var tcS = s * tempChange;
                            var hcSW = sw * humidityChange;
                            var hcS = s * humidityChange;
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.SW), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Temperature += tcSW;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Humidity += hcSW;
                            }
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.S), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.S)].Temperature += tcS;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.S)].Humidity += hcS;
                            }
                        }
                        else if (dir >= 225 && dir < 270) {
                            var t = Mathf.InverseLerp(225, 270, dir);
                            var sw = Mathf.Lerp(1, 0.5f, t);
                            var nw = 1 - sw;
                            var tcNW = nw * tempChange;
                            var tcSW = sw * tempChange;
                            var hcNW = nw * humidityChange;
                            var hcSW = sw * humidityChange;
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.NW), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Temperature += tcNW;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Humidity += hcNW;
                            }
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.SW), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Temperature += tcSW;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Humidity += hcSW;
                            }
                        }
                        else if (dir >= 270) {
                            var sw = 0.5f;
                            var nw = 0.5f;
                            var tcNW = nw * tempChange;
                            var tcSW = sw * tempChange;
                            var hcNW = nw * humidityChange;
                            var hcSW = sw * humidityChange;
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.NW), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Temperature += tcNW;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.NW)].Humidity += hcNW;
                            }
                            if (wData.WorldDict.TryGetValue(tile.GetNeighbor(HexDirection.SW), out WorldTile _)) {
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Temperature += tcSW;
                                wData.WorldDict[tile.GetNeighbor(HexDirection.SW)].Humidity += hcSW;
                            }
                        }
                    }
                }

                // humidity smoothing
                foreach (CubeCoordinates tile in wData.WorldDict.Keys) {
                    var smoothedHumidity = wData.WorldDict[tile].Humidity;
                    var smoothing = 1;
                    for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                        if (wData.WorldDict.TryGetValue(tile.GetNeighbor(d), out WorldTile wt)) {
                            smoothedHumidity += wt.Humidity;
                            smoothing++;
                        }
                    }
                    smoothedHumidity /= smoothing;
                    wData.WorldDict[tile].Humidity = smoothedHumidity;
                }

            }

            // dump remaining humidity
            foreach (CubeCoordinates tile in wData.WorldDict.Keys) { 
                while(wData.WorldDict[tile].Humidity > wData.WorldDict[tile].MaxHumidity) {
                    wData.WorldDict[tile].Precipitation += wData.WorldDict[tile].Humidity * 0.25f;
                    wData.WorldDict[tile].Humidity *= 0.75f;
                }
            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Final Wind/Temp/Humidity + Interactions Complete.");
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Precipitation Calculated.");

            // precipitation smoothing
            passes = 3;
            for (int i = 0; i < passes; i++) {
                foreach (CubeCoordinates tile in wData.WorldDict.Keys) {
                    var smoothedPre = wData.WorldDict[tile].Precipitation;
                    var smoothing = 1;
                    for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                        if (wData.WorldDict.TryGetValue(tile.GetNeighbor(d), out WorldTile wt)) {
                            smoothedPre += wt.Precipitation;
                            smoothing++;
                        }
                    }
                    smoothedPre /= smoothing;
                    wData.WorldDict[tile].Precipitation = smoothedPre;
                }
            }

            // PROFILING
            Debug.Log(Time.realtimeSinceStartup.ToString() + ": Precipitation Smoothing Complete.");

            // For fun, I am going to implement some rough climate assignment and visuals
            // visuals
            foreach(CubeCoordinates tile in wData.WorldDict.Keys) {
                if (wData.WorldDict[tile].Temperature > 20) {
                    if (wData.WorldDict[tile].Humidity > 75) {
                        wData.WorldDict[tile].Terrain = TerrainType.TropRainForest;
                    }
                    else if (wData.WorldDict[tile].Humidity <= 75 && wData.WorldDict[tile].Humidity > 30) {
                        wData.WorldDict[tile].Terrain = TerrainType.TropForest;
                    }
                    else if (wData.WorldDict[tile].Humidity <= 30 && wData.WorldDict[tile].Humidity > 15) {
                        wData.WorldDict[tile].Terrain = TerrainType.Savanna;
                    }
                    else if (wData.WorldDict[tile].Humidity <= 15) {
                        wData.WorldDict[tile].Terrain = TerrainType.SubtropDesert;
                    }
                }
                else if (wData.WorldDict[tile].Temperature <= 20 && wData.WorldDict[tile].Temperature > 10) {
                    if (wData.WorldDict[tile].Humidity > 65) {
                        wData.WorldDict[tile].Terrain = TerrainType.TempRainForest;
                    }
                    else if (wData.WorldDict[tile].Humidity <= 65 && wData.WorldDict[tile].Humidity > 30) {
                        wData.WorldDict[tile].Terrain = TerrainType.TempDecidForest;
                    }
                    else if (wData.WorldDict[tile].Humidity <= 30 && wData.WorldDict[tile].Humidity > 10) {
                        wData.WorldDict[tile].Terrain = TerrainType.Woodland;
                    }
                    else if (wData.WorldDict[tile].Humidity <= 10) {
                        wData.WorldDict[tile].Terrain = TerrainType.Grassland;
                    }
                }
                else if (wData.WorldDict[tile].Temperature <= 10 && wData.WorldDict[tile].Temperature > 4) {
                    if (wData.WorldDict[tile].Humidity > 25) {
                        wData.WorldDict[tile].Terrain = TerrainType.TempDecidForest;
                    }
                    else if (wData.WorldDict[tile].Humidity <= 25 && wData.WorldDict[tile].Humidity > 10) {
                        wData.WorldDict[tile].Terrain = TerrainType.Woodland;
                    }
                    else if (wData.WorldDict[tile].Humidity <= 10) {
                        wData.WorldDict[tile].Terrain = TerrainType.Grassland;
                    }
                }
                else if (wData.WorldDict[tile].Temperature <= 4 && wData.WorldDict[tile].Temperature > -5) {
                    if (wData.WorldDict[tile].Humidity > 25) {
                        wData.WorldDict[tile].Terrain = TerrainType.Taiga;
                    }
                    else if (wData.WorldDict[tile].Humidity <= 25 && wData.WorldDict[tile].Humidity > 10) {
                        wData.WorldDict[tile].Terrain = TerrainType.Shrubland;
                    }
                    else if (wData.WorldDict[tile].Humidity <= 10) {
                        wData.WorldDict[tile].Terrain = TerrainType.SubtropDesert;
                    }
                } else if(wData.WorldDict[tile].Temperature <= -5) {
                    wData.WorldDict[tile].Terrain = TerrainType.Tundra;
                }
                if(wData.WorldDict[tile].Elevation > 22) {
                    wData.WorldDict[tile].Terrain = TerrainType.Mountain;
                } else if(wData.WorldDict[tile].Elevation <= 22 && wData.WorldDict[tile].Elevation > 19) {
                    wData.WorldDict[tile].Terrain = TerrainType.Hill;
                } else if(wData.WorldDict[tile].Elevation < 2) {
                    wData.WorldDict[tile].Terrain = TerrainType.Sand;
                }
            }

            return wData;

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
