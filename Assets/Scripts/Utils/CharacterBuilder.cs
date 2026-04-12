using UnityEngine;

/// <summary>
/// Builds cute blocky humanoid characters from cubes (Minecraft/Crossy Road style).
/// All parts are cubes for a cohesive voxel aesthetic.
/// Total character height ~1.1 units before visual root scaling.
/// </summary>
public static class CharacterBuilder
{
    public enum AccessoryType
    {
        None,
        Crown,
        Helmet,
        StrawHat,
        WizardHat,
        Hood
    }

    [System.Serializable]
    public struct CharacterConfig
    {
        public Color bodyColor;   // torso + arms
        public Color pantsColor;  // legs
        public Color skinColor;   // head
        public Color hairColor;   // optional hair cube on top of head (use default/clear to skip)
        public AccessoryType accessory;
        public bool hasHair;      // set true to add hair cube
    }

    // Cached primitive mesh (cube only)
    private static Mesh _cubeMesh;

    private static Mesh GetCubeMesh()
    {
        if (_cubeMesh != null) return _cubeMesh;
#if UNITY_EDITOR || !UNITY_WEBGL
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var col = temp.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        _cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return _cubeMesh;
#else
        _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        return _cubeMesh;
#endif
    }

    private static Material _sharedMat;
    private static Material GetSharedMaterial()
    {
        if (_sharedMat != null) return _sharedMat;
        string[] shaderNames = {
            "Universal Render Pipeline/Lit",
            "Standard",
            "Mobile/Diffuse",
            "Legacy Shaders/Diffuse",
            "Unlit/Color",
        };
        foreach (var n in shaderNames)
        {
            var shader = Shader.Find(n);
            if (shader != null) { _sharedMat = new Material(shader); return _sharedMat; }
        }
        _sharedMat = new Material(Shader.Find("Hidden/InternalErrorShader"));
        return _sharedMat;
    }

    private static GameObject MakeCube(string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
    {
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetCubeMesh();
        go.AddComponent<MeshRenderer>();
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var renderer = go.GetComponent<Renderer>();
        renderer.material = new Material(GetSharedMaterial()) { color = color };
        return go;
    }

    /// <summary>
    /// Builds a blocky humanoid character under a "Visual" root transform.
    /// The Visual root is parented to <paramref name="parent"/> and scaled by 0.65.
    /// </summary>
    public static GameObject BuildCharacter(Transform parent, CharacterConfig config)
    {
        var visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(parent, false);
        visualRoot.transform.localScale = Vector3.one * 0.65f;

        // --- Legs ---
        MakeCube("LeftLeg", visualRoot.transform,
            new Vector3(-0.09f, 0.15f, 0f),
            new Vector3(0.12f, 0.3f, 0.12f),
            config.pantsColor);
        MakeCube("RightLeg", visualRoot.transform,
            new Vector3(0.09f, 0.15f, 0f),
            new Vector3(0.12f, 0.3f, 0.12f),
            config.pantsColor);

        // --- Body / Torso ---
        MakeCube("Body", visualRoot.transform,
            new Vector3(0f, 0.5f, 0f),
            new Vector3(0.3f, 0.4f, 0.2f),
            config.bodyColor);

        // --- Arms ---
        MakeCube("LeftArm", visualRoot.transform,
            new Vector3(-0.21f, 0.5f, 0f),
            new Vector3(0.12f, 0.35f, 0.12f),
            config.bodyColor);
        MakeCube("RightArm", visualRoot.transform,
            new Vector3(0.21f, 0.5f, 0f),
            new Vector3(0.12f, 0.35f, 0.12f),
            config.bodyColor);

        // --- Head ---
        MakeCube("Head", visualRoot.transform,
            new Vector3(0f, 0.9f, 0f),
            new Vector3(0.35f, 0.35f, 0.35f),
            config.skinColor);

        // --- Eyes ---
        Color eyeColor = new Color(0.08f, 0.06f, 0.06f);
        MakeCube("LeftEye", visualRoot.transform,
            new Vector3(-0.07f, 0.92f, 0.18f),
            new Vector3(0.04f, 0.04f, 0.02f),
            eyeColor);
        MakeCube("RightEye", visualRoot.transform,
            new Vector3(0.07f, 0.92f, 0.18f),
            new Vector3(0.04f, 0.04f, 0.02f),
            eyeColor);

        // --- Hair (optional) ---
        if (config.hasHair)
        {
            MakeCube("Hair", visualRoot.transform,
                new Vector3(0f, 1.1f, 0f),
                new Vector3(0.36f, 0.06f, 0.36f),
                config.hairColor);
        }

        // --- Accessory ---
        BuildAccessory(visualRoot.transform, config);

        return visualRoot;
    }

    private static void BuildAccessory(Transform parent, CharacterConfig config)
    {
        switch (config.accessory)
        {
            case AccessoryType.Crown:
                MakeCube("Crown", parent,
                    new Vector3(0f, 1.12f, 0f),
                    new Vector3(0.25f, 0.08f, 0.25f),
                    new Color(0.95f, 0.78f, 0.12f)); // gold
                break;

            case AccessoryType.Helmet:
                MakeCube("Helmet", parent,
                    new Vector3(0f, 1.0f, 0f),
                    new Vector3(0.37f, 0.15f, 0.37f),
                    new Color(0.4f, 0.4f, 0.45f)); // dark gray
                break;

            case AccessoryType.StrawHat:
                // Wide brim
                MakeCube("HatBrim", parent,
                    new Vector3(0f, 1.1f, 0f),
                    new Vector3(0.45f, 0.06f, 0.45f),
                    new Color(0.75f, 0.65f, 0.30f)); // straw color
                // Top
                MakeCube("HatTop", parent,
                    new Vector3(0f, 1.17f, 0f),
                    new Vector3(0.2f, 0.08f, 0.2f),
                    new Color(0.75f, 0.65f, 0.30f));
                break;

            case AccessoryType.WizardHat:
                // Stack of 3 cubes getting smaller (cone-like)
                MakeCube("WizHat1", parent,
                    new Vector3(0f, 1.1f, 0f),
                    new Vector3(0.3f, 0.08f, 0.3f),
                    new Color(0.25f, 0.12f, 0.35f)); // dark purple
                MakeCube("WizHat2", parent,
                    new Vector3(0f, 1.19f, 0f),
                    new Vector3(0.2f, 0.1f, 0.2f),
                    new Color(0.30f, 0.15f, 0.40f));
                MakeCube("WizHat3", parent,
                    new Vector3(0f, 1.3f, 0f),
                    new Vector3(0.1f, 0.12f, 0.1f),
                    new Color(0.35f, 0.18f, 0.45f));
                break;

            case AccessoryType.Hood:
                MakeCube("Hood", parent,
                    new Vector3(0f, 0.95f, -0.05f),
                    new Vector3(0.38f, 0.2f, 0.38f),
                    new Color(0.12f, 0.12f, 0.15f)); // dark
                break;
        }
    }
}
