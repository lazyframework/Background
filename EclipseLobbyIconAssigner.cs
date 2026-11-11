#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Tools > Project Eclipse > Assign Lobby Icons
// Mengimpor PNG di Assets/EclipseLobby/Icons sebagai Sprite, mengisinya ke EclipseStyledLobbyBuilder di scene, lalu membangun ulang lobby.
public static class EclipseLobbyIconAssigner
{
    const string Dir = "Assets/EclipseLobby/Icons";
    static readonly string[] Fields =
    {
        "portrait", "coin", "gem", "bell", "gearTop", "calendar", "battlePass", "eventStar",
        "navOperations", "navAgents", "navArmory", "navInventory", "navStore", "navSettings"
    };

    [MenuItem("Tools/Project Eclipse/Assign Lobby Icons")]
    static void Assign()
    {
#if UNITY_2023_1_OR_NEWER
        var b = Object.FindFirstObjectByType<EclipseStyledLobbyBuilder>();
#else
        var b = Object.FindObjectOfType<EclipseStyledLobbyBuilder>();
#endif
        if (b == null) { Debug.LogError("Tidak ada EclipseStyledLobbyBuilder di scene ini."); return; }

        var so = new SerializedObject(b);
        int n = 0;
        foreach (var f in Fields)
        {
            string path = Dir + "/" + f + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning("Tidak ketemu: " + path); continue; }
            if (ti.textureType != TextureImporterType.Sprite || ti.alphaIsTransparency == false)
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
        Undo.RegisterFullObjectHierarchyUndo(b.gameObject, "Assign Lobby Icons");
        b.Build();
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(b.gameObject.scene);
        Debug.Log("Lobby: " + n + " ikon terpasang.");
    }
}
#endif
