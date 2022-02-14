# HexProcWorldGen
A procedural world generator written in C#, visuals done with the Unity game engine. WASD for camera movement.

The world generation code is located primarily in Assets/Scripts/WorldMap/. Code for the visuals is likewise primarily in Assets/Scripts/HexMap/.

HexProcWorldGen/Assets/Scripts/WorldMap/WorldGenerator.cs contains the generator class and functions.

Notes:
!! Performance of certain functions in the generator is closely related to the map size set. Current default size is 80,000 tiles. Particularly the generator can spend a lot of time doing precipitation calculations. If the program hangs about 60-70% of the way to completion, allow it to continue generating. On an i7-6700k, the default world size takes just under 2 minutes. !!

"EconSim" is an early prototype name and I use it for the namespace that contains the World-generation code.

Generally speaking code that is related only to the visuals is not contained in this namespace.

The project is WIP but currently the following generator features are implemented:

## Plate tectonics
Landmasses and bodies of water a created via simulated plate tectonics. A lazy flood-fill algorithm is used to assign each independent unit of map-space (a tile) to a "plate". Each plate is assigned a vector that represents its motions. These plates and their motion vectors are then used to calculate the world's heightmap in a way that produces geologically-believable results.

## Temperature
A temperature map is generated using distance from equator and height.

## Wind
A vector field is generated to represent wind conditions across the map. THe 6 primary wind cells of are simulated by changing the calculations for the flow field at intervals corresponding to the appropriate latitudes. Wind is also used to modify the previously-created temperature map as well.

## Precipitation
A combination of the previous data maps are used to calculate precipitation for each tile. Humidity is calculated using the values of water, wind, height, and temperature at each location. The humidity is then applied to precipitation when a tile exceeds its maximum humidity capacity.

## Climate assignment
A basic climate zone from a paired down selection of the Koppen climate classification system is assigned to each tile.

## Rivers [WIP]
Rivers are generated using the above maps and are drawn from high-elevation to low in areas with enough precipitation. Some erosion is applied based on the amount of precipitation. Currently this method of generation creates a number of small rivers in similar locations, and can sometimes create narrow values with the erosion. Most testing and adjustment needed.

## Time flow post-generation [WIP]
A basic timeflow is currently set up but is not displayed to the end user. Working on an adequate way to convert this "SimTime" to an appropriate C# DateTime object.
See WorldTime.cs

# Planned:

Implement air pressure simulation to create more nuanced and realistic precipitation generation.
Tweak river generation to allow for longer meandering river structures
