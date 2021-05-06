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
        public bool debug;

        public WorldGenerator(WorldArgs _args, bool _debug) {
            args = _args;
            debug = _debug;
        }

        public WorldGenerator(WorldArgs _args) {
            args = _args;
            debug = false;
        }

        public static Dictionary<CubeCoordinates, WorldTile> GenerateWorld(WorldArgs _args) {
            return new WorldGenerator(_args).GenerateWorld();
        }

        public Dictionary<CubeCoordinates, WorldTile> GenerateWorld() {

            // seeding
            if(args.UseStringSeed) {
                args.WorldSeed = args.StringSeed.GetHashCode();
            }
            if(args.RandomizeSeed) {
                args.WorldSeed = UnityEngine.Random.Range(0, 9999999);
            }
            UnityEngine.Random.InitState(args.WorldSeed);

            // create initial tiles
            var world = new Dictionary<CubeCoordinates, WorldTile>();
            for (int z = 0; z < args.SizeZ; z++) {
                for (int x = 0; x < args.SizeX; x++) {
                    var coords = CubeCoordinates.OffsetToCube(new Vector3(x, 0, z));
                    world[coords] = new WorldTile {
                        Coordinates = coords,
                        PlateCoords = new CubeCoordinates(), // default this, so that we can detect it later on ??
                        Terrain = TerrainType.None
                    };
                }
            }

            /*
             * PLATE TECTONICS
             */

            // generate points for tectonic plate origins
            Dictionary<CubeCoordinates, WorldPlate> plates = new Dictionary<CubeCoordinates, WorldPlate>();
            for (int i = 0; i < args.NumPlates; i++) {
                int randX = UnityEngine.Random.Range(0, args.SizeX);
                int randZ = UnityEngine.Random.Range(0, args.SizeZ);
                var _origin = CubeCoordinates.OffsetToCube(new Vector3(randX, 0f, randZ));
                // ensure that the point we pick has not yet been assigned to a plate
                while(!world[_origin].PlateCoords.Equals(new CubeCoordinates())) {
                    randX = UnityEngine.Random.Range(0, args.SizeX);
                    randZ = UnityEngine.Random.Range(0, args.SizeZ);
                    _origin = CubeCoordinates.OffsetToCube(new Vector3(randX, 0f, randZ));
                }
                plates[_origin] = new WorldPlate {
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
                var decay = 0.9996f;
                ffDeq.AddToBack(_origin);
                visited[_origin] = true;
                while (ffDeq.Count > 0) {
                    var coords = ffDeq.RemoveFromFront();
                    if (world[coords].PlateCoords.Equals(new CubeCoordinates())) {
                        world[coords].PlateCoords = _origin;
                        plates[_origin].Tiles.Add(coords);
                        plateSize++;
                        world[coords].Terrain = TerrainType.Debug; // debug
                        //if(plateSize >= maxPlateSize) {
                        //    break;
                        //}
                        if (chance >= UnityEngine.Random.Range(0f, 100f)) {
                            for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                                if (!visited.TryGetValue(coords.GetNeighbor(d), out bool _)) {
                                    if (world.TryGetValue(coords.GetNeighbor(d), out WorldTile _)) {
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
            foreach (WorldTile tile in world.Values) {
                if(tile.PlateCoords.Equals(new CubeCoordinates())) {
                    unassignedTiles.Enqueue(tile);
                }
            }
            Debug.Log(unassignedTiles.Count + " / " + args.SizeX * args.SizeZ);
            while (unassignedTiles.Count > 0) {
                var tile = unassignedTiles.Dequeue();
                var neighborsWithPlates = new List<WorldTile>();
                for(HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                    if(world.TryGetValue(tile.Coordinates.GetNeighbor(d), out WorldTile value) && !value.PlateCoords.Equals(new CubeCoordinates())) {
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
                    plates[tile.PlateCoords].Tiles.Add(tile.Coordinates);
                    world[tile.Coordinates].Terrain = TerrainType.Debug2;
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
            foreach (WorldPlate plate in plates.Values) {
                foreach (CubeCoordinates tile in plate.Tiles) {
                    for (HexDirection i = 0; i <= HexDirection.NW; i++) {
                        if (world.TryGetValue(tile.GetNeighbor(i), out WorldTile value)) {
                            if (!world[tile].PlateCoords.Equals(value.PlateCoords)) {
                                plate.BoundaryTiles.Add(tile);
                                world[tile].Terrain = TerrainType.Debug3; // debug
                                break;
                            }
                        }
                    }
                }
            }

            // generate random desired elevations for plates
            // also generate plate motion
            foreach (WorldPlate plate in plates.Values) {

                // desired elevation
                if (plate.Oceanic) {
                    plate.DesiredElevation = UnityEngine.Random.Range(-15, -1);
                }
                else {
                    plate.DesiredElevation = UnityEngine.Random.Range(0, 9);
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
                world[plate.Origin].MotionVector = new Tuple<CubeCoordinates, CubeCoordinates>(plate.Origin, CubeCoordinates.Lerp(plate.Origin, plate.DriftAxis, args.PlateMotionScaleFactor));

            }

            // set elevations to bogus number
            foreach (WorldTile tile in world.Values) {
                tile.Elevation = -100;
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
            foreach (WorldPlate plate in plates.Values) {

                foreach (CubeCoordinates tile in plate.BoundaryTiles) {
                    // zero out elevation
                    world[tile].Elevation = 0;

                    List<CubeCoordinates> plateNeighbors = new List<CubeCoordinates>();
                    for (HexDirection i = HexDirection.N; i <= HexDirection.NW; i++) {
                        if (world.TryGetValue(tile.GetNeighbor(i), out WorldTile value)) {
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
                        var neighborVector = plates[world[neighbor].PlateCoords].Motion;
                        var r = plate.Motion - neighborVector;
                        var neighborDir = (neighbor - tile);
                        // the component of r along neighbor dir
                        pressure += neighborDir.x != 0 ? r.x : 0;
                        pressure += neighborDir.y != 0 ? r.y : 0;
                        pressure += neighborDir.z != 0 ? r.z : 0;
                        //Debug.Log("Self: " + plate.Motion + " | Neighbor: " + neighborVector + " | Resultant: " + r
                        // + " | NeighborDir: " + neighborDir + " | Pressure: " + pressure);

                        // map pressure to elevation, THIS COULD USE A LOT OF TWEAKING
                        if (pressure > 0) {
                            if (plate.Oceanic == plates[world[neighbor].PlateCoords].Oceanic) {
                                // same plate type, directly colliding
                                if (plate.Oceanic) {
                                    world[tile].Elevation = Mathf.Max(plate.DesiredElevation, plates[world[neighbor].PlateCoords].DesiredElevation);
                                    world[tile].Elevation += (int)(Mathf.Abs(pressure) * 0.25f);
                                } else {
                                    world[tile].Elevation = Mathf.Max(plate.DesiredElevation, plates[world[neighbor].PlateCoords].DesiredElevation);
                                    world[tile].Elevation += (int)(Mathf.Abs(pressure) * 0.65f);
                                }
                            }
                            else if (plate.Oceanic == true && plates[world[neighbor].PlateCoords].Oceanic == false) {
                                // this is ocean, neighbor is land
                                world[tile].Elevation = (int)Mathf.Lerp(plate.DesiredElevation, plates[world[neighbor].PlateCoords].DesiredElevation, 0.75f);
                                world[tile].Elevation += (int)(Mathf.Abs(pressure) * 0.1f);
                            }
                            else if (plate.Oceanic == false && plates[world[neighbor].PlateCoords].Oceanic == true) {
                                // this is land, neighbor is ocean
                                world[tile].Elevation = (int)Mathf.Lerp(plate.DesiredElevation, plates[world[neighbor].PlateCoords].DesiredElevation, 0.15f);
                                world[tile].Elevation += (int)(Mathf.Abs(pressure) * 0.35f);
                            }
                        }
                        else if (pressure < 0 && world[tile].Elevation == 0) {
                            world[tile].Elevation = (plate.DesiredElevation + plates[world[neighbor].PlateCoords].DesiredElevation) / 2;
                            world[tile].Elevation += (int)(pressure * 0.02f);
                            if (plate.Oceanic && world[tile].Elevation > 2) {
                                world[tile].Elevation = 2; // clamp oceanic plates to 2 elevation, just above water
                            }
                        }

                    }
                    // clamp values
                    if(world[tile].Elevation < plate.MinElevation) {
                        world[tile].Elevation = plate.MinElevation;
                    } else if(world[tile].Elevation > plate.MaxElevation) {
                        world[tile].Elevation = plate.MaxElevation;
                    }

                }

                var smoothingPasses = 3;
                while(smoothingPasses > 0) {
                    // testing some smoothing on the boundary elevations
                    foreach (CubeCoordinates tile in plate.BoundaryTiles) {
                        var boundarySmoothedElevation = world[tile].Elevation;
                        var boundarySmoothing = 1;
                        if (world[tile].Elevation >= 0) {
                            for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                                if (world.TryGetValue(tile.GetNeighbor(d), out WorldTile value)) {
                                    if (value.Elevation > world[tile].Elevation || (value.Elevation - world[tile].Elevation) > -13) {
                                        boundarySmoothedElevation += value.Elevation;
                                        boundarySmoothing++;
                                    }
                                }
                            }
                        } // else {
                          //    for(HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                          //        if(world.TryGetValue(tile.GetNeighbor(d), out WorldTile value)) {
                          //            if(value.Elevation < world[tile].Elevation) {
                          //                boundarySmoothedElevation += value.Elevation;
                          //                boundarySmoothing++;
                          //            }
                          //        }
                          //    }
                          //}
                        boundarySmoothedElevation /= boundarySmoothing;
                        world[tile].Elevation = boundarySmoothedElevation;
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

                    if(world[coords].Elevation == -100) {
                        // find closest boundary
                        CubeCoordinates[] closestBTiles = new CubeCoordinates[] {
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

                        var bDistance = (CubeCoordinates.DistanceBetween(closestBTiles[0], coords) + CubeCoordinates.DistanceBetween(closestBTiles[1], coords) + CubeCoordinates.DistanceBetween(closestBTiles[2], coords)) / 3;
                        var bElevation = (world[closestBTiles[0]].Elevation + world[closestBTiles[1]].Elevation + world[closestBTiles[2]].Elevation) / 3;
                        world[coords].Elevation = Mathf.RoundToInt(Mathf.SmoothStep(plate.DesiredElevation, bElevation, Mathf.Pow(args.UpliftDecay, bDistance)));
                        //world[coords].Elevation = Mathf.RoundToInt(bElevation * Mathf.Pow(args.UpliftDecay, bDistance));
                        //if(plate.Oceanic) {
                        //    world[coords].Elevation -= 1;
                        //}

                    }

                }

                foreach(CubeCoordinates coords in plate.Tiles) {
                    // some clamping, but with neighbor check to allow for small islands, but eliminate outliers
                    if (plate.Oceanic && world[coords].Elevation > -1) {
                        var neighborLand = false;
                        for (HexDirection d = HexDirection.N; d <= HexDirection.NW; d++) {
                            if (world.TryGetValue(coords.GetNeighbor(d), out WorldTile value) && value.Elevation > -1) {
                                neighborLand = true;
                                break;
                            }
                        }
                        if (!neighborLand) {
                            world[coords].Elevation = -1;
                        }
                    }
                }

            }

            // for the visuals
            foreach (WorldTile tile in world.Values) {
                if(debug) {
                    var exists = plates.TryGetValue(tile.Coordinates, out WorldPlate _p);
                    if (exists) {
                        tile.Terrain = TerrainType.Debug2; // used to represent plate centers for now
                    }
                } else {
                    if (tile.Elevation < 0) {
                        tile.Terrain = TerrainType.Ocean;
                    }
                    else if (tile.Elevation > 15) {
                        tile.Terrain = TerrainType.Mountain;
                    }
                    else if (tile.Elevation > 10 && tile.Elevation <= 15) {
                        tile.Terrain = TerrainType.Hill;
                    }
                    else if(tile.Elevation > 0 && tile.Elevation <= 10) {
                        tile.Terrain = TerrainType.Land;
                    } else {
                        tile.Terrain = TerrainType.Sand;
                    }
                }
            }

            //foreach(WorldPlate plate in plates.Values) {
            //    foreach(CubeCoordinates tile in plate.BoundaryTiles) {
            //        world[tile].Terrain = TerrainType.Debug;
            //    }
            //}

            /*
            * WEATHER
            */
            Dictionary<CubeCoordinates, CubeCoordinates> windCurrentMap = new Dictionary<CubeCoordinates, CubeCoordinates>();
            Dictionary<CubeCoordinates, float> temperatureMap = new Dictionary<CubeCoordinates, float>();
            Dictionary<CubeCoordinates, float> moistureMap = new Dictionary<CubeCoordinates, float>();

            // cool seed: 8437681

            /*
             * Determine which tiles fall along the equator. 
             * 
             * To do this, we are going to select all tiles that can be reached via a (+2, -1, -1) or (-2, +1, +1)
             * permutation from (0, 0, 0). Then, we will select their neighbors in the NW, SW, NE, SE directions.
             */
            var equator = new List<CubeCoordinates>();
            var current = new CubeCoordinates(0, 0, 0);
            current = current + CubeCoordinates.Scale(CubeCoordinates.Permutations[0], args.SizeZ/2);
            Debug.Log(world.TryGetValue(current, out WorldTile _));
            while(world.TryGetValue(current, out WorldTile _)) {
                equator.Add(current);
                temperatureMap[current] = world[current].Temperature = 30f;
                if (world.TryGetValue(current.GetNeighbor(HexDirection.NE), out WorldTile _)) {
                    temperatureMap[current.GetNeighbor(HexDirection.NE)] = world[current.GetNeighbor(HexDirection.NE)].Temperature = 30f;
                    equator.Add(current.GetNeighbor(HexDirection.NE));
                }
                if (world.TryGetValue(current.GetNeighbor(HexDirection.SE), out WorldTile _)) {
                    temperatureMap[current.GetNeighbor(HexDirection.SE)] = world[current.GetNeighbor(HexDirection.SE)].Temperature = 30f;
                    equator.Add(current.GetNeighbor(HexDirection.SE));
                }
                current.x += 2;
                current.y--;
                current.z--;
            }

            //foreach(CubeCoordinates tile in equator) {
            //    world[tile].Terrain = TerrainType.Debug3;
            //}

            foreach(CubeCoordinates tile in world.Keys) {
                if(!temperatureMap.TryGetValue(tile, out float _)) {
                    var closestEqTile = equator[0];
                    foreach(CubeCoordinates eq in equator) {
                        if(CubeCoordinates.DistanceBetween(tile, closestEqTile) > CubeCoordinates.DistanceBetween(tile, eq)) {
                            closestEqTile = eq;
                        }
                    }
                    if(CubeCoordinates.DistanceBetween(tile, closestEqTile) < 3) {
                        temperatureMap[tile] = 30f;
                    } else {
                        temperatureMap[tile] = Mathf.SmoothStep(-40f, 30f, Mathf.Pow(0.99f, CubeCoordinates.DistanceBetween(tile, closestEqTile)));
                    }
                    world[tile].Temperature = temperatureMap[tile];
                }
            }

            // a seed: 8319346
            foreach (CubeCoordinates tile in temperatureMap.Keys) {
                var el = world[tile].Elevation;
                // elevation should be able to drop the temp by about -20 to -40 degrees
                // so, as el gets closer to maxEl, we drop the temperature further, on some kind of exponential curve
                if(el > 9) {
                    var tempChange = Mathf.Lerp(-40f, 0f, Mathf.Pow(0.96f, el - 9));
                    world[tile].Temperature = world[tile].Temperature + tempChange;
                } else if (el < -9) {
                    var tempChange = Mathf.Lerp(-10f, 0f, Mathf.Pow(0.95f, Mathf.Abs(el)));
                    world[tile].Temperature = world[tile].Temperature + tempChange;
                }
            }

            // for temperature visuals. should remove later
            //foreach (WorldTile tile in world.Values) {
            //    if (temperatureMap[tile.Coordinates] >= 28.0f) {
            //        tile.Terrain = TerrainType.Debug3;
            //    } else if (temperatureMap[tile.Coordinates] >= 15.0f) {
            //        tile.Terrain = TerrainType.Debug;
            //    } else if (temperatureMap[tile.Coordinates] >= 7.5f) {
            //        tile.Terrain = TerrainType.Sand;
            //    } else if (temperatureMap[tile.Coordinates] >= -2.5f) {
            //        tile.Terrain = TerrainType.Debug4;
            //    } else if (temperatureMap[tile.Coordinates] >= -20.0f) {
            //        tile.Terrain = TerrainType.Ocean;
            //    } else if (temperatureMap[tile.Coordinates] >= - 30.0f) {
            //        tile.Terrain = TerrainType.Debug2;
            //    } else {
            //        tile.Terrain = TerrainType.Debug5;
            //    }
            //}

            return world;

        }

    }
}
