using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class EclipseWeaponSetupBuilder : EditorWindow
{
    enum BarrelAxisMode { Auto, PlusX, MinusX, PlusY, MinusY, PlusZ, MinusZ }

    GameObject weaponObject;
    GameObject characterRoot;

    BarrelAxisMode barrelAxis = BarrelAxisMode.Auto;
    bool flipUp = false;
    bool autoFitScale = true;
    float targetWeaponLength = 0.80f;
    bool overwriteMountSettings = true;
    float foregripAlongBarrel = 0.60f;
    float foregripHeightOffset = 0.0f;
    bool addMuzzleGizmo = true;
    bool registerInController = true;

    bool showAdvanced = false;
    Vector2 scrollPos;
    readonly List<string> logs = new List<string>();
    Color lastStatusColor = Color.gray;

    static readonly string[] AgentCandidateNames =
    {
        "Agent 013",
        "Agent013",
        "Agent_013",
        "AgentBody",
        "SWAT",
        "swat lp"
    };

    [MenuItem("Eclipse/Auto Setup Weapon & Hand IK")]
    public static void OpenWindow()
    {
        var win = GetWindow<EclipseWeaponSetupBuilder>("Eclipse Weapon Setup");
        win.minSize = new Vector2(460f, 560f);
        win.AutoDetect(false);
        win.Show();
    }

    void OnEnable()
    {
        if (weaponObject == null || characterRoot == null)
            AutoDetect(false);
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.LabelField("Eclipse Weapon Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Isi 2 referensi dari Hierarchy lalu klik SETUP. Semua langkah (bersihkan komponen nyasar, Muzzle, LeftHandGrip, Mount, Hand IK) dikerjakan otomatis dan disimpan di Scene.",
            MessageType.Info);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("1. Referensi", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        weaponObject = (GameObject)EditorGUILayout.ObjectField(
            "Weapon Object (AUG)", weaponObject, typeof(GameObject), true);
        characterRoot = (GameObject)EditorGUILayout.ObjectField(
            "Character Root (Agent 013)", characterRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            logs.Clear();
        }

        if (GUILayout.Button("Auto-Detect dari Scene (prioritas Agent 013 / AUG)", GUILayout.Height(24)))
        {
            AutoDetect(true);
        }

        EditorGUILayout.Space(6);
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Pengaturan lanjutan (opsional)", true);
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            barrelAxis = (BarrelAxisMode)EditorGUILayout.EnumPopup("Barrel Axis", barrelAxis);
            flipUp = EditorGUILayout.Toggle("Flip Up", flipUp);
            autoFitScale = EditorGUILayout.Toggle("Auto Fit Scale", autoFitScale);
            using (new EditorGUI.DisabledScope(!autoFitScale))
            {
                targetWeaponLength = EditorGUILayout.Slider("Target Weapon Length (m)", targetWeaponLength, 0.25f, 2.2f);
            }
            overwriteMountSettings = EditorGUILayout.Toggle("Timpa Setting Mount", overwriteMountSettings);
            foregripAlongBarrel = EditorGUILayout.Slider("Foregrip Along Barrel", foregripAlongBarrel, 0.2f, 0.9f);
            foregripHeightOffset = EditorGUILayout.Slider("Foregrip Height", foregripHeightOffset, -0.15f, 0.15f);
            addMuzzleGizmo = EditorGUILayout.Toggle("Add Muzzle Gizmo", addMuzzleGizmo);
            registerInController = EditorGUILayout.Toggle("Register di Controller", registerInController);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);

        string validationError = Validate();
        bool canRun = string.IsNullOrEmpty(validationError);

        using (new EditorGUI.DisabledScope(!canRun))
        {
            GUI.backgroundColor = new Color(0.28f, 0.68f, 0.42f);
            if (GUILayout.Button("SETUP SEKARANG", GUILayout.Height(36)))
            {
                RunSetup();
            }
            GUI.backgroundColor = Color.white;
        }

        if (!string.IsNullOrEmpty(validationError))
        {
            EditorGUILayout.HelpBox(validationError, MessageType.Warning);
        }

        if (logs.Count > 0)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Hasil", EditorStyles.boldLabel);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = lastStatusColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prevBg;

            for (int i = 0; i < logs.Count; i++)
            {
                EditorGUILayout.LabelField(logs[i], EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    string Validate()
    {
        if (Application.isPlaying)
            return "Hentikan Play Mode terlebih dahulu agar perubahan tersimpan permanen di Scene.";

        if (weaponObject == null)
            return "Pilih Weapon Object (AUG) dari Hierarchy.";

        if (EditorUtility.IsPersistent(weaponObject))
            return "Weapon Object harus instance di Hierarchy (Scene), bukan aset dari folder Project.";

        if (characterRoot == null)
            return "Pilih Character Root (Agent 013) dari Hierarchy.";

        if (EditorUtility.IsPersistent(characterRoot))
            return "Character Root harus instance di Hierarchy (Scene), bukan aset dari folder Project.";

        return null;
    }

    void AutoDetect(bool overwrite)
    {
        logs.Clear();

        if (overwrite || weaponObject == null)
        {
            var w = FindInScene(g =>
                NameIs(g.name, "AUG") ||
                g.name.IndexOf("aug", StringComparison.OrdinalIgnoreCase) >= 0);
            if (w != null)
                weaponObject = w;
        }

        if (overwrite || characterRoot == null)
        {
            GameObject found = null;
            for (int i = 0; i < AgentCandidateNames.Length; i++)
            {
                string candidate = AgentCandidateNames[i];
                found = FindInScene(g => NameIs(g.name, candidate));
                if (found != null)
                    break;
            }

            if (found == null)
            {
                found = FindInScene(g =>
                    g.name.IndexOf("agent 013", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    g.name.IndexOf("agent013", StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (found != null)
                characterRoot = found;
        }

        Repaint();
    }

    static GameObject FindInScene(Func<GameObject, bool> predicate)
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i];
            if (go == null) continue;
            if (EditorUtility.IsPersistent(go)) continue;
            if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
            if (predicate(go)) return go;
        }
        return null;
    }

    static bool NameIs(string actual, string target)
    {
        return string.Equals(actual.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    void RunSetup()
    {
        logs.Clear();
        lastStatusColor = new Color(0.75f, 0.92f, 0.80f);

        Undo.RegisterFullObjectHierarchyUndo(weaponObject, "Eclipse Setup Weapon");
        Undo.RegisterFullObjectHierarchyUndo(characterRoot, "Eclipse Setup Agent 013");

        // 1. Pastikan EclipseWeapon & EclipseWeaponMount ada di root weaponObject
        Component weaponComp = EnsureComponentByTypeName(weaponObject, "EclipseWeapon", out bool createdWeaponComp);
        logs.Add(createdWeaponComp
            ? "[OK] EclipseWeapon ditambahkan ke AUG."
            : "[OK] EclipseWeapon sudah ada di root.");

        Component mountComp = EnsureComponentByTypeName(weaponObject, "EclipseWeaponMount", out bool createdMountComp);
        logs.Add(createdMountComp
            ? "[OK] EclipseWeaponMount ditambahkan ke AUG."
            : "[OK] EclipseWeaponMount sudah ada di root.");

        // 2. Cari Animator di dalam characterRoot (prioritas AgentBody / SWAT / Humanoid)
        Animator bodyAnimator = FindBodyAnimator(characterRoot);
        if (bodyAnimator == null)
        {
            lastStatusColor = new Color(0.98f, 0.86f, 0.62f);
            logs.Add($"[!] Tidak ada Animator di dalam '{characterRoot.name}'. Pastikan Agent 013 / AgentBody / SWAT memiliki Animator.");
        }
        else
        {
            logs.Add($"[OK] Animator ditemukan di '{bodyAnimator.gameObject.name}'.");
        }

        // 3. Buat / pakai ulang Muzzle & LeftHandGrip
        Transform muzzle = EnsureChildPoint(weaponObject.transform, "Muzzle",
            new Vector3(-0.407f, 0.0f, 0.087f), out bool createdMuzzle);
        logs.Add(createdMuzzle
            ? "[OK] Muzzle dibuat di AUG/Muzzle."
            : "[OK] Muzzle lama dipakai ulang: AUG/Muzzle");

        Transform leftGrip = EnsureChildPoint(weaponObject.transform, "LeftHandGrip",
            new Vector3(-0.242f, -0.004f, -0.026f), out bool createdGrip);
        logs.Add(createdGrip
            ? "[OK] LeftHandGrip dibuat di AUG/LeftHandGrip."
            : "[OK] LeftHandGrip lama dipakai ulang: AUG/LeftHandGrip");

        // 4. Auto-fit scale & orientasi bila diaktifkan
        Vector3 boundsSize = GetCombinedLocalBoundsSize(weaponObject.transform, out bool hasMesh);
        if (hasMesh)
        {
            logs.Add($"[OK] Ukuran mesh terdeteksi: {boundsSize.x:F3} x {boundsSize.y:F3} x {boundsSize.z:F3}");
            if (autoFitScale && boundsSize.magnitude > 0.0001f)
            {
                float longest = Mathf.Max(boundsSize.x, Mathf.Max(boundsSize.y, boundsSize.z));
                if (longest > 0.0001f)
                {
                    float scaleFactor = targetWeaponLength / longest;
                    weaponObject.transform.localScale *= scaleFactor;
                    logs.Add($"[OK] Auto-fit scale diterapkan (panjang target {targetWeaponLength:F2} m).");
                }
            }
        }

        if (addMuzzleGizmo)
        {
            EnsureComponentByTypeName(muzzle.gameObject, "EclipseMuzzleGizmo", out _);
        }

        // 5. Sambungkan referensi pada EclipseWeapon & EclipseWeaponMount
        AssignReferences(weaponComp, mountComp, muzzle, leftGrip, bodyAnimator);

        // 6. Pasang EclipseWeaponHandIK ke objek Animator (Agent 013 / AgentBody / SWAT)
        if (bodyAnimator != null)
        {
            Component handIk = EnsureComponentByTypeName(bodyAnimator.gameObject, "EclipseWeaponHandIK", out bool createdIk);
            AssignHandIkReferences(handIk, bodyAnimator, characterRoot);
            logs.Add(createdIk
                ? $"[OK] EclipseWeaponHandIK ditambahkan ke '{bodyAnimator.gameObject.name}'."
                : $"[OK] EclipseWeaponHandIK diperbarui pada '{bodyAnimator.gameObject.name}'.");
        }
        else
        {
            logs.Add("[!] EclipseWeaponHandIK dilewati karena Animator tidak ketemu.");
        }

        // 7. Registrasi ke Weapon Controller jika diminta
        if (registerInController)
        {
            RegisterWeaponInController(characterRoot, weaponComp);
        }

        Selection.activeTransform = muzzle;
        EditorSceneManager.MarkSceneDirty(weaponObject.scene);
        logs.Add("[OK] Selesai. Tekan Ctrl+S untuk menyimpan Scene.");
    }

    static Animator FindBodyAnimator(GameObject root)
    {
        if (root == null) return null;
        var animators = root.GetComponentsInChildren<Animator>(true);
        if (animators == null || animators.Length == 0) return null;

        Animator best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < animators.Length; i++)
        {
            var anim = animators[i];
            if (anim == null) continue;

            int score = 0;
            string n = anim.gameObject.name.ToLowerInvariant();

            if (anim.isHuman) score += 100;
            if (n.Contains("agentbody")) score += 60;
            else if (n.Contains("swat")) score += 50;
            else if (n.Contains("agent")) score += 40;
            else if (n.Contains("body") || n.Contains("rig") || n.Contains("mesh")) score += 20;

            if (anim.avatar != null) score += 15;
            if (anim.gameObject.activeInHierarchy) score += 10;

            if (score > bestScore)
            {
                bestScore = score;
                best = anim;
            }
        }

        return best;
    }

    static Transform EnsureChildPoint(Transform parent, string childName, Vector3 defaultLocalPos, out bool created)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            created = false;
            return existing;
        }

        var go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = defaultLocalPos;
        go.transform.localRotation = Quaternion.identity;
        created = true;
        return go.transform;
    }

    static Vector3 GetCombinedLocalBoundsSize(Transform root, out bool hasMesh)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        Bounds combined = new Bounds(Vector3.zero, Vector3.zero);
        bool init = false;

        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] == null || filters[i].sharedMesh == null) continue;
            Bounds b = filters[i].sharedMesh.bounds;
            Matrix4x4 rel = root.worldToLocalMatrix * filters[i].transform.localToWorldMatrix;
            Bounds transformed = TransformBounds(b, rel);

            if (!init)
            {
                combined = transformed;
                init = true;
            }
            else
            {
                combined.Encapsulate(transformed);
            }
        }

        hasMesh = init;
        return init ? combined.size : Vector3.zero;
    }

    static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint(localBounds.center);
        Vector3 ext = localBounds.extents;

        Vector3 axisX = matrix.MultiplyVector(new Vector3(ext.x, 0f, 0f));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, ext.y, 0f));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, ext.z));

        Vector3 newExt = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z)
        );

        return new Bounds(center, newExt * 2f);
    }

    static Component EnsureComponentByTypeName(GameObject target, string typeName, out bool created)
    {
        created = false;
        if (target == null) return null;

        var existing = target.GetComponents<Component>()
            .FirstOrDefault(c => c != null && c.GetType().Name == typeName);
        if (existing != null) return existing;

        Type t = FindTypeByName(typeName);
        if (t == null || !typeof(Component).IsAssignableFrom(t)) return null;

        created = true;
        return Undo.AddComponent(target, t);
    }

    static Type FindTypeByName(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Type.EmptyTypes; }
            })
            .FirstOrDefault(t => t != null && t.Name == typeName);
    }

    void AssignReferences(Component weaponComp, Component mountComp, Transform muzzle, Transform leftGrip, Animator bodyAnimator)
    {
        if (weaponComp != null)
        {
            var so = new SerializedObject(weaponComp);
            SetObjectIfPropertyExists(so, "muzzle", muzzle);
            SetObjectIfPropertyExists(so, "muzzlePoint", muzzle);
            SetObjectIfPropertyExists(so, "leftHandGrip", leftGrip);
            SetObjectIfPropertyExists(so, "leftGripPoint", leftGrip);
            so.ApplyModifiedProperties();
        }

        if (mountComp != null)
        {
            var so = new SerializedObject(mountComp);
            SetObjectIfPropertyExists(so, "animator", bodyAnimator);
            SetObjectIfPropertyExists(so, "characterAnimator", bodyAnimator);
            SetObjectIfPropertyExists(so, "characterRoot", characterRoot.transform);
            if (overwriteMountSettings)
            {
                SetBoolIfPropertyExists(so, "flipUp", flipUp);
            }
            so.ApplyModifiedProperties();
        }
    }

    void AssignHandIkReferences(Component handIk, Animator bodyAnimator, GameObject root)
    {
        if (handIk == null) return;

        var so = new SerializedObject(handIk);
        SetObjectIfPropertyExists(so, "animator", bodyAnimator);
        SetObjectIfPropertyExists(so, "leftHandTarget", weaponObject.transform.Find("LeftHandGrip"));

        Component ctrl = FindWeaponController(root);
        if (ctrl != null)
        {
            SetObjectIfPropertyExists(so, "weaponController", ctrl);
        }
        so.ApplyModifiedProperties();
    }

    void RegisterWeaponInController(GameObject root, Component weaponComp)
    {
        Component ctrl = FindWeaponController(root);
        if (ctrl == null || weaponComp == null) return;

        var so = new SerializedObject(ctrl);
        var prop = so.GetIterator();
        bool changed = false;

        while (prop.NextVisible(true))
        {
            if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                prop.objectReferenceValue == null &&
                prop.name.ToLowerInvariant().Contains("weapon"))
            {
                prop.objectReferenceValue = weaponComp;
                changed = true;
            }
        }

        if (changed)
        {
            so.ApplyModifiedProperties();
            logs.Add($"[OK] AUG disambungkan ke '{ctrl.GetType().Name}' pada '{ctrl.gameObject.name}'.");
        }
    }

    static Component FindWeaponController(GameObject root)
    {
        if (root != null)
        {
            var inRoot = root.GetComponentsInChildren<Component>(true)
                .FirstOrDefault(c => c != null && c.GetType().Name.Contains("WeaponController"));
            if (inRoot != null) return inRoot;
        }

        var inScene = FindInScene(g => g.GetComponentsInChildren<Component>(true)
            .Any(c => c != null && c.GetType().Name.Contains("WeaponController")));
        if (inScene != null)
        {
            return inScene.GetComponentsInChildren<Component>(true)
                .FirstOrDefault(c => c != null && c.GetType().Name.Contains("WeaponController"));
        }

        return null;
    }

    static void SetObjectIfPropertyExists(SerializedObject so, string propName, UnityEngine.Object val)
    {
        var prop = so.FindProperty(propName);
        if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
        {
            prop.objectReferenceValue = val;
        }
    }

    static void SetBoolIfPropertyExists(SerializedObject so, string propName, bool val)
    {
        var prop = so.FindProperty(propName);
        if (prop != null && prop.propertyType == SerializedPropertyType.Boolean)
        {
            prop.boolValue = val;
        }
    }
}
