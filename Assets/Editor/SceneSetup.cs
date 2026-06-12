using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

[InitializeOnLoad]
public class SceneSetup
{
    static SceneSetup()
    {
        EditorApplication.delayCall += () =>
        {
            DumpHierarchy();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!EditorPrefs.GetBool("RoomGame_SceneSetup_Done_V32", false))
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (scene.isDirty)
                {
                    Debug.LogWarning("SceneSetup: Active scene has unsaved changes. Skipping automatic setup.");
                    return;
                }
                Setup();
                EditorPrefs.SetBool("RoomGame_SceneSetup_Done_V32", true);
            }
        };
    }

    private static void DumpHierarchy()
    {
        try
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            string dumpPath = "hierarchy_dump.txt";
            using (var writer = new System.IO.StreamWriter(dumpPath, false))
            {
                writer.WriteLine("Active Scene: " + scene.name);
                var rootObjects = scene.GetRootGameObjects();
                foreach (var rootObj in rootObjects)
                {
                    DumpTransform(rootObj.transform, "", writer);
                }
            }
            Debug.Log("Dumped scene hierarchy to " + dumpPath);

            // Also inspect FBX structures
            InspectFBX("Assets/desk (6).fbx", "desk_6_fbx_structure.txt");
            InspectFBX("Assets/Room desk and computer models/Untitled.fbx", "untitled_fbx_structure.txt");

            // Inspect room door
            InspectRoomDoor();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("DumpHierarchy failed: " + ex.Message);
        }
    }

    private static void InspectFBX(string assetPath, string outputPath)
    {
        try
        {
            GameObject fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (fbx == null)
            {
                System.IO.File.WriteAllText(outputPath, $"{assetPath} not found.");
                return;
            }
            using (var writer = new System.IO.StreamWriter(outputPath, false))
            {
                writer.WriteLine($"FBX Structure: {assetPath}");
                DumpFBXTransform(fbx.transform, "", writer);
            }
        }
        catch (System.Exception ex)
        {
            System.IO.File.WriteAllText(outputPath, $"Error inspecting {assetPath}: {ex.Message}");
        }
    }

    private static void DumpFBXTransform(Transform t, string indent, System.IO.StreamWriter writer)
    {
        writer.WriteLine($"{indent}- {t.name}");
        for (int i = 0; i < t.childCount; i++)
        {
            DumpFBXTransform(t.GetChild(i), indent + "  ", writer);
        }
    }

    private static void DumpTransform(Transform t, string indent, System.IO.StreamWriter writer)
    {
        writer.WriteLine($"{indent}- {t.name} (Position: {t.localPosition}, Rotation: {t.localEulerAngles}, Scale: {t.localScale})");
        for (int i = 0; i < t.childCount; i++)
        {
            DumpTransform(t.GetChild(i), indent + "  ", writer);
        }
    }

    [MenuItem("Tools/Reset and Run Setup %#r")]
    public static void ResetAndRun()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode Active", "Please exit Play Mode in Unity before running the scene setup!", "OK");
            return;
        }

        EditorPrefs.SetBool("RoomGame_SceneSetup_Done_V32", false);
        Setup();
        EditorPrefs.SetBool("RoomGame_SceneSetup_Done_V32", true);
    }


    public static void Setup()
    {
        if (EditorApplication.isPlaying) return;

        var scene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path) || !scene.path.EndsWith("Main.unity"))
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        }
        Debug.Log("SceneSetup: Modifying scene " + scene.name);

        CleanupMissingAndOldObjects();

        // Materials
        Material torsoMat = GetOrCreateMaterial("Assets/Materials/TorsoMat.mat", new Color(0.2f, 0.4f, 0.8f));
        Material limbMat = GetOrCreateMaterial("Assets/Materials/LimbMat.mat", new Color(0.1f, 0.1f, 0.1f));
        Material headMat = GetOrCreateMaterial("Assets/Materials/HeadMat.mat", new Color(0.95f, 0.8f, 0.7f));

        // Physics Material (realistic friction)
        PhysicsMaterial frictionMat = GetOrCreatePhysicMaterial("Assets/Materials/RealisticFriction.physicMaterial", 0.5f, 0.6f);

        // Spawn smaller player character
        GameObject player = CreatePlayerCharacter(torsoMat, limbMat, headMat);

        // Set up screens, computers, desks with physics and realistic friction material
        SetupFurniturePhysics(frictionMat);

        // Setup Interactable Computer OS screens
        SetupComputerScreens();

        // Setup First Person Camera (parented inside head, recreates if missing)
        SetupFirstPersonCamera(player, torsoMat);

        // Fix Poster/Chalkboard scale/aspect ratio
        FixPosterScale();

        // Setup Openable Cabinets
        SetupOpenableCabinets();

        // Setup Room Door (physics and controller limits)
        SetupRoomDoor(frictionMat);

        // Fix Wall Materials (chalkboard wall pink, end wall blue)
        FixWallMaterials();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("SceneSetup: Scene successfully updated with Shadows-Only FPS, First Person Arm, Physics Grabbing, and Pink Chalkboard Wall!");
    }

    private static void CleanupMissingAndOldObjects()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            mainCam = GameObject.FindAnyObjectByType<Camera>();
        }
        if (mainCam != null)
        {
            mainCam.transform.SetParent(null);
        }

        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go == null) continue;

            if (go.name == "Player" || go.GetComponent<PlayerMovement>() != null)
            {
                GameObject.DestroyImmediate(go);
            }
            else if (go.name.Contains("Biped_Humanoid") || PrefabUtility.IsPrefabAssetMissing(go))
            {
                GameObject.DestroyImmediate(go);
            }
        }
    }

    private static Material GetOrCreateMaterial(string assetPath, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Glossiness", 0.1f);
            AssetDatabase.CreateAsset(mat, assetPath);
        }
        return mat;
    }

    private static PhysicsMaterial GetOrCreatePhysicMaterial(string assetPath, float dynamicFriction, float staticFriction)
    {
        PhysicsMaterial mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(assetPath);
        if (mat == null)
        {
            mat = new PhysicsMaterial("RealisticFriction");
            mat.dynamicFriction = dynamicFriction;
            mat.staticFriction = staticFriction;
            mat.frictionCombine = PhysicsMaterialCombine.Average;
            AssetDatabase.CreateAsset(mat, assetPath);
        }
        return mat;
    }

    private static GameObject CreatePlayerCharacter(Material torsoMat, Material limbMat, Material headMat)
    {
        // 1. Create player root
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1.2f, -3f); // Spawn above floor, away from tables
        player.transform.rotation = Quaternion.identity;

        // 2. Add Rigidbody (mass 70kg, frozen all rotations)
        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.mass = 70f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.5f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // 3. Add CapsuleCollider (teenager/adult sized: height 1.65m)
        CapsuleCollider controller = player.AddComponent<CapsuleCollider>();
        controller.height = 1.65f;
        controller.radius = 0.25f;
        controller.center = new Vector3(0f, 0.825f, 0f);

        // 4. Add Player Movement Script
        PlayerMovement movement = player.AddComponent<PlayerMovement>();
        movement.moveSpeed = 3.5f; // slightly faster walk for a taller person
        movement.jumpHeight = 1.0f; // slightly higher jump for a taller person
        movement.gravity = 15f;
        movement.mouseSensitivity = 2f;

        // 5. Visuals container
        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(player.transform);
        visuals.transform.localPosition = Vector3.zero;
        visuals.transform.localRotation = Quaternion.identity;

        // Torso (shadows only)
        GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        torso.name = "Torso";
        torso.transform.SetParent(visuals.transform);
        torso.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        torso.transform.localScale = new Vector3(0.4f, 0.8f, 0.25f);
        GameObject.DestroyImmediate(torso.GetComponent<BoxCollider>());
        var torsoRenderer = torso.GetComponent<MeshRenderer>();
        torsoRenderer.sharedMaterial = torsoMat;
        torsoRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        // Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(visuals.transform);
        head.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        GameObject.DestroyImmediate(head.GetComponent<BoxCollider>());
        var headRenderer = head.GetComponent<MeshRenderer>();
        headRenderer.sharedMaterial = headMat;
        headRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        // Left Leg
        GameObject leftLegPivot = new GameObject("LeftLegPivot");
        leftLegPivot.transform.SetParent(visuals.transform);
        leftLegPivot.transform.localPosition = new Vector3(-0.12f, 0.6f, 0f);
        leftLegPivot.transform.localRotation = Quaternion.identity;

        GameObject leftLegVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftLegVisual.name = "LeftLegVisual";
        leftLegVisual.transform.SetParent(leftLegPivot.transform);
        leftLegVisual.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        leftLegVisual.transform.localScale = new Vector3(0.15f, 0.6f, 0.15f);
        GameObject.DestroyImmediate(leftLegVisual.GetComponent<BoxCollider>());
        var leftLegRenderer = leftLegVisual.GetComponent<MeshRenderer>();
        leftLegRenderer.sharedMaterial = limbMat;
        leftLegRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        // Right Leg
        GameObject rightLegPivot = new GameObject("RightLegPivot");
        rightLegPivot.transform.SetParent(visuals.transform);
        rightLegPivot.transform.localPosition = new Vector3(0.12f, 0.6f, 0f);
        rightLegPivot.transform.localRotation = Quaternion.identity;

        GameObject rightLegVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightLegVisual.name = "RightLegVisual";
        rightLegVisual.transform.SetParent(rightLegPivot.transform);
        rightLegVisual.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        rightLegVisual.transform.localScale = new Vector3(0.15f, 0.6f, 0.15f);
        GameObject.DestroyImmediate(rightLegVisual.GetComponent<BoxCollider>());
        var rightLegRenderer = rightLegVisual.GetComponent<MeshRenderer>();
        rightLegRenderer.sharedMaterial = limbMat;
        rightLegRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        // Left Arm (Shadow only)
        GameObject leftArmPivot = new GameObject("LeftArmPivot");
        leftArmPivot.transform.SetParent(visuals.transform);
        leftArmPivot.transform.localPosition = new Vector3(-0.28f, 1.25f, 0f);
        leftArmPivot.transform.localRotation = Quaternion.identity;

        GameObject leftArmVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftArmVisual.name = "LeftArmVisual";
        leftArmVisual.transform.SetParent(leftArmPivot.transform);
        leftArmVisual.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        leftArmVisual.transform.localScale = new Vector3(0.15f, 0.6f, 0.15f);
        GameObject.DestroyImmediate(leftArmVisual.GetComponent<BoxCollider>());
        var leftArmRenderer = leftArmVisual.GetComponent<MeshRenderer>();
        leftArmRenderer.sharedMaterial = limbMat;
        leftArmRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        // Right Arm (Shadow only)
        GameObject rightArmPivot = new GameObject("RightArmPivot");
        rightArmPivot.transform.SetParent(visuals.transform);
        rightArmPivot.transform.localPosition = new Vector3(0.28f, 1.25f, 0f);
        rightArmPivot.transform.localRotation = Quaternion.identity;

        GameObject rightArmVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightArmVisual.name = "RightArmVisual";
        rightArmVisual.transform.SetParent(rightArmPivot.transform);
        rightArmVisual.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        rightArmVisual.transform.localScale = new Vector3(0.15f, 0.6f, 0.15f);
        GameObject.DestroyImmediate(rightArmVisual.GetComponent<BoxCollider>());
        var rightArmRenderer = rightArmVisual.GetComponent<MeshRenderer>();
        rightArmRenderer.sharedMaterial = limbMat;
        rightArmRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        // Link limb references for shadow animations
        movement.leftLeg = leftLegPivot.transform;
        movement.rightLeg = rightLegPivot.transform;
        movement.leftArm = leftArmPivot.transform;
        movement.rightArm = rightArmPivot.transform;

        return player;
    }

    private static void SetupFurniturePhysics(PhysicsMaterial frictionMat)
    {
        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        HashSet<GameObject> processedRoots = new HashSet<GameObject>();

        foreach (var go in allObjects)
        {
            if (go == null) continue;

            string name = go.name.ToLower();
            if (name.Contains("desk") || name.Contains("computer") || name.Contains("computertest") || name.Contains("screen") || name.Contains("stool"))
            {
                if (go.transform.parent != null && go.transform.parent.name == "Screen")
                {
                    continue;
                }

                GameObject root = GetOutermostPhysicsRoot(go);
                if (processedRoots.Contains(root))
                    continue;

                processedRoots.Add(root);

                bool isDesk = root.name.ToLower().Contains("desk");

                // Configure Rigidbody
                Rigidbody rb = root.GetComponent<Rigidbody>();
                if (rb == null) rb = root.AddComponent<Rigidbody>();

                if (isDesk)
                {
                    // Desks are bolted to the floor — kinematic so they never fall or slide
                    rb.isKinematic = true;
                    rb.mass = 80f;
                }
                else if (root.name.ToLower().Contains("stool"))
                {
                    rb.isKinematic = false;
                    rb.mass = 5f;   // Realistic stool weight (5kg)
                    rb.linearDamping = 1.0f;
                    rb.angularDamping = 1.0f;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                }
                else if (root.name.ToLower().Contains("computer"))
                {
                    rb.isKinematic = false;
                    rb.mass = 3f;   // Realistic monitor weight (3kg)
                    rb.linearDamping = 2.0f;
                    rb.angularDamping = 2.0f;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                }
                else
                {
                    rb.isKinematic = false;
                    rb.mass = 4f;   // Screen weight (4kg)
                    rb.linearDamping = 2.0f;
                    rb.angularDamping = 2.0f;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                }

                // Mesh colliders on all children
                var meshFilters = root.GetComponentsInChildren<MeshFilter>();
                foreach (var mf in meshFilters)
                {
                    if (mf.sharedMesh == null) continue;
                    var existingColliders = mf.GetComponents<Collider>();
                    foreach (var oldCol in existingColliders) GameObject.DestroyImmediate(oldCol);
                    MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                    mc.sharedMaterial = frictionMat;
                }
            }
        }
    }

    private static GameObject GetOutermostPhysicsRoot(GameObject go)
    {
        GameObject outermost = go;
        Transform current = go.transform;
        string goName = go.name.ToLower();

        bool isStool = goName.Contains("stool");
        bool isDesk = goName.Contains("desk");
        bool isPC = goName.Contains("computer") || goName.Contains("computertest") || goName.Contains("screen");

        while (current.parent != null)
        {
            string parentName = current.parent.name.ToLower();
            bool match = false;
            if (isStool && parentName.Contains("stool")) match = true;
            else if (isDesk && parentName.Contains("desk")) match = true;
            else if (isPC && (parentName.Contains("computer") || parentName.Contains("computertest") || parentName.Contains("screen"))) match = true;

            if (match)
            {
                outermost = current.parent.gameObject;
                current = current.parent;
            }
            else
            {
                break;
            }
        }
        return outermost;
    }

    private static void SetupFirstPersonCamera(GameObject player, Material sleeveMat)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            mainCam = GameObject.FindAnyObjectByType<Camera>();
        }

        // If the Main Camera was deleted during previous player cleanup, recreate it!
        if (mainCam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            mainCam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            Debug.Log("SceneSetup: Recreated missing Main Camera in scene.");
        }

        // Remove 3rd person follow camera component if exists
        CameraFollow follow = mainCam.GetComponent<CameraFollow>();
        if (follow != null)
        {
            GameObject.DestroyImmediate(follow);
        }

        // Clean up any existing First Person Arms on the Camera to prevent duplication
        for (int i = mainCam.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = mainCam.transform.GetChild(i);
            if (child.name.Contains("FPArmPivot") || child.name.Contains("FPLeftArm") || child.name.Contains("FPRightArm"))
            {
                GameObject.DestroyImmediate(child.gameObject);
            }
        }

        mainCam.transform.SetParent(player.transform);
        mainCam.transform.localPosition = new Vector3(0f, 1.5f, 0.1f); // Inside the head center looking out
        mainCam.transform.localRotation = Quaternion.identity;

        // Create VISIBLE First Person Right Arm as a child of the Camera!
        GameObject fpRightArmPivot = new GameObject("FPRightArmPivot");
        fpRightArmPivot.transform.SetParent(mainCam.transform);
        fpRightArmPivot.transform.localPosition = new Vector3(0.3f, -0.35f, 0.5f); // Symmetrical right side
        fpRightArmPivot.transform.localRotation = Quaternion.identity;

        GameObject fpRightArmVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fpRightArmVisual.name = "FPRightArmVisual";
        fpRightArmVisual.transform.SetParent(fpRightArmPivot.transform);
        fpRightArmVisual.transform.localPosition = new Vector3(0f, 0f, 0.25f); // Forward from pivot
        fpRightArmVisual.transform.localScale = new Vector3(0.12f, 0.12f, 0.5f);
        GameObject.DestroyImmediate(fpRightArmVisual.GetComponent<BoxCollider>());
        
        var fpRightArmRenderer = fpRightArmVisual.GetComponent<MeshRenderer>();
        fpRightArmRenderer.sharedMaterial = sleeveMat;
        fpRightArmRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        // Create VISIBLE First Person Left Arm as a child of the Camera!
        GameObject fpLeftArmPivot = new GameObject("FPLeftArmPivot");
        fpLeftArmPivot.transform.SetParent(mainCam.transform);
        fpLeftArmPivot.transform.localPosition = new Vector3(-0.3f, -0.35f, 0.5f); // Symmetrical left side
        fpLeftArmPivot.transform.localRotation = Quaternion.identity;

        GameObject fpLeftArmVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fpLeftArmVisual.name = "FPLeftArmVisual";
        fpLeftArmVisual.transform.SetParent(fpLeftArmPivot.transform);
        fpLeftArmVisual.transform.localPosition = new Vector3(0f, 0f, 0.25f);
        fpLeftArmVisual.transform.localScale = new Vector3(0.12f, 0.12f, 0.5f);
        GameObject.DestroyImmediate(fpLeftArmVisual.GetComponent<BoxCollider>());

        var fpLeftArmRenderer = fpLeftArmVisual.GetComponent<MeshRenderer>();
        fpLeftArmRenderer.sharedMaterial = sleeveMat;
        fpLeftArmRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        movement.playerCamera = mainCam.transform;
        movement.firstPersonLeftArm = fpLeftArmPivot.transform;
        movement.firstPersonRightArm = fpRightArmPivot.transform;
    }

    private static void FixPosterScale()
    {
        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go == null) continue;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.name.ToLower().Contains("poster"))
            {
                Vector3 localScale = go.transform.localScale;

                // Swap X and Z scales if X is smaller than Z (due to 90 degrees rotation)
                if (localScale.x < localScale.z)
                {
                    float temp = localScale.x;
                    localScale.x = localScale.z;
                    localScale.z = temp;
                }

                // Adjust height (Y scale) based on width (X scale) to fit the native aspect ratio
                float aspect = 4.402424f;
                if (renderer.sharedMaterial.mainTexture != null)
                {
                    Texture texture = renderer.sharedMaterial.mainTexture;
                    aspect = (float)texture.width / texture.height;
                }
                localScale.y = localScale.x / aspect;

                go.transform.localScale = localScale;

                // Adjust position to sit flush on the wall (wall is at X = -1.9353 with X scale = 0.22868527)
                // Wall front face = -1.9353 + 0.22868527 / 2 = -1.82095736
                float wallFrontX = -1.82095736f;
                Vector3 localPos = go.transform.localPosition;
                localPos.x = wallFrontX + (localScale.z / 2f) + 0.005f;
                go.transform.localPosition = localPos;

                Debug.Log($"SceneSetup: Fixed poster '{go.name}' scale to {localScale} matching texture aspect ratio {aspect} and repositioned to {localPos}");
            }
        }
    }

    private static GameObject FindCabinet(string name)
    {
        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go.name == name && go.transform.parent == null)
            {
                return go;
            }
        }
        return GameObject.Find(name); // fallback
    }

    private static void SetupOpenableCabinets()
    {
        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<GameObject> cabinets = new List<GameObject>();
        foreach (var go in allObjects)
        {
            if (go != null && go.transform.parent == null)
            {
                string name = go.name;
                if (name.StartsWith("Cabinet that opens") || 
                    name == "Cube (19)" || 
                    name == "Cube (20)" || 
                    name == "Cube (22)" || 
                    name == "Cube (28)" || 
                    name == "Cube 28")
                {
                    cabinets.Add(go);
                }
            }
        }

        Material doorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IMG_1307 (1).mat");
        Material handleMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IMG_1307 (2).mat");
        Material frameMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/New Material 1.mat");

        foreach (var cab in cabinets)
        {
            SetupCabinet(cab, doorMat, handleMat, frameMat);
        }
    }

    private static void SetupCabinet(GameObject cab, Material doorMat, Material handleMat, Material frameMat)
    {
        // 1. Capture original material of the cabinet itself (if it has one and is not frameMat/placeholder)
        var renderer = cab.GetComponent<Renderer>();
        Material originalMat = renderer != null ? renderer.sharedMaterial : null;
        if (originalMat == frameMat || (originalMat != null && originalMat.name == "New Material"))
        {
            originalMat = null; // Already overwritten or placeholder
        }

        // 2. Capture existing door materials from scene BEFORE deleting them
        Material[] existingLeftMats = new Material[2];  // 0: Top, 1: Bottom
        Material[] existingRightMats = new Material[2];
        Material[] existingSingleMats = new Material[2];

        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go != null && go.name.StartsWith(cab.name + "_"))
            {
                Transform fv = go.transform.Find("FrontVisual");
                if (fv != null)
                {
                    Renderer r = fv.GetComponent<Renderer>();
                    if (r != null && r.sharedMaterial != null && r.sharedMaterial != frameMat && r.sharedMaterial.name != "New Material")
                    {
                        int lvlIndex = go.name.Contains("Bottom") ? 1 : 0;
                        if (go.name.Contains("LeftDoor")) existingLeftMats[lvlIndex] = r.sharedMaterial;
                        else if (go.name.Contains("RightDoor")) existingRightMats[lvlIndex] = r.sharedMaterial;
                        else if (go.name.Contains("Door")) existingSingleMats[lvlIndex] = r.sharedMaterial;
                    }
                }
            }
        }

        // 3. Prepare the cabinet body
        // Ensure it has a Rigidbody set to Kinematic
        Rigidbody cabRb = cab.GetComponent<Rigidbody>();
        if (cabRb == null) cabRb = cab.AddComponent<Rigidbody>();
        cabRb.isKinematic = true;

        // Change material to the plain frame material
        if (renderer != null && frameMat != null)
        {
            renderer.sharedMaterial = frameMat;
        }

        // Clean up any previously created doors in the scene associated with this cabinet
        foreach (var go in allObjects)
        {
            if (go != null && go.name.StartsWith(cab.name + "_") && (go.name.Contains("Door") || go.name.Contains("Handle")))
            {
                GameObject.DestroyImmediate(go);
            }
        }

        // Clean up any direct/indirect child objects of the cabinet itself that contain "door" or "handle" in their name
        // (to prevent duplicate static doors from overlapping with our new physical doors)
        for (int i = cab.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = cab.transform.GetChild(i);
            string childName = child.name.ToLower();
            if (childName.Contains("door") || childName.Contains("handle"))
            {
                GameObject.DestroyImmediate(child.gameObject);
            }
        }

        Vector3 cabScale = cab.transform.localScale;

        // Detect front face direction based on nearest boundary wall
        Vector3 pos = cab.transform.position;
        float distToRight = 4.23f - pos.x;
        float distToLeft = pos.x - (-3.21f);
        float distToBack = 4.23f - pos.z;
        float distToFront = pos.z - (-15.47f);

        float minDist = Mathf.Min(Mathf.Min(distToRight, distToLeft), Mathf.Min(distToBack, distToFront));
        Vector3 worldFrontDir = Vector3.left;
        if (minDist == distToRight) worldFrontDir = Vector3.left;
        else if (minDist == distToLeft) worldFrontDir = Vector3.right;
        else if (minDist == distToBack) worldFrontDir = Vector3.back;
        else if (minDist == distToFront) worldFrontDir = Vector3.forward;

        Vector3 localFrontDir = cab.transform.InverseTransformDirection(worldFrontDir);
        
        // Find which local axis aligns with localFrontDir
        float dotX = Vector3.Dot(localFrontDir, Vector3.right);
        float dotZ = Vector3.Dot(localFrontDir, Vector3.forward);

        bool isFrontX = Mathf.Abs(dotX) > Mathf.Abs(dotZ);
        float frontSign = isFrontX ? Mathf.Sign(dotX) : Mathf.Sign(dotZ);

        Vector3 depthDir = isFrontX ? new Vector3(frontSign, 0f, 0f) : new Vector3(0f, 0f, frontSign);
        Vector3 widthDir = isFrontX ? new Vector3(0f, 0f, -frontSign) : new Vector3(-frontSign, 0f, 0f);

        // Determine if it should be single or double door based on width dimension
        float worldWidth = isFrontX ? cabScale.z : cabScale.x;
        bool isSingleDoor = worldWidth <= 0.8f;

        // Check if the cabinet is a 2-leveled cabinet (e.g. Cube (22), Cube (28))
        bool isTwoLeveled = cab.name.Contains("Cube (22)") || cab.name.Contains("Cube (28)") || cab.name.Contains("Cube 28");
        int numLevels = isTwoLeveled ? 2 : 1;

        // Fallback for original texture material if overwritten
        if (isTwoLeveled && originalMat == null)
        {
            originalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/IMG_1303.mat");
        }

        float T = 0.04f; // Door thickness in cabinet local space

        for (int lvl = 0; lvl < numLevels; lvl++)
        {
            float lvlHeightScale = isTwoLeveled ? 0.5f : 1f;
            float lvlPosY = isTwoLeveled ? (lvl == 0 ? 0.25f : -0.25f) : 0f;
            float handleLocalY = 0f;

            if (isTwoLeveled)
            {
                handleLocalY = (lvl == 0) ? -0.35f : 0.35f;
            }
            else
            {
                // Find other cabinets in the scene to check if stacked standalone
                var allCabs = new List<GameObject>();
                string[] cabNames = { "Cabinet that opens", "Cube (19)", "Cube (20)", "Cube (22)", "Cube (28)", "Cube 28" };
                foreach (string name in cabNames)
                {
                    GameObject c = GameObject.Find(name);
                    if (c != null && c != cab) allCabs.Add(c);
                }

                foreach (var other in allCabs)
                {
                    float horizontalDist = Vector2.Distance(
                        new Vector2(pos.x, pos.z), 
                        new Vector2(other.transform.position.x, other.transform.position.z)
                    );

                    if (horizontalDist < 0.2f) // Stacked cabinet found!
                    {
                        if (pos.y > other.transform.position.y)
                        {
                            handleLocalY = -0.35f; // top level
                        }
                        else
                        {
                            handleLocalY = 0.35f; // bottom level
                        }
                        break;
                    }
                }
            }

            // Create textures for this level (if 2-leveled, split the texture vertically)
            Material lvlDoorMat = originalMat != null ? originalMat : doorMat;
            if (isTwoLeveled && originalMat != null)
            {
                lvlDoorMat = new Material(originalMat);
                lvlDoorMat.name = cab.name + "_Lvl_" + lvl + "_Mat";
                lvlDoorMat.mainTextureScale = new Vector2(1f, 0.5f);
                lvlDoorMat.mainTextureOffset = new Vector2(0f, lvl == 0 ? 0.5f : 0f);
            }

            if (isSingleDoor)
            {
                // Single door locker
                string doorName = cab.name + "_" + (isTwoLeveled ? (lvl == 0 ? "Door_Top" : "Door_Bottom") : "Door");
                GameObject doorContainer = new GameObject(doorName);
                
                // Position container at the hinge pivot point in world space
                Vector3 pivotPos = depthDir * 0.5f + widthDir * 0.5f + Vector3.up * lvlPosY;
                doorContainer.transform.position = cab.transform.TransformPoint(pivotPos);
                doorContainer.transform.rotation = cab.transform.rotation;
                doorContainer.transform.localScale = Vector3.one;
                float W = 0.998f; // Single door spans full width with a tiny clearance gap
                float worldW = W * (isFrontX ? cabScale.z : cabScale.x);
                float worldH = lvlHeightScale * cabScale.y;
                float worldT = T * (isFrontX ? cabScale.x : cabScale.z);

                Vector3 widthOffsetDir = -widthDir;
                Vector3 doorCenterOffset = widthOffsetDir * (worldW * 0.5f) + depthDir * (worldT * 0.5f);

                // Add Physical BoxCollider and Rigidbody to the container
                BoxCollider boxCol = doorContainer.AddComponent<BoxCollider>();
                boxCol.center = doorCenterOffset;
                if (isFrontX)
                    boxCol.size = new Vector3(worldT, worldH, worldW);
                else
                    boxCol.size = new Vector3(worldW, worldH, worldT);

                Rigidbody doorRb = doorContainer.AddComponent<Rigidbody>();
                doorRb.mass = 3f;
                doorRb.linearDamping = 0.5f;
                doorRb.angularDamping = 2.5f; // Higher rotational friction so it stays open where the player leaves it
                doorRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // Visual Child 1: Front Quad (textured with doorMat)
                GameObject front = GameObject.CreatePrimitive(PrimitiveType.Quad);
                front.name = "FrontVisual";
                front.transform.SetParent(doorContainer.transform);
                front.transform.localPosition = doorCenterOffset + depthDir * (worldT * 0.501f);
                front.transform.localRotation = Quaternion.LookRotation(-localFrontDir, Vector3.up);
                front.transform.localScale = new Vector3(worldW, worldH, 1f);
                GameObject.DestroyImmediate(front.GetComponent<Collider>());
                
                Material appliedMat = existingSingleMats[lvl] != null ? existingSingleMats[lvl] : lvlDoorMat;
                if (appliedMat != null) front.GetComponent<Renderer>().sharedMaterial = appliedMat;

                // Visual Child 2: Door Body Cube (solid grey frameMat)
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "DoorBodyVisual";
                body.transform.SetParent(doorContainer.transform);
                body.transform.localPosition = doorCenterOffset;
                body.transform.localRotation = Quaternion.identity;
                if (isFrontX)
                    body.transform.localScale = new Vector3(worldT, worldH, worldW);
                else
                    body.transform.localScale = new Vector3(worldW, worldH, worldT);
                GameObject.DestroyImmediate(body.GetComponent<Collider>());
                if (frameMat != null) body.GetComponent<Renderer>().sharedMaterial = frameMat;

                // Add HingeJoint to the container
                HingeJoint hinge = doorContainer.AddComponent<HingeJoint>();
                hinge.connectedBody = cabRb;
                hinge.anchor = Vector3.zero; // Pivot is exactly at container origin
                hinge.axis = new Vector3(0f, 1f, 0f);
                hinge.useLimits = true;
                
                JointLimits limits = new JointLimits();
                limits.min = 0f;
                limits.max = 130f;
                limits.bounciness = 0.2f;
                hinge.limits = limits;

                // Add CabinetDoor component
                CabinetDoor cabinetDoor = doorContainer.AddComponent<CabinetDoor>();
                cabinetDoor.cabinet = cab;

                // Add Handle
                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handle.name = "Handle";
                handle.transform.SetParent(doorContainer.transform);
                
                if (isFrontX)
                {
                    handle.transform.localScale = new Vector3(0.02f, 0.15f, 0.04f);
                }
                else
                {
                    handle.transform.localScale = new Vector3(0.04f, 0.15f, 0.02f);
                }
                handle.transform.localPosition = widthOffsetDir * (worldW - 0.06f) + depthDir * (worldT + 0.02f) + Vector3.up * (handleLocalY * cabScale.y);
                handle.transform.localRotation = Quaternion.identity;

                if (handleMat != null)
                {
                    handle.GetComponent<Renderer>().sharedMaterial = handleMat;
                }
            }
            else
            {
                // Double door cabinet
                float W = 0.499f; // Each door is slightly less than half width to leave a tiny clearance gap
                float worldW = W * (isFrontX ? cabScale.z : cabScale.x);
                float worldH = lvlHeightScale * cabScale.y;
                float worldT = T * (isFrontX ? cabScale.x : cabScale.z);

                // ================= LEFT DOOR =================
                string leftDoorName = cab.name + "_" + (isTwoLeveled ? (lvl == 0 ? "LeftDoor_Top" : "LeftDoor_Bottom") : "LeftDoor");
                GameObject leftDoorContainer = new GameObject(leftDoorName);
                
                Vector3 leftPivotPos = depthDir * 0.5f + widthDir * 0.5f + Vector3.up * lvlPosY;
                leftDoorContainer.transform.position = cab.transform.TransformPoint(leftPivotPos);
                leftDoorContainer.transform.rotation = cab.transform.rotation;
                leftDoorContainer.transform.localScale = Vector3.one;
                
                Vector3 leftWidthOffsetDir = -widthDir;
                Vector3 leftDoorCenterOffset = leftWidthOffsetDir * (worldW * 0.5f) + depthDir * (worldT * 0.5f);

                BoxCollider leftCol = leftDoorContainer.AddComponent<BoxCollider>();
                leftCol.center = leftDoorCenterOffset;
                if (isFrontX)
                    leftCol.size = new Vector3(worldT, worldH, worldW);
                else
                    leftCol.size = new Vector3(worldW, worldH, worldT);

                Rigidbody leftRb = leftDoorContainer.AddComponent<Rigidbody>();
                leftRb.mass = 2f;
                leftRb.linearDamping = 0.5f;
                leftRb.angularDamping = 2.5f; // Higher rotational friction so it stays open where the player leaves it
                leftRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                GameObject leftFront = GameObject.CreatePrimitive(PrimitiveType.Quad);
                leftFront.name = "FrontVisual";
                leftFront.transform.SetParent(leftDoorContainer.transform);
                leftFront.transform.localPosition = leftDoorCenterOffset + depthDir * (worldT * 0.501f);
                leftFront.transform.localRotation = Quaternion.LookRotation(-localFrontDir, Vector3.up);
                leftFront.transform.localScale = new Vector3(worldW, worldH, 1f);
                GameObject.DestroyImmediate(leftFront.GetComponent<Collider>());
                
                Material appliedLeftMat = existingLeftMats[lvl] != null ? existingLeftMats[lvl] : lvlDoorMat;
                if (appliedLeftMat != null) leftFront.GetComponent<Renderer>().sharedMaterial = appliedLeftMat;

                GameObject leftBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftBody.name = "DoorBodyVisual";
                leftBody.transform.SetParent(leftDoorContainer.transform);
                leftBody.transform.localPosition = leftDoorCenterOffset;
                leftBody.transform.localRotation = Quaternion.identity;
                if (isFrontX)
                    leftBody.transform.localScale = new Vector3(worldT, worldH, worldW);
                else
                    leftBody.transform.localScale = new Vector3(worldW, worldH, worldT);
                GameObject.DestroyImmediate(leftBody.GetComponent<Collider>());
                if (frameMat != null) leftBody.GetComponent<Renderer>().sharedMaterial = frameMat;

                HingeJoint leftHinge = leftDoorContainer.AddComponent<HingeJoint>();
                leftHinge.connectedBody = cabRb;
                leftHinge.anchor = Vector3.zero;
                leftHinge.axis = new Vector3(0f, 1f, 0f);
                leftHinge.useLimits = true;

                JointLimits leftLimits = new JointLimits();
                leftLimits.min = -130f;
                leftLimits.max = 0f;
                leftLimits.bounciness = 0.2f;
                leftHinge.limits = leftLimits;

                // Add CabinetDoor component
                CabinetDoor leftCabinetDoor = leftDoorContainer.AddComponent<CabinetDoor>();
                leftCabinetDoor.cabinet = cab;

                GameObject leftHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftHandle.name = "LeftHandle";
                leftHandle.transform.SetParent(leftDoorContainer.transform);
                
                if (isFrontX)
                {
                    leftHandle.transform.localScale = new Vector3(0.02f, 0.15f, 0.04f);
                }
                else
                {
                    leftHandle.transform.localScale = new Vector3(0.04f, 0.15f, 0.02f);
                }
                leftHandle.transform.localPosition = leftWidthOffsetDir * (worldW - 0.06f) + depthDir * (worldT + 0.02f) + Vector3.up * (handleLocalY * cabScale.y);
                leftHandle.transform.localRotation = Quaternion.identity;

                if (handleMat != null)
                {
                    leftHandle.GetComponent<Renderer>().sharedMaterial = handleMat;
                }

                // ================= RIGHT DOOR =================
                string rightDoorName = cab.name + "_" + (isTwoLeveled ? (lvl == 0 ? "RightDoor_Top" : "RightDoor_Bottom") : "RightDoor");
                GameObject rightDoorContainer = new GameObject(rightDoorName);
                
                Vector3 rightPivotPos = depthDir * 0.5f - widthDir * 0.5f + Vector3.up * lvlPosY;
                rightDoorContainer.transform.position = cab.transform.TransformPoint(rightPivotPos);
                rightDoorContainer.transform.rotation = cab.transform.rotation;
                rightDoorContainer.transform.localScale = Vector3.one;
                
                Vector3 rightWidthOffsetDir = widthDir;
                Vector3 rightDoorCenterOffset = rightWidthOffsetDir * (worldW * 0.5f) + depthDir * (worldT * 0.5f);

                BoxCollider rightCol = rightDoorContainer.AddComponent<BoxCollider>();
                rightCol.center = rightDoorCenterOffset;
                if (isFrontX)
                    rightCol.size = new Vector3(worldT, worldH, worldW);
                else
                    rightCol.size = new Vector3(worldW, worldH, worldT);

                Rigidbody rightRb = rightDoorContainer.AddComponent<Rigidbody>();
                rightRb.mass = 2f;
                rightRb.linearDamping = 0.5f;
                rightRb.angularDamping = 2.5f; // Higher rotational friction so it stays open where the player leaves it
                rightRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                GameObject rightFront = GameObject.CreatePrimitive(PrimitiveType.Quad);
                rightFront.name = "FrontVisual";
                rightFront.transform.SetParent(rightDoorContainer.transform);
                rightFront.transform.localPosition = rightDoorCenterOffset + depthDir * (worldT * 0.501f);
                rightFront.transform.localRotation = Quaternion.LookRotation(-localFrontDir, Vector3.up);
                rightFront.transform.localScale = new Vector3(worldW, worldH, 1f);
                GameObject.DestroyImmediate(rightFront.GetComponent<Collider>());
                
                Material appliedRightMat = existingRightMats[lvl] != null ? existingRightMats[lvl] : lvlDoorMat;
                if (appliedRightMat != null) rightFront.GetComponent<Renderer>().sharedMaterial = appliedRightMat;

                GameObject rightBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightBody.name = "DoorBodyVisual";
                rightBody.transform.SetParent(rightDoorContainer.transform);
                rightBody.transform.localPosition = rightDoorCenterOffset;
                rightBody.transform.localRotation = Quaternion.identity;
                if (isFrontX)
                    rightBody.transform.localScale = new Vector3(worldT, worldH, worldW);
                else
                    rightBody.transform.localScale = new Vector3(worldW, worldH, worldT);
                GameObject.DestroyImmediate(rightBody.GetComponent<Collider>());
                if (frameMat != null) rightBody.GetComponent<Renderer>().sharedMaterial = frameMat;

                HingeJoint rightHinge = rightDoorContainer.AddComponent<HingeJoint>();
                rightHinge.connectedBody = cabRb;
                rightHinge.anchor = Vector3.zero;
                rightHinge.axis = new Vector3(0f, 1f, 0f);
                rightHinge.useLimits = true;

                JointLimits rightLimits = new JointLimits();
                rightLimits.min = 0f;
                rightLimits.max = 130f;
                rightLimits.bounciness = 0.2f;
                rightHinge.limits = rightLimits;

                // Add CabinetDoor component
                CabinetDoor rightCabinetDoor = rightDoorContainer.AddComponent<CabinetDoor>();
                rightCabinetDoor.cabinet = cab;

                GameObject rightHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightHandle.name = "RightHandle";
                rightHandle.transform.SetParent(rightDoorContainer.transform);
                
                if (isFrontX)
                {
                    rightHandle.transform.localScale = new Vector3(0.02f, 0.15f, 0.04f);
                }
                else
                {
                    rightHandle.transform.localScale = new Vector3(0.04f, 0.15f, 0.02f);
                }
                rightHandle.transform.localPosition = rightWidthOffsetDir * (worldW - 0.06f) + depthDir * (worldT + 0.02f) + Vector3.up * (handleLocalY * cabScale.y);
                rightHandle.transform.localRotation = Quaternion.identity;

                if (handleMat != null)
                {
                    rightHandle.GetComponent<Renderer>().sharedMaterial = handleMat;
                }
            }
        }
    }

    private static void SetupComputerScreens()
    {
        // 1. Ensure EventSystem is present
        GameObject eventSystemGo = GameObject.Find("EventSystem");
        if (eventSystemGo == null)
        {
            eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("SceneSetup: Created EventSystem for UI interactions.");
        }

        // 2. Find all computer screen assemblies
        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;

        foreach (var go in allObjects)
        {
            if (go == null) continue;

            string goName = go.name;          // preserve original casing
            string name = goName.ToLower();

            // Match ONLY numbered Screen objects ("Screen (2)", "Screen (3)") and computertest.
            // The plain object named "Screen" or "screen" is a TV/prop with no OS.
            bool isMonitorRoot =
                goName.StartsWith("Screen (") ||
                goName.StartsWith("screen (") ||
                name.Contains("computertest");

            if (!isMonitorRoot) continue;

            // Skip sub-parts that sit under another monitor/desk root
            Transform p = go.transform.parent;
            if (p != null)
            {
                string pn = p.name.ToLower();
                if (pn.Contains("screen") || pn.Contains("computertest") || pn.Contains("desk"))
                    continue;
            }

            // Attach InteractablePC if missing
            InteractablePC pc = go.GetComponent<InteractablePC>();
            if (pc == null) pc = go.AddComponent<InteractablePC>();
            count++;
        }
        Debug.Log($"SceneSetup: Attached InteractablePC to {count} screen objects.");
    }

    private static void FixWallMaterials()
    {
        Material pinkBricks = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Pink Bricks.mat");
        Material blueBricks = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Bricks.mat");

        if (pinkBricks == null || blueBricks == null)
        {
            Debug.LogError("SceneSetup: Could not load wall materials!");
            return;
        }

        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go == null) continue;

            if (go.name == "Cube (15)")
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = pinkBricks;
                    Debug.Log("SceneSetup: Changed Cube (15) material to Pink Bricks.");
                }
            }
            else if (go.name == "Cube (2)")
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = blueBricks;
                    Debug.Log("SceneSetup: Changed Cube (2) material to Bricks (blue).");
                }
            }
        }
    }

    private static void SetupRoomDoor(PhysicsMaterial frictionMat)
    {
        // Find the root Door GameObject
        GameObject doorGo = null;
        var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go.name == "Door" && go.transform.parent == null)
            {
                doorGo = go;
                break;
            }
        }

        if (doorGo == null)
        {
            Debug.LogWarning("SceneSetup: Root Door GameObject not found in the scene.");
            return;
        }

        // Add DoorController component if missing
        DoorController controller = doorGo.GetComponent<DoorController>();
        if (controller == null)
        {
            controller = doorGo.AddComponent<DoorController>();
        }

        // Configure Rigidbody on the DoorGo
        Rigidbody rb = doorGo.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = doorGo.AddComponent<Rigidbody>();
        }
        rb.mass = 25f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 1.0f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = true;
        rb.isKinematic = false;

        // Configure HingeJoint
        HingeJoint hinge = doorGo.GetComponent<HingeJoint>();
        if (hinge == null)
        {
            hinge = doorGo.AddComponent<HingeJoint>();
        }
        hinge.anchor = new Vector3(0f, 0f, -0.45f); // Hinge edge of the 0.90m wide door panel
        hinge.axis = Vector3.up;
        hinge.useLimits = true;

        // Initialize limits using the controller
        controller.UpdateLimits();

        // Remove any colliders from the children of Door to prevent them from clipping/jamming in walls/floor
        var childColliders = doorGo.GetComponentsInChildren<Collider>(true);
        foreach (var c in childColliders)
        {
            if (c.gameObject != doorGo)
            {
                GameObject.DestroyImmediate(c);
            }
        }

        // Add a single BoxCollider to the parent Door GameObject
        BoxCollider boxCol = doorGo.GetComponent<BoxCollider>();
        if (boxCol == null)
        {
            boxCol = doorGo.AddComponent<BoxCollider>();
        }
        boxCol.center = Vector3.zero;
        boxCol.size = new Vector3(0.05f, 2.05f, 0.86f); // Thinner, shorter, and narrower to avoid wall/floor clipping
        if (frictionMat != null)
        {
            boxCol.sharedMaterial = frictionMat;
        }

        Debug.Log("SceneSetup: Successfully set up room Door with Rigidbody, HingeJoint, and DoorController.");
    }

    private static void InspectRoomDoor()
    {
        try
        {
            GameObject doorGo = null;
            var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allObjects)
            {
                if (go.name == "Door" && go.transform.parent == null)
                {
                    doorGo = go;
                    break;
                }
            }

            string outputPath = "door_inspection.txt";
            using (var writer = new System.IO.StreamWriter(outputPath, false))
            {
                if (doorGo == null)
                {
                    writer.WriteLine("Root Door GameObject not found in the scene.");
                    return;
                }

                writer.WriteLine("Root Door: " + doorGo.name);
                writer.WriteLine("Position: " + doorGo.transform.position);
                writer.WriteLine("Rotation: " + doorGo.transform.rotation.eulerAngles);
                writer.WriteLine("Local Scale: " + doorGo.transform.localScale);
                
                Rigidbody rb = doorGo.GetComponent<Rigidbody>();
                writer.WriteLine("Rigidbody: " + (rb != null ? "Yes" : "No"));
                if (rb != null)
                {
                    writer.WriteLine("  Mass: " + rb.mass);
                    writer.WriteLine("  IsKinematic: " + rb.isKinematic);
                    writer.WriteLine("  UseGravity: " + rb.useGravity);
                }

                HingeJoint hinge = doorGo.GetComponent<HingeJoint>();
                writer.WriteLine("HingeJoint: " + (hinge != null ? "Yes" : "No"));
                if (hinge != null)
                {
                    writer.WriteLine("  Anchor: " + hinge.anchor);
                    writer.WriteLine("  Axis: " + hinge.axis);
                    writer.WriteLine("  Use Limits: " + hinge.useLimits);
                    writer.WriteLine("  Limits Min: " + hinge.limits.min + ", Max: " + hinge.limits.max);
                }

                DoorController controller = doorGo.GetComponent<DoorController>();
                writer.WriteLine("DoorController: " + (controller != null ? "Yes" : "No"));

                writer.WriteLine("\nChildren of Door:");
                for (int i = 0; i < doorGo.transform.childCount; i++)
                {
                    Transform child = doorGo.transform.GetChild(i);
                    writer.WriteLine("- Child " + i + ": " + child.name);
                    writer.WriteLine("  Local Pos: " + child.localPosition);
                    writer.WriteLine("  Local Rot: " + child.localRotation.eulerAngles);
                    writer.WriteLine("  Local Scale: " + child.localScale);
                    
                    MeshFilter mf = child.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        Bounds bounds = mf.sharedMesh.bounds;
                        writer.WriteLine("  Mesh Bounds Center: " + bounds.center);
                        writer.WriteLine("  Mesh Bounds Size: " + bounds.size);
                        
                        // Transform bounds to parent local space
                        Vector3 childScale = child.localScale;
                        Quaternion childRot = child.localRotation;
                        Vector3 childPos = child.localPosition;
                        
                        Vector3 localMin = childPos + childRot * Vector3.Scale(bounds.min, childScale);
                        Vector3 localMax = childPos + childRot * Vector3.Scale(bounds.max, childScale);
                        writer.WriteLine("  Calculated bounds in parent local: min " + localMin + ", max " + localMax);
                    }

                    var colliders = child.GetComponents<Collider>();
                    writer.WriteLine("  Colliders Count: " + colliders.Length);
                    foreach (var col in colliders)
                    {
                        writer.WriteLine("    Collider Type: " + col.GetType().Name + ", Enabled: " + col.enabled);
                    }
                }
            }
            Debug.Log("Dumped door inspection to " + outputPath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("InspectRoomDoor failed: " + ex.Message);
        }
    }
}
// Trigger compile 9

