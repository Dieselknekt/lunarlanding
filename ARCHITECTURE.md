# Lunar Landing Site Navigator — Project Architecture & AI Context

**Challenge:** NASA Space Apps Challenge 2026 — Lunar Landing Site Navigator  
**Engine:** Unity 6 / URP (Universal 3D), C# (100% English in-game & codebase)  
**Repo Root:** `C:\projects\lunarlanding\`  
**Unity Root:** `C:\projects\lunarlanding\lunarunity\` (`.gitignore` placed inside `lunarunity\`)  

---

## 1. Vision & Core Gameplay Loop
An interactive 3D lunar landing site simulator and analysis tool built around two seamless perspectives:
1. **Orbital / System View (`OrbitalCamera`):** Shows the relative 3D positions, lighting, and shadows of the **Sun**, **Earth**, and **Moon** at a given simulation timestamp. Allows users to inspect Line-of-Sight (LOS) coverage across the lunar surface for solar power and Earth communication, and click any surface point to analyze it.
2. **Lunar Surface FPS View (`FPSCamera`):** Teleports the user down to `1.7` units above the selected landing point, aligning the player's local "up" vector to the spherical lunar surface normal. Users can walk around (WASD + mouse look) and scrub simulation time to observe the Sun or Earth rising/setting behind crater rims and mountains.

---

## 2. Key Technical Decisions & Constraints
* **Language Policy:** All C# code, comments, tooltips, Inspector labels, and in-game UI must be strictly in **English**.
* **Moon-Centered Origin:** `Moon` is fixed at `(0, 0, 0)` (initial placeholder radius `50` / Scale `100`). `SunPivot` and `EarthPivot` rotate around `(0, 0, 0)`. This avoids floating-point precision jitter when exploring the surface in FPS mode.
* **No Colliders on Celestial Visuals:** `SunVisual` and `EarthVisual` have their `SphereCollider` components removed so LOS raycasts from the surface only test against lunar terrain (and future dedicated eclipse/diffraction checks) and mouse clicks don't hit invisible spheres.
* **Scalability for Physics:** LOS logic is structured to expand from pure optical raycasts to **radio wave propagation / Fresnel knife-edge diffraction** around crater rims, as well as **Earthshine** and **lunar eclipse** shadow/refraction calculations.

---

## 3. Current Scene Hierarchy (`SampleScene`)
* `Global Volume` — Default URP post-processing volume (kept).
* `Moon` — Sphere at `(0, 0, 0)`, Scale `(100, 100, 100)` *(temporary placeholder; will be replaced with high-res cratered terrain / NASA LRO DEM mesh with `MeshCollider`)*.
* `SunPivot` — Empty GameObject at `(0, 0, 0)`
  * `SunLight` — Directional Light, Rotation `(0, 180, 0)`, Soft Shadows enabled.
  * `SunVisual` — Sphere at `(0, 0, 500)`, Scale `(20, 20, 20)`, `SphereCollider` removed.
* `EarthPivot` — Empty GameObject at `(0, 0, 0)`
  * `EarthVisual` — Sphere at `(0, 0, 300)`, Scale `(15, 15, 15)`, `SphereCollider` removed.
* `OrbitalCamera` — Active by default, Position `(0, 60, -150)`.
* `FPSCamera` — Inactive by default, Position `(0, 0, 0)`.
* `GameManager` — Empty GameObject at `(0, 0, 0)` hosting core scripts.

---

## 4. C# Scripts (`Assets/Scripts/`) & Current Status
- [x] `CelestialSystem.cs` — Attached to `GameManager`. Controls `sunPivot` and `earthPivot` rotations over `simulationDays` (synodic month 29.53d, axial tilt 6.68°, Earth libration 7°).
- [ ] `LunarLOSAndLanding.cs` — *(Next)* Raycast site selection on `Moon`, slope calculation, Sun/Earth LOS raycasts, and `CheckRadioDiffraction` hook.
- [ ] `DualCameraController.cs` — *(Next)* Right-drag/scroll orbit controls + spherical-gravity FPS movement (`Tab` to return to orbit).
- [ ] `NavigatorUI.cs` — *(Planned)* TextMeshPro HUD (time slider, play/pause, telemetry readout, "Land / Enter FPS" button).

---

## 5. Backlog & Planned Upgrades
1. Implement `LunarLOSAndLanding.cs` and `DualCameraController.cs`.
2. Build `NavigatorUI.cs` (English HUD).
3. Replace `Moon` placeholder sphere with high-detail crater terrain / NASA LRO heightmap mesh so mountains occlude the Sun/Earth in FPS view.
4. Add radio wave diffraction (Fresnel zone / knife-edge) to `CheckRadioDiffraction` for Earth LOS near the lunar south pole.
5. Add Earth lighting/phases (Earthshine) and lunar eclipse shadow/atmospheric refraction when Earth passes between Sun and Moon.