using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Gerbang koneksi: dipasang SEBELUM layar login.
// Saat scene mulai, layar penuh "CHECKING CONNECTION" muncul dan internet dicek sungguhan (lihat EclipseInternet).
//  - Online  -> gerbang hilang (atau langsung pindah ke Next Scene Name), login bisa dipakai.
//  - Offline -> layar "NO CONNECTION" + tombol RETRY, dicek ulang otomatis tiap beberapa detik. Login tidak bisa dijangkau.
// Cara pakai:
//  A) Scene boot/splash sebelum login: pasang di sana, isi Next Scene Name = nama scene login.
//  B) Langsung di scene login: pasang di sana, biarkan Next Scene Name kosong (login tertutup layar ini sampai online).
public class EclipseConnectionGate : MonoBehaviour
{
    [Tooltip("Scene tujuan saat online (mis. scene login). Kosong = hanya membuka layar yang sudah ada.")]
    public string nextSceneName = "";
    [Tooltip("Cek ulang otomatis tiap X detik saat offline. 0 = hanya lewat tombol RETRY.")]
    public float autoRetrySeconds = 5f;
    public Font font;
    [Tooltip("Opsional: gambar latar.")]
    public Sprite background;
    public UnityEvent onOnline = new UnityEvent();

    static readonly Color Red = new Color(.92f, .1f, .12f, 1f);
    static readonly Color Txt = new Color(.952f, .938f, .914f);
    static readonly Color Dim = new Color(.613f, .604f, .589f);

    GameObject root;
    Text bang, title, message, status;
    GameObject retryBtn;
    bool retryRequested;

    void Start()
    {
        if (font == null)
        {
#if UNITY_2022_2_OR_NEWER
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }
        BuildUI();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        while (true)
        {
            SetChecking();
            bool ok = false;
            yield return EclipseInternet.Check(r => ok = r);
            if (ok) { Finish(); yield break; }

            SetOffline();
            retryRequested = false;
            float t = 0f;
            while (!retryRequested && (autoRetrySeconds <= 0f || t < autoRetrySeconds))
            {
                t += Time.unscaledDeltaTime;
                if (autoRetrySeconds > 0f)
                    status.text = "Retrying in " + Mathf.CeilToInt(autoRetrySeconds - t) + "s";
                yield return null;
            }
        }
    }

    void Finish()
    {
        onOnline.Invoke();
        if (!string.IsNullOrEmpty(nextSceneName)) { SceneManager.LoadScene(nextSceneName); return; }
        if (root != null) Destroy(root);
    }

    void SetChecking()
    {
        bang.text = "...";
        title.text = "CHECKING CONNECTION";
        message.text = "Please wait a moment.";
        status.text = "";
        retryBtn.SetActive(false);
    }

    void SetOffline()
    {
        bang.text = "!";
        title.text = "NO CONNECTION";
        message.text = "An internet connection is required to play.\nCheck your network and try again.";
        status.text = "";
        retryBtn.SetActive(true);
    }

    // ---------------------------------------------------------------- UI

    void BuildUI()
    {
        root = new GameObject("EclipseConnectionGateCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var sc = root.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1672, 941);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // latar penuh (menutup & memblokir apa pun di belakangnya)
        var bg = Box(root.transform, "Backdrop", new Color(.05f, .052f, .058f, 1f));
        Stretch(bg.rectTransform);
        if (background != null) { bg.sprite = background; bg.color = Color.white; bg.preserveAspect = false; }
        var shade = Box(root.transform, "Shade", new Color(0f, 0f, 0f, background != null ? .55f : 0f));
        Stretch(shade.rectTransform);

        // kartu
        var edge = Box(root.transform, "CardEdge", Red);
        Center(edge.rectTransform, Vector2.zero, new Vector2(768, 424));
        var card = Box(root.transform, "Card", new Color(.075f, .077f, .085f, 1f));
        Center(card.rectTransform, Vector2.zero, new Vector2(760, 416));
        var bar = Box(root.transform, "CardBar", Red);
        Center(bar.rectTransform, new Vector2(0, 208), new Vector2(180, 6));

        bang = Label("Bang", new Vector2(0, 135), new Vector2(300, 110), 92, FontStyle.BoldAndItalic, Red);
        title = Label("Title", new Vector2(0, 45), new Vector2(700, 60), 42, FontStyle.BoldAndItalic, Txt);
        message = Label("Message", new Vector2(0, -28), new Vector2(660, 80), 22, FontStyle.Normal, Dim);
        status = Label("Status", new Vector2(0, -92), new Vector2(400, 30), 18, FontStyle.Normal, Dim);

        var btn = Box(root.transform, "RetryButton", Red);
        Center(btn.rectTransform, new Vector2(0, -152), new Vector2(280, 64));
        btn.raycastTarget = true;
        var b = btn.gameObject.AddComponent<Button>();
        b.targetGraphic = btn;
        var c = b.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(1f, .85f, .85f);
        c.pressedColor = new Color(.7f, .55f, .55f);
        b.colors = c;
        b.onClick.AddListener(() => retryRequested = true);
        var bl = Label("RetryLabel", Vector2.zero, new Vector2(280, 64), 28, FontStyle.BoldAndItalic, Color.white, btn.transform);
        bl.text = "RETRY";
        retryBtn = btn.gameObject;

        EnsureEventSystem();
    }

    Image Box(Transform parent, string n, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var im = go.GetComponent<Image>();
        im.color = c;
        im.raycastTarget = true;   // lapisan penuh memblokir klik ke belakang
        return im;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static void Center(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    Text Label(string n, Vector2 pos, Vector2 size, int fs, FontStyle st, Color col, Transform parent = null)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent != null ? parent : root.transform, false);
        Center(go.GetComponent<RectTransform>(), pos, size);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = fs;
        t.fontStyle = st;
        t.color = col;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.lineSpacing = 1.15f;
        t.raycastTarget = false;
        return t;
    }

    static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        bool none = Object.FindAnyObjectByType<EventSystem>() == null;
#else
        bool none = Object.FindObjectOfType<EventSystem>() == null;
#endif
        if (!none) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }
}

#if UNITY_EDITOR
public static class EclipseConnectionGateMenu
{
    [MenuItem("Tools/EclipseScripts/Create Connection Gate")]
    static void Create()
    {
        var go = new GameObject("Eclipse Connection Gate");
        Undo.RegisterCreatedObjectUndo(go, "Create Connection Gate");
        go.AddComponent<EclipseConnectionGate>();
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
#endif
