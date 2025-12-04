# Prefab-Painter-Unity-Editor-Tool
Prefab Painter is a Unity Editor extension that allows designers and environment artists to quickly place prefabs directly inside the Scene view. It provides brush-based painting, single placement, erase mode, and several level-design-friendly utilities.

Features
Painting Modes

Single Mode — place exactly one prefab per click

Brush Mode — distribute multiple prefabs inside a circular brush area

Erase Mode — removes previously painted objects inside the brush radius

Placement Controls

Paint on Drag — continuously paint while moving the mouse

Surface alignment — rotate objects to match the hit normal

Random scale / random rotation — natural variation for vegetation, rocks, etc.

Surface offset — slightly raise objects above the terrain

Grid Snapping

Snap prefab placement to a configurable XZ grid
Useful for cities, modular assets, or architectural environments.

Layer Filtering

Raycast only against a target layer (e.g., Terrain)

Avoids painting on unwanted colliders

Spawn Avoidance

Prevents placing a prefab too close to an existing one

Keeps spacing clean, avoids intersecting objects

🎹 Hotkeys

S → Single Mode

B → Brush Mode

E → Erase Mode

Undo Support

All actions support Unity Undo (Ctrl + Z )

Demo GIF

(Add your GIF / short video here)

painting vegetation on terrain

switching erase mode

grid snapping example

How to Use

Open the tool: Tools → Prefab Painter

Pick a prefab (drag&drop)

Select a painting mode

Click or drag in Scene View to paint

Technical Highlights

EditorWindow & SceneView interaction
SceneView.duringSceneGui, HandleUtility.GUIPointToWorldRay

Prefab spawning
PrefabUtility.InstantiatePrefab

Prefab origin detection
GetCorrespondingObjectFromSource

Undo stack
Undo.RegisterCreatedObjectUndo, grouped modifications

Brush visualization
Handles.DrawWireDisc

Raycasting with layer mask
Physics.Raycast(mask)

Overlap detection for spawn avoidance
Physics.OverlapSphere()

Requirements

Unity 2021+ (tested on 2021/2022/2023)

Works with any render pipeline (URP/HDRP/Built-in)

License

MIT — feel free to use or modify.
