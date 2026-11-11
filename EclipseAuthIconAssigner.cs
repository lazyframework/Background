#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Tools > Project Eclipse > Assign Login Icons
// Mengimpor PNG di Assets/EclipseLobby/AuthIcons sebagai Sprite, mengisinya ke EclipseAuthBuilder di scene, lalu membangun ulang layar login.
public static class EclipseAuthIconAssigner
{
    const string Dir = "Assets/EclipseLobby/AuthIcons";
    static readonly string[] Fields =
    {
        "iconUser", "iconUserAdd", "iconMail", "iconLock", "iconEye", "iconEyeOff", "iconGoogle", "iconDiscord", "iconBack"
    };

    [MenuItem("Tools/Project Eclipse/Assign Login Icons")]
    static void Assign()
    {
#if UNITY_2023_1_OR_NEWER
        var b = Object.FindFirstObjectByType<EclipseAuthBuilder>();
#else
        var b = Object.FindObjectOfType<EclipseAuthBuilder>();
#endif
        if (b == null) { Debug.LogError("Tidak ada EclipseAuthBuilder di scene ini."); return; }

        var so = new SerializedObject(b);
        int n = 0;
        foreach (var f in Fields)
        {
            string path = Dir + "/" + f + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning("Tidak ketemu: " + path); continue; }
            if (ti.textureType != TextureImporterType.Sprite || !ti.alphaIsTransparency)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var prop = so.FindProperty(f);
            if (sp != null && prop != null) { prop.objectReferenceValue = sp; n++; }
        }
        so.ApplyModifiedProperties();
        Undo.RegisterFullObjectHierarchyUndo(b.gameObject, "Assign Login Icons");
        b.Build();
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(b.gameObject.scene);
        Debug.Log("Login: " + n + " ikon terpasang.");
    }
}
#endif
