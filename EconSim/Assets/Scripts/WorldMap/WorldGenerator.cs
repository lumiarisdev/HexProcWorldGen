using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Nito.Collections;

namespace EconSim
{
    public class WorldGenerator
    {

        private WorldArgs args;

        public WorldGenerator(WorldArgs _args) {
            args = _args;
        }

        public static WorldMapData GenerateWorld(WorldArgs _args) {
            return new WorldGenerator(_args).GenerateWorld();
        }

        public WorldMapData GenerateWorld() {

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
                wData.PlateDict[_origin] = new WorldPlate {
                    Origin = _origin,
                    Tiles = new List<CubeCoordinates>(),
                    BoundaryTiles = new List<CubeCoordinates>(),
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
             * it may cause performance problems. The cost of the less straightforward solution is O(boundaryTiles)
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

            // generate random desired elevations for plates
            // also generate plate motion
            foreach (WorldPlate plate in wData.PlateDict.Values) {

                // desired elevation
                if (plate.Oceanic) {
                    plate.DesiredElevation = UnityEngine.Random.Range(-45, -1);
                }
                else {
                    plate.DesiredElevation = UnityEngine.Random.Range(4, 40);
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

            // for the visuals
            foreach (WorldTile tile in wData.WorldDict.Values) {
                //if (tile.Elevation < 0) {
                //    tile.Terrain = TerrainType.Ocean;
                //}
                if (tile.Elevation > 64) {
                    tile.Terrain = TerrainType.Mountain;
                }
                else if (tile.Elevation > 46 && tile.Elevation <= 64) {
                    tile.Terrain = TerrainType.Hill;
                }
                else if (tile.Elevation > 4 && tile.Elevation <= 46) {
                    tile.Terrain = TerrainType.Land;
                }
                else {
                    tile.Terrain = TerrainType.Sand;
                }
            }

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

            /*
             * Assign temperatures for all tiles based on distance from the equator as well as elevation.
             */

            foreach(CubeCoordinates tile in wData.WorldDict.Keys) {
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

            // a seed: 8319346
            foreach (CubeCoordinates tile in wData.TempDict.Keys) {
                var el = wData.WorldDict[tile].Elevation;
                // elevation should be able to drop the temp by about -20 to -40 degrees
                // so, as el gets closer to maxEl, we drop the temperature further, on some kind of exponential curve
                if(el > 46) {
                    var tempChange = Mathf.Lerp(-40f, 0f, Mathf.Pow(args.TemperatureDecayElevation, el - 9));
                    wData.WorldDict[tile].Temperature = wData.WorldDict[tile].Temperature + tempChange;
                } else if (el < -46) {
                    var tempChange = Mathf.Lerp(-7.5f, 0f, Mathf.Pow(args.TemperatureDecayElevation-1, Mathf.Abs(el)));
                    wData.WorldDict[tile].Temperature = wData.WorldDict[tile].Temperature + tempChange;
                }
            }

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
            // uncomment to assign wind to the cell tiles, for debugging
            //foreach(CubeCoordinates cell in windCells) {
            //    wData.WindDict[cell] = new Vector3((Mathf.InverseLerp(windCells[0].z, windCells[1].z, cell.z) * -2f) + CubeCoordinates.Permutations[0].x,
            //            (Mathf.InverseLerp(windCells[0].z, windCells[1].z, cell.z) * 1f) + CubeCoordinates.Permutations[0].y,
            //          (Mathf.InverseLerp(windCells[0].z, windCells[1].z, cell.z) * 1f) + CubeCoordinates.Permutations[0].z
            //        );
            //}

            // x >=
            // y <
            // 

            foreach (CubeCoordinates tile in wData.WorldDict.Keys) {
                // determine wind cell
                var windCell = windCells[0];
                for (int i = 1; i < windCells.Length; i++) {
                    if ((windCells[i].x <= tile.x && windCells[i].y > tile.y)) {
                        windCell = windCells[i];
                    }
                }
                // wind for wind cell 0
                if (windCell.Equals(windCells[0])) {
                    var windDirection = new Vector3(
                        (Mathf.InverseLerp(windCells[0].z, windCells[1].z, tile.z) * -2f) + CubeCoordinates.Permutations[0].x,
                        (Mathf.InverseLerp(windCells[0].z, windCells[1].z, tile.z) * 1f) + CubeCoordinates.Permutations[0].y,
                        (Mathf.InverseLerp(windCells[0].z, windCells[1].z, tile.z) * 1f) + CubeCoordinates.Permutations[0].z
                        );
                    var windMag = Mathf.InverseLerp(windCells[1].z, windCells[0].z, tile.z) * 100f;
                    var wind = windDirection; // * windMag;
                    wData.WindDict[tile] = wind;
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
