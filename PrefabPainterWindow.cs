using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PrefabPainterWindow : EditorWindow
{
    private enum PaintMode
    {
        Single = 0,
        Brush = 1,
        Erase = 2
    }

    [Header("Basic Settings")]
    public GameObject prefabToPaint;
    public Transform parentForInstances;

    [Header("Brush")]
    public float brushRadius = 1.5f;
    public int strokeDensity = 5;
    public float surfaceOffset = 0.0f;
    public bool paintOnDrag = true;

    [Header("Randomization")]
    public bool randomRotationY = true;
    public bool alignToSurfaceNormal = true;
    public Vector2 randomScaleRange = new Vector2(1f, 1f);

    [Header("Grid & Layers")]
    public bool snapToGrid = false;
    public float gridSize = 1f;
    public bool useLayerFilter = false;
    public int targetLayer = 0;   // csak akkor számít, ha useLayerFilter = true

    [Header("Spawn Avoidance")]
    public bool enableSpawnAvoidance = false;
    public bool autoAvoidanceFromPrefab = true;
    public float avoidanceRadius = 1.0f;   // manuális minimális távolság
    public float avoidancePadding = 0.2f;  // auto mód: collider méret + padding

    [Header("Controls")]
    public bool requireShiftToPaint = true;

    private PaintMode paintMode = PaintMode.Single;

    // --- Window megnyitása a menüből ---
    [MenuItem("Tools/Prefab Painter Pro")]
    public static void ShowWindow()
    {
        var window = GetWindow<PrefabPainterWindow>("Prefab Painter Pro");
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    // ==========================
    // UI
    // ==========================
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Prefab Painter Pro", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        prefabToPaint = (GameObject)EditorGUILayout.ObjectField(
            "Prefab to Paint",
            prefabToPaint,
            typeof(GameObject),
            false
        );

        parentForInstances = (Transform)EditorGUILayout.ObjectField(
            "Parent (optional)",
            parentForInstances,
            typeof(Transform),
            true
        );

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
        paintMode = (PaintMode)GUILayout.Toolbar(
            (int)paintMode,
            new[] { "Single", "Brush", "Erase" }
        );
        EditorGUILayout.HelpBox("Hotkeys: S = Single, B = Brush, E = Erase", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        brushRadius = EditorGUILayout.Slider("Brush Radius", brushRadius, 0.1f, 20f);
        strokeDensity = EditorGUILayout.IntSlider("Stroke Density", strokeDensity, 1, 50);
        surfaceOffset = EditorGUILayout.Slider("Surface Offset", surfaceOffset, -1f, 1f);
        paintOnDrag = EditorGUILayout.Toggle("Paint/Erase While Dragging", paintOnDrag);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);
        alignToSurfaceNormal = EditorGUILayout.Toggle("Align to Surface Normal", alignToSurfaceNormal);
        randomRotationY = EditorGUILayout.Toggle("Random Y Rotation", randomRotationY);
        randomScaleRange = EditorGUILayout.Vector2Field("Random Scale Range", randomScaleRange);
        randomScaleRange.x = Mathf.Max(0.01f, randomScaleRange.x);
        randomScaleRange.y = Mathf.Max(randomScaleRange.x, randomScaleRange.y);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid & Layers", EditorStyles.boldLabel);
        snapToGrid = EditorGUILayout.Toggle("Snap to Grid", snapToGrid);
        if (snapToGrid)
        {
            gridSize = EditorGUILayout.Slider("Grid Size", gridSize, 0.1f, 10f);
        }

        useLayerFilter = EditorGUILayout.Toggle("Use Layer Filter", useLayerFilter);
        if (useLayerFilter)
        {
            targetLayer = EditorGUILayout.LayerField("Target Layer", targetLayer);
            EditorGUILayout.HelpBox("Raycasts will only hit this layer.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Layer filter disabled: raycasts hit all layers.", MessageType.None);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spawn Avoidance", EditorStyles.boldLabel);
        enableSpawnAvoidance = EditorGUILayout.Toggle("Enable Spawn Avoidance", enableSpawnAvoidance);

        if (enableSpawnAvoidance)
        {
            autoAvoidanceFromPrefab = EditorGUILayout.Toggle("Auto from Prefab Size", autoAvoidanceFromPrefab);

            if (autoAvoidanceFromPrefab)
            {
                avoidancePadding = EditorGUILayout.Slider("Padding", avoidancePadding, 0f, 2f);
                EditorGUILayout.HelpBox("Min distance is based on prefab colliders (diameter in XZ) + padding.", MessageType.None);
            }
            else
            {
                avoidanceRadius = EditorGUILayout.Slider("Min Distance", avoidanceRadius, 0.1f, 10f);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
        requireShiftToPaint = EditorGUILayout.Toggle("Require Shift to Paint", requireShiftToPaint);

        EditorGUILayout.HelpBox(
            "Usage:\n" +
            "- Open Scene view.\n" +
            "- Select Single / Brush / Erase.\n" +
            "- (Optional) Hold Shift while painting.\n" +
            "- Single: 1 prefab per click.\n" +
            "- Brush: multiple prefabs per stroke.\n" +
            "- Erase: removes painted objects under the brush.\n" +
            "- All operations support Undo (Ctrl+Z).",
            MessageType.Info
        );
    }

    // ==========================
    // Scene GUI
    // ==========================
    private void OnSceneGUI(SceneView sceneView)
    {
        HandleHotkeys();

        // Erase módban prefab nélkül is működhet
        if (prefabToPaint == null && paintMode != PaintMode.Erase)
            return;

        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        if (e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(controlId);
        }

        int layerMask = useLayerFilter ? (1 << targetLayer) : Physics.DefaultRaycastLayers;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, layerMask))
            return;

        DrawBrushGizmos(hitInfo.point, hitInfo.normal);

        bool correctModifier = !requireShiftToPaint || e.shift;
        bool isLeftButton = e.button == 0;
        bool isMouseDown = e.type == EventType.MouseDown && isLeftButton;
        bool isMouseDrag = e.type == EventType.MouseDrag && isLeftButton;

        bool shouldAct = false;
        switch (paintMode)
        {
            case PaintMode.Single:
                shouldAct = isMouseDown;
                break;
            case PaintMode.Brush:
            case PaintMode.Erase:
                shouldAct = isMouseDown || (paintOnDrag && isMouseDrag);
                break;
        }

        if (!correctModifier || !shouldAct)
            return;

        if (paintMode == PaintMode.Single)
        {
            if (snapToGrid)
                AdjustHitForGrid(ref hitInfo);

            PaintSingle(hitInfo);
        }
        else if (paintMode == PaintMode.Brush)
        {
            PaintStroke(hitInfo, layerMask);
        }
        else if (paintMode == PaintMode.Erase)
        {
            EraseStroke(hitInfo);
        }

        e.Use();
    }

    private void HandleHotkeys()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown || e.alt || e.control || e.command)
            return;

        bool handled = false;

        if (e.keyCode == KeyCode.S)
        {
            paintMode = PaintMode.Single;
            handled = true;
        }
        else if (e.keyCode == KeyCode.B)
        {
            paintMode = PaintMode.Brush;
            handled = true;
        }
        else if (e.keyCode == KeyCode.E)
        {
            paintMode = PaintMode.Erase;
            handled = true;
        }

        if (handled)
        {
            Repaint();
            e.Use();
        }
    }

    // ==========================
    // Painting logic
    // ==========================

    private void PaintSingle(RaycastHit hit)
    {
        if (prefabToPaint == null)
            return;

        float minDist = GetMinAvoidanceDistance();
        List<Vector3> existingPositions = enableSpawnAvoidance ? GetPaintedObjectPositions() : null;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        TryPlacePrefab(hit, existingPositions, null, minDist);

        Undo.CollapseUndoOperations(undoGroup);
    }

    private void PaintStroke(RaycastHit centerHit, int layerMask)
    {
        if (prefabToPaint == null)
            return;

        float minDist = GetMinAvoidanceDistance();
        List<Vector3> existingPositions = enableSpawnAvoidance ? GetPaintedObjectPositions() : null;
        List<Vector3> strokePositions = new List<Vector3>();

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < strokeDensity; i++)
        {
            Vector3 randomPoint = GetRandomPointInBrush(centerHit.point, centerHit.normal, brushRadius);

            if (snapToGrid)
                randomPoint = SnapToGridXZ(randomPoint);

            // Raycast fentről lefelé, hogy a felszínre igazítsuk
            Ray ray = new Ray(randomPoint + Vector3.up * 10f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hitInfo, 50f, layerMask))
            {
                TryPlacePrefab(hitInfo, existingPositions, strokePositions, minDist);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
    }

    private void EraseStroke(RaycastHit centerHit)
    {
        Collider[] colliders = Physics.OverlapSphere(centerHit.point, brushRadius);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var col in colliders)
        {
            if (col == null) continue;

            Transform t = col.transform;
            if (IsPaintedObject(t))
            {
                Undo.DestroyObjectImmediate(t.gameObject);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
    }

    /// <summary>
    /// Egyetlen prefab lerakására tett kísérlet.
    /// Ellenőrzi a távolságot a már létező és a stroke-on belül frissen lerakott pozíciókhoz.
    /// </summary>
    private bool TryPlacePrefab(
        RaycastHit hit,
        List<Vector3> existingPositions,
        List<Vector3> strokePositions,
        float minDist)
    {
        Vector3 position = hit.point + hit.normal * surfaceOffset;

        if (snapToGrid)
            position = SnapToGridXZ(position);

        if (enableSpawnAvoidance && minDist > 0f)
        {
            if (existingPositions != null && IsTooClose(position, existingPositions, minDist))
                return false;

            if (strokePositions != null && IsTooClose(position, strokePositions, minDist))
                return false;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToPaint);
        if (instance == null)
        {
            instance = Instantiate(prefabToPaint);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Paint Prefab");

        instance.transform.position = position;

        if (parentForInstances != null)
            instance.transform.SetParent(parentForInstances);

        if (alignToSurfaceNormal)
            instance.transform.up = hit.normal;

        if (randomRotationY)
            instance.transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.Self);

        if (randomScaleRange.x != 1f || randomScaleRange.y != 1f)
        {
            float s = Random.Range(randomScaleRange.x, randomScaleRange.y);
            instance.transform.localScale = Vector3.one * s;
        }

        if (strokePositions != null)
            strokePositions.Add(instance.transform.position);

        return true;
    }

    // ==========================
    // Helpers
    // ==========================

    private void DrawBrushGizmos(Vector3 center, Vector3 normal)
    {
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        if (snapToGrid)
            center = SnapToGridXZ(center);

        Handles.DrawWireDisc(center + normal * surfaceOffset, normal, brushRadius);
    }

    private void AdjustHitForGrid(ref RaycastHit hit)
    {
        if (!snapToGrid)
            return;

        hit.point = SnapToGridXZ(hit.point);
    }

    private Vector3 SnapToGridXZ(Vector3 position)
    {
        if (gridSize <= 0.0001f)
            return position;

        position.x = Mathf.Round(position.x / gridSize) * gridSize;
        position.z = Mathf.Round(position.z / gridSize) * gridSize;
        return position;
    }

    private Vector3 GetRandomPointInBrush(Vector3 center, Vector3 normal, float radius)
    {
        Vector2 randomCircle = Random.insideUnitCircle * radius;

        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.Cross(normal, Vector3.right);

        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent);

        Vector3 offset = tangent * randomCircle.x + bitangent * randomCircle.y;
        return center + offset;
    }

    private bool IsPaintedObject(Transform t)
    {
        if (t == null) return false;

        if (parentForInstances != null)
            return t == parentForInstances || t.IsChildOf(parentForInstances);

        if (prefabToPaint == null)
            return false;

        var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
        if (source == prefabToPaint)
            return true;

        return t.name.StartsWith(prefabToPaint.name);
    }

    private List<Vector3> GetPaintedObjectPositions()
    {
        List<Vector3> positions = new List<Vector3>();

        if (parentForInstances != null)
        {
            foreach (Transform child in parentForInstances)
                positions.Add(child.position);
        }
        else
        {
            Transform[] all = GameObject.FindObjectsOfType<Transform>();
            foreach (var t in all)
            {
                if (IsPaintedObject(t))
                    positions.Add(t.position);
            }
        }

        return positions;
    }

    private bool IsTooClose(Vector3 position, List<Vector3> others, float minDist)
    {
        float minDistSqr = minDist * minDist;
        foreach (var p in others)
        {
            if ((position - p).sqrMagnitude < minDistSqr)
                return true;
        }
        return false;
    }

    private float GetMinAvoidanceDistance()
    {
        if (!enableSpawnAvoidance)
            return 0f;

        if (!autoAvoidanceFromPrefab || prefabToPaint == null)
            return avoidanceRadius;

        float r = GetColliderRadius(prefabToPaint.transform);
        // 2 * r = “diaméter” – így biztosan nem lógnak egymásba
        return r * 2f + avoidancePadding;
    }

    // collider-alapú “érzékelési sugár” bármilyen objectre
    private float GetColliderRadius(Transform t)
    {
        if (t == null) return 0.5f;

        var capsule = t.GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
        {
            return capsule.radius * Mathf.Max(t.lossyScale.x, t.lossyScale.z);
        }

        var sphere = t.GetComponentInChildren<SphereCollider>();
        if (sphere != null)
        {
            return sphere.radius * Mathf.Max(t.lossyScale.x, t.lossyScale.z);
        }

        var box = t.GetComponentInChildren<BoxCollider>();
        if (box != null)
        {
            Vector3 scaledSize = Vector3.Scale(box.size, t.lossyScale);
            return Mathf.Max(scaledSize.x, scaledSize.z) * 0.5f;
        }

        var anyCol = t.GetComponentInChildren<Collider>();
        if (anyCol != null)
        {
            Bounds b = anyCol.bounds;
            return Mathf.Max(b.extents.x, b.extents.z);
        }

        return 0.5f;
    }
}
