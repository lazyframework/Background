using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Memastikan UI bisa diklik di semua scene (Lobby, Operations, Select Character, Game Mode, Settings, dst).
// Tidak perlu dipasang manual: dibuat otomatis saat game mulai dan bertahan antar scene.
// Perbaikan yang dilakukan:
//  - EventSystem belum ada      -> dibuat.
//  - Project memakai Input System baru tapi EventSystem masih StandaloneInputModule -> diganti InputSystemUIInputModule.
//  - InputSystemUIInputModule tanpa Actions Asset (sering terjadi kalau dibuat lewat skrip) -> diisi aksi bawaan.
public class EclipseUiInputFix : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("EclipseUiInputFix");
        DontDestroyOnLoad(go);
        go.AddComponent<EclipseUiInputFix>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnLoaded;
        StartCoroutine(Run());
    }

    void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; }

    void OnLoaded(Scene s, LoadSceneMode m) { StartCoroutine(Run()); }

    // tunggu builder membuat UI-nya dulu, lalu periksa (dua kali supaya aman)
    IEnumerator Run()
    {
        yield return null;
        yield return null;
        Ensure();
        yield return new WaitForSecondsRealtime(.5f);
        Ensure();
    }

    public static void Ensure()
    {
#if UNITY_2023_1_OR_NEWER
        var es = Object.FindFirstObjectByType<EventSystem>();
        bool hasCanvas = Object.FindFirstObjectByType<Canvas>() != null;
#else
        var es = Object.FindObjectOfType<EventSystem>();
        bool hasCanvas = Object.FindObjectOfType<Canvas>() != null;
#endif
        if (es == null)
        {
            if (!hasCanvas) return;
            es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null) Destroy(legacy);
        var mod = es.GetComponent<InputSystemUIInputModule>();
        if (mod == null) mod = es.gameObject.AddComponent<InputSystemUIInputModule>();
        if (mod.actionsAsset == null) mod.AssignDefaultActions();
#else
        if (es.GetComponent<StandaloneInputModule>() == null) es.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }
}
