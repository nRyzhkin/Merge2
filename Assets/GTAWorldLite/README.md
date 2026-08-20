# GTA World Lite

Self-contained **URP** package: procedural skybox, lightweight water, distance fog, and a single `GTAWorldController` for time-of-day / weather.

Does **not** include the GTA surface shaders (Default, Terrain, Foliage, Glass, Decal).

## Requirements

- Unity 6+ with **Universal Render Pipeline (URP)**
- A **Directional Light** in the scene (created automatically by World Controller if missing)

## Quick setup (new project)

1. Import this folder (`Assets/GTAWorldLite`) or install the exported `.unitypackage`.
2. **Edit → Project Settings → Graphics** — assign your URP asset if not already set.
3. Drag `Prefabs/GTA_WorldController.prefab` into the scene (or create empty GameObject → **GTA → World Controller**).
4. Sky material auto-assigns on enable (`Materials/GTA_Sky.mat`).
5. **Lighting → Environment** — skybox should point to `GTA_Sky` (set by controller).
6. Add water: drag `Water/Prefabs/Water_Plane.prefab` into the scene, position Y to water level, scale X/Z to cover your play area.
7. Select the water plane → **Bake Water Surface Depth** (inspector or **GTA → World Lite → Bake Water Surface Depth**).
8. Press Play — adjust **Time Of Day** and **Weather** on World Controller.

## Menu items

| Menu | Action |
|------|--------|
| **GTA → World Lite → Export .unitypackage** | Export this folder for other projects |
| **GTA → World Lite → Bake Textures (256×256)** | Generate default normal / foam / caustic maps |
| **GTA → World Lite → Bake Water Surface Depth** | Bake shore depth for all water planes in scene |
| **GTA → World Lite → Scene View → Fog** | Toggle fog in Scene View (editor only) |

## Shaders

| Shader | Use |
|--------|-----|
| `GTA/Sky` | Procedural skybox (no textures) |
| `GTA/WaterLite` | Transparent water with baked shore foam |

## Globals (set by World Controller)

`_GTA_SunDirection`, `_GTA_SunColor`, `_GTA_AmbientColor`, `_GTA_FogColor`, `_GTA_FogStart/End`, `_GTA_ReflectionIntensity`, sky palette globals, etc.

Water baker sets `_WaterSurfaceMap`, `_GTA_WaterLevel`, foam globals.

## Export to another project

**GTA → World Lite → Export .unitypackage…**  
Import the file in the target URP project. Only URP is required — no other GTA assets.

## File layout

```
GTAWorldLite/
  Shaders/          GTA/Sky + GTA/WaterLite + includes
  Runtime/          World Controller, water baker, fog settings
  Editor/           Bake tools, scene fog toggle, package exporter
  Materials/        GTA_Sky.mat
  Prefabs/          GTA_WorldController.prefab
  Water/
    Materials/      GTA_Water_Bright.mat
    Prefabs/        Water_Plane.prefab
    Textures/Lite/  Wave normal + foam/caustic
    Generated/      Baked depth maps (per scene)
```
