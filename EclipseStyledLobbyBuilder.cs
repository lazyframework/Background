using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Lobby "Project Eclipse" v2.
// - Tidak memakai mesh kustom (MaskableGraphic). Semua pakai Image biasa, jadi tampil di renderer apa pun.
// - Frame panel dibuat dari sprite 9-slice yang dihasilkan lewat kode.
// - Semua gambar (latar, ikon, potret, banner, peta) diisi sendiri lewat Inspector; kosong = placeholder polos.
// - Dibangun ulang setiap Play, jadi tidak ada hierarki lama yang basi.
public class EclipseStyledLobbyBuilder : MonoBehaviour
{
    [Header("Opsional: isi gambar sendiri (kosong = placeholder polos)")]
    public Font font;
    public Sprite background, portrait, agentBanner, mapArtwork;
    public RenderTexture characterPreview;

    [Header("Karakter terpilih")]
    [Tooltip("Daftar agen bersama (sama dengan layar Select Character). Kosong = hanya kapsul penanda.")]
    public EclipseAgentDatabase database;
    [Tooltip("Tampilkan karakter yang dipilih di layar Select Character.")]
    public bool showSelectedAgent = true;
    public string selectedAgentPrefsKey = "EclipseSelectedAgent";
    [Tooltip("Kecepatan putar otomatis model di lobby (0 = diam, bisa diputar dengan menyeret).")]
    public float modelAutoSpin = 0f;

    [Header("Opsional: ikon (kosong = tidak ditampilkan)")]
    public Sprite gem, coin, bell, friends, gearTop, calendar, speaker, smallGem;
    public Sprite rankShield, seasonShield;
    public Sprite battlePass, eventStar;
    public Sprite navOperations, navAgents, navArmory, navInventory, navStore, navSettings;

    [Header("Deploy")]
    public bool loadSceneOnDeploy = true;
    [Tooltip("Tombol Operations / Agents / Armory / Inventory / Store / Settings memuat scene masing-masing (nama diatur di EclipseScenes).")]
    public bool loadScenesOnNavigate = true;
    [Tooltip("Tujuan tombol DEPLOY.")]
    public string gameSceneName = "GameMode";
    [Tooltip("Kunci PlayerPrefs nama pemain (diisi oleh layar Login).")]
    public string playerNamePrefsKey = "EclipsePlayerName";

    [Header("Profile")]
    public string playerName = "FamaLine";
    public int level = 12, diamonds = 1200, coins = 15340;

    [Serializable] public class RouteEvent : UnityEvent<string> { }
    public RouteEvent onNavigate = new RouteEvent();
    public UnityEvent onDeploy = new UnityEvent();
    public Color accent = new Color(.86f, .09f, .12f, 1);

    const string Root = "EclipseLobbyCanvas_v2";
    const string Legacy = "StyledLobbyCanvas";

    RectTransform frame;
    // ---- anchor responsif: elemen level-atas menempel ke sisi kiri/kanan/atas/bawah layar ----
    enum HA { Left, Center, Right }
    enum VA { Top, Center, Bottom }
    HA ha = HA.Left;
    VA va = VA.Top;

    public enum BackdropMode { Stretch, Cover }
    [Header("Latar layar penuh")]
    [Tooltip("Stretch = memenuhi layar di semua rasio tanpa kosong dan tanpa terpotong (gambar agak melebar/menyempit). Cover = rasio terjaga, tapi sisi gambar bisa terpotong.")]
    public BackdropMode backdropMode = BackdropMode.Stretch;

    void BackdropStretch()
    {
        var parent = frame.parent;
        var cont = new GameObject("BackdropRoot", typeof(RectTransform), typeof(RectMask2D));
        cont.transform.SetParent(parent, false);
        cont.transform.SetAsFirstSibling();
        var cr = cont.GetComponent<RectTransform>();
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;

        var solid = new GameObject("BackdropSolid", typeof(RectTransform), typeof(Image));
        solid.transform.SetParent(cont.transform, false);
        var sr = solid.GetComponent<RectTransform>();
        sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;
        var si = solid.GetComponent<Image>();
        si.color = new Color(.067f, .069f, .073f);
        si.raycastTarget = false;

        var bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(EclipseIconSlot));
        bg.transform.SetParent(cont.transform, false);
        var img = bg.GetComponent<Image>();
        img.sprite = background;
        img.color = Color.white;
        img.raycastTarget = false;
        img.preserveAspect = false;
        var br = bg.GetComponent<RectTransform>();
        if (backdropMode == BackdropMode.Cover)
        {
            var fit = bg.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1672f / 941f;
        }
        else
        {
            br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;
        }
    }

    // garis selebar layar (margin kiri-kanan), menempel ke atas atau bawah
    void HLine(string n, float y, float h, float margin, Color c, bool bottom)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(frame, false);
        var r = go.GetComponent<RectTransform>();
        float ay = bottom ? 0 : 1;
        r.anchorMin = new Vector2(0, ay);
        r.anchorMax = new Vector2(1, ay);
        r.pivot = new Vector2(.5f, ay);
        if (bottom) { r.offsetMin = new Vector2(margin, 941 - (y + h)); r.offsetMax = new Vector2(-margin, 941 - y); }
        else { r.offsetMin = new Vector2(margin, -(y + h)); r.offsetMax = new Vector2(-margin, -y); }
        var im = go.GetComponent<Image>();
        im.color = c;
        im.raycastTarget = false;
    }

    readonly Dictionary<Transform, Vector2> origin = new Dictionary<Transform, Vector2>();

    static Sprite sFill, sLine, sGlow, sRing, sDisc;

    // Warna
    static readonly Color Txt = new Color(.952f, .938f, .914f);
    static readonly Color Dim = new Color(.613f, .604f, .589f);
    static readonly Color PanelFill = new Color(.05f, .052f, .06f, .82f);
    static readonly Color PanelLine = new Color(.2f, .21f, .24f, .9f);
    static readonly Color BrightFill = new Color(.5f, .035f, .06f, .97f);
    static readonly Color BrightLine = new Color(.95f, .2f, .22f, 1f);

    void Start()
    {
        var root = transform.Find(Root);
        if (root == null) { Build(); ApplyStoredName(); InitModel(); return; }
        // Hierarki sudah dibuat di editor: cukup pasang ulang listener tombol (listener runtime tidak tersimpan di scene)
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            string id = b.gameObject.name;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => Route(id));
        }
        ApplyStoredName();
        InitModel();
    }

    // menampilkan karakter terpilih (model 3D, gambar 2D, atau kapsul penanda)
    void InitModel()
    {
        if (!Application.isPlaying || !showSelectedAgent) return;
        var root = transform.Find(Root);
        if (root == null) return;

        var st = GetComponent<EclipseCharacterStage>();
        if (st == null) st = gameObject.AddComponent<EclipseCharacterStage>();
        st.autoSpin = modelAutoSpin;
        st.Init(characterPreview);

        int idx = PlayerPrefs.GetInt(selectedAgentPrefsKey, 0);
        EclipseAgentData a = database != null ? database.Get(idx) : null;
        if (database != null && database.Count > 0) idx = Mathf.Clamp(idx, 0, database.Count - 1);

        RawImage raw = null;
        Image art = null;
        EclipseModelDrag drag = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "CharacterModel") { raw = t.GetComponent<RawImage>(); drag = t.GetComponent<EclipseModelDrag>(); }
            else if (t.name == "CharacterArt") art = t.GetComponent<Image>();
        }
        if (raw == null) return;   // hierarki lama: klik BUILD STYLED LOBBY sekali
        raw.texture = st.Target;
        raw.enabled = true;
        if (drag != null) drag.stage = st;

        bool has2D = a != null && a.characterArt != null && a.modelPrefab == null;
        st.Show(a != null ? a.modelPrefab : null, !has2D, idx);
        if (art != null) { art.sprite = has2D ? a.characterArt : null; art.enabled = has2D; }
    }

    // nama pemain dari layar Login menggantikan teks profil
    void ApplyStoredName()
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(playerNamePrefsKey)) return;
        string n = PlayerPrefs.GetString(playerNamePrefsKey, "");
        if (string.IsNullOrEmpty(n)) return;
        playerName = n;
        var root = transform.Find(Root);
        if (root == null) return;
        foreach (var t in root.GetComponentsInChildren<Text>(true))
            if (t.name == "PlayerName") { t.text = n; break; }
    }

    void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (Application.CanStreamedLevelBeLoaded(sceneName)) SceneManager.LoadScene(sceneName);
        else Debug.LogError("Scene '" + sceneName + "' belum ada di File > Build Profiles > Scene List.", this);
    }

#if UNITY_EDITOR
    // Langsung dibangun di editor begitu komponen ditambahkan
    void Reset()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && transform.Find(Root) == null) Build(); };
    }
#endif

    public void Route(string id)
    {
        onNavigate.Invoke(id);
        if (id == "Deploy")
        {
            onDeploy.Invoke();
            if (loadSceneOnDeploy) LoadScene(gameSceneName);
        }
        else if (loadScenesOnNavigate)
        {
            string target = EclipseScenes.FromId(id);
            if (!string.IsNullOrEmpty(target) && id != "Home") EclipseScenes.Load(target, this);
        }
        Debug.Log("Lobby: " + id, this);
    }

    // ---------------------------------------------------------------- helpers

    RectTransform R(Transform p, string n, float x, float y, float w, float h)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var r = go.GetComponent<RectTransform>();
        r.SetParent(p, false);
        Vector2 o;
        origin.TryGetValue(p, out o);
        r.pivot = new Vector2(0, 1);
        if (p == frame)
        {
            float ax = ha == HA.Left ? 0 : (ha == HA.Right ? 1672 : 836);
            float ay = va == VA.Top ? 0 : (va == VA.Bottom ? 941 : 470.5f);
            r.anchorMin = r.anchorMax = new Vector2(ha == HA.Left ? 0 : (ha == HA.Right ? 1 : .5f),
                                                    va == VA.Top ? 1 : (va == VA.Bottom ? 0 : .5f));
            r.anchoredPosition = new Vector2(x - ax, -(y - ay));
        }
        else
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x - o.x, -(y - o.y));
        }
        r.sizeDelta = new Vector2(w, h);
        origin[r] = new Vector2(x, y);
        return r;
    }

    Image Img(Transform p, string n, float x, float y, float w, float h, Sprite s, Color c, bool sliced = false, bool ray = false)
    {
        var i = R(p, n, x, y, w, h).gameObject.AddComponent<Image>();
        i.sprite = s;
        i.color = c;
        i.raycastTarget = ray;
        if (s != null)
        {
            if (sliced) i.type = Image.Type.Sliced;
            else i.preserveAspect = true;
        }
        return i;
    }

    Text T(Transform p, string n, string s, float x, float cy, float w, int size, bool bold = false,
           TextAnchor a = TextAnchor.MiddleLeft, bool italic = false, Color? col = null)
    {
        float h = size * 1.6f;
        var t = R(p, n, x, cy - h / 2f, w, h).gameObject.AddComponent<Text>();
        t.font = font;
        t.text = s;
        t.fontSize = size;
        t.fontStyle = bold ? (italic ? FontStyle.BoldAndItalic : FontStyle.Bold) : (italic ? FontStyle.Italic : FontStyle.Normal);
        t.color = col ?? Txt;
        t.alignment = a;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    Image S(Transform p, string n, float x, float y, float w, float h, Color c)
    {
        return Img(p, n, x, y, w, h, null, c);
    }

    // Slot ikon selalu dibuat (walau sprite kosong) supaya bisa diisi lewat Source Image di Hierarchy.
    Image Icon(Transform p, string n, Sprite sprite, float x, float y, float w, float h)
    {
        var i = Img(p, n, x, y, w, h, sprite, Color.white);
        i.preserveAspect = true;
        i.gameObject.AddComponent<EclipseIconSlot>();
        return i;
    }

    // Slot gambar besar (potret, banner, peta): ditaruh paling belakang di dalam panelnya.
    Image Art(Transform p, string n, float x, float y, float w, float h, Sprite sprite)
    {
        var i = Img(p, n, x, y, w, h, sprite, Color.white);
        i.preserveAspect = false;
        i.gameObject.AddComponent<EclipseIconSlot>();
        i.transform.SetAsFirstSibling();
        return i;
    }

    // Panel = akar + Glow(opsional) + Fill + Outline
    RectTransform Panel(Transform p, string n, float x, float y, float w, float h, Color fill, Color line, float glow = 0, bool art = false)
    {
        var r = R(p, n, x, y, w, h);
        if (glow > 0)
        {
            var g = Img(r, "Glow", x - 20, y - 20, w + 40, h + 40, GlowSprite(), new Color(.9f, .1f, .12f, glow), true);
        }
        if (!art) Img(r, "Fill", x, y, w, h, FillSprite(), fill, true);
        Img(r, "Outline", x, y, w, h, LineSprite(), line, true);
        return r;
    }

    void Detail(Transform p, float x, float y, float w, float h)
    {
        S(p, "TopAccent", x + 22, y + 1, Mathf.Min(64, w * .24f), 2, accent);
        S(p, "LowerTrace", x + 14, y + h - 7, w - 52, 1, new Color(.245f, .253f, .268f, .55f));
        for (int i = 0; i < 3; i++)
        {
            var t = S(p, "Slash" + i, x + w - 30 + i * 7, y + h - 13, 3, 9, new Color(.327f, .337f, .357f, .75f));
            t.rectTransform.localEulerAngles = new Vector3(0, 0, -30);
        }
    }

    void Click(RectTransform root)
    {
        var fill = root.Find("Fill");
        Graphic target = null;
        var outline = root.Find("Outline");
        if (outline != null) target = outline.GetComponent<Graphic>();
        if (target == null)
        {
            var ic = root.Find("Icon");
            if (ic != null) target = ic.GetComponent<Graphic>();
        }
        if (fill != null) fill.GetComponent<Graphic>().raycastTarget = true;
        else
        {
            // panel tanpa fill / ikon: pakai gambar transparan sebagai area klik
            var hit = Img(root, "Hit", origin[root].x, origin[root].y, root.sizeDelta.x, root.sizeDelta.y, null, new Color(0, 0, 0, 0), false, true);
            if (target == null) target = hit;
        }
        if (target == null) target = root.GetComponentInChildren<Graphic>();
        var b = root.gameObject.AddComponent<Button>();
        b.targetGraphic = target;
        var c = b.colors;
        c.normalColor = new Color(.92f, .92f, .93f, 1f);
        c.highlightedColor = Color.white;
        c.pressedColor = new Color(.68f, .66f, .62f, 1f);
        c.selectedColor = Color.white;
        c.fadeDuration = .1f;
        b.colors = c;
        string id = root.name;
        b.onClick.AddListener(() => Route(id));
    }

    // ---------------------------------------------------------------- sprite generator

    static float Sd(float x, float y, float x0, float y0, float x1, float y1, float c)
    {
        float d = Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(y - y0, y1 - y));
        float tl = ((x - x0) + (y1 - y) - c) * 0.70710678f; // pojok kiri-atas terpotong
        float br = ((x1 - x) + (y - y0) - c) * 0.70710678f; // pojok kanan-bawah terpotong
        return Mathf.Min(d, Mathf.Min(tl, br));
    }

    static Sprite Make(string name, int w, int h, int border, Func<float, float, Color> px)
    {
#if UNITY_EDITOR
        // Di editor sprite disimpan sebagai PNG nyata di Assets/EclipseLobby/Generated,
        // supaya hierarki yang dibangun bisa tersimpan di scene.
        const string dir = "Assets/EclipseLobby/Generated";
        string path = dir + "/" + name + ".png";
        var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
#endif
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.name = name;
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        var cols = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                cols[y * w + x] = px(x + .5f, y + .5f);
        t.SetPixels32(cols);
        t.Apply();
#if UNITY_EDITOR
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
        var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
        ti.textureType = UnityEditor.TextureImporterType.Sprite;
        ti.spriteImportMode = UnityEditor.SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.spriteBorder = new Vector4(border, border, border, border);
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        var s = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                              new Vector4(border, border, border, border));
        s.name = name;
        return s;
#endif
    }

    static Sprite FillSprite()
    {
        if (sFill == null)
            sFill = Make("EclipseFill", 64, 64, 20, (x, y) =>
            {
                float d = Sd(x, y, 0, 0, 64, 64, 12);
                float a = Mathf.Clamp01(d + .5f);
                float g = Mathf.Lerp(.72f, 1f, y / 64f);
                return new Color(g, g, g, a);
            });
        return sFill;
    }

    static Sprite LineSprite()
    {
        if (sLine == null)
            sLine = Make("EclipseLine", 64, 64, 20, (x, y) =>
            {
                float d = Sd(x, y, 0, 0, 64, 64, 12);
                float a = Mathf.Clamp01(d + .5f) * Mathf.Clamp01(2.1f - d);
                return new Color(1, 1, 1, a);
            });
        return sLine;
    }

    static Sprite GlowSprite()
    {
        if (sGlow == null)
            sGlow = Make("EclipseGlow", 64, 64, 20, (x, y) =>
            {
                float d = Sd(x, y, 20, 20, 44, 44, 12);
                float a = d >= 0 ? 1f : Mathf.Pow(Mathf.Clamp01(1f + d / 20f), 2.2f);
                return new Color(1, 1, 1, a);
            });
        return sGlow;
    }

    static Sprite RingSprite()
    {
        if (sRing == null)
            sRing = Make("EclipseRing", 32, 32, 0, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 16f) * (x - 16f) + (y - 16f) * (y - 16f));
                return new Color(1, 1, 1, Mathf.Clamp01(1.6f - Mathf.Abs(r - 13.5f) + .5f));
            });
        return sRing;
    }

    static Sprite DiscSprite()
    {
        if (sDisc == null)
            sDisc = Make("EclipseDisc", 32, 32, 0, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 16f) * (x - 16f) + (y - 16f) * (y - 16f));
                return new Color(1, 1, 1, Mathf.Clamp01(15f - r));
            });
        return sDisc;
    }

    // ---------------------------------------------------------------- build

    [ContextMenu("Build Styled Lobby")]
    public void Build()
    {
        foreach (var name in new[] { Root, Legacy })
        {
            Transform old;
            while ((old = transform.Find(name)) != null)
            {
                old.gameObject.SetActive(false);
                old.SetParent(null);
                if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }
        }

        origin.Clear();

        if (font == null)
        {
#if UNITY_2022_2_OR_NEWER
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        var go = new GameObject(Root, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scale = go.GetComponent<CanvasScaler>();
        scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1672, 941);
        scale.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // frame memenuhi seluruh layar; isi menempel ke sisi layar lewat anchor (lihat R())
        frame = new GameObject("ReferenceFrame", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(go.transform, false);
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        frame.gameObject.AddComponent<EclipseSafeArea>(); // UI tidak tertutup notch / sudut layar
        origin[frame] = Vector2.zero;

        Background();
        Header();
        Profile();
        Left();
        Right();
        Bottom();

#if UNITY_2023_1_OR_NEWER
        bool noES = UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null;
#else
        bool noES = UnityEngine.Object.FindObjectOfType<EventSystem>() == null;
#endif
        if (noES)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }
    }

    void Background()
    {
        BackdropStretch();
        ha = HA.Center; va = VA.Center;

        // gambar 2D karakter (dipakai kalau agen tidak punya model 3D)
        var art = Img(frame, "CharacterArt", 486, 100, 700, 740, null, Color.white);
        art.preserveAspect = true;
        art.gameObject.AddComponent<EclipseIconSlot>();

        // model 3D: RawImage menampilkan RenderTexture dari EclipseCharacterStage (aktif saat Play)
        var mr = R(frame, "CharacterModel", 486, 100, 700, 740);
        var raw = mr.gameObject.AddComponent<RawImage>();
        raw.texture = characterPreview;
        raw.enabled = characterPreview != null;
        raw.raycastTarget = true;
        mr.gameObject.AddComponent<EclipseModelDrag>();
    }

    void Header()
    {
        ha = HA.Right; va = VA.Top;
        Wallet("Coins", 1083, 168, coin, coins.ToString("N0"));
        Wallet("Diamonds", 1285, 160, gem, diamonds.ToString("N0"));

        S(frame, "DividerMail", 1475, 40, 1, 32, new Color(.4f, .41f, .44f, .55f));
        Top("Notifications", bell, 1503, 36, 46, 40);
        Img(frame, "MailBadge", 1539, 36, 11, 11, DiscSprite(), accent);
        S(frame, "DividerGear", 1575, 40, 1, 32, new Color(.4f, .41f, .44f, .55f));
        Top("SettingsTop", gearTop, 1598, 36, 44, 40);
    }

    void Wallet(string id, float x, float w, Sprite icon, string value)
    {
        var p = Panel(frame, id, x, 34, w, 44, new Color(.05f, .052f, .06f, .82f), new Color(.2f, .21f, .24f, .9f));
        Icon(p, "Icon", icon, x + 8, 38, 34, 36);
        T(p, "Balance", value, x + 58, 56, w - 100, 22);
        T(p, "Plus", "+", x + w - 44, 56, 36, 32, false, TextAnchor.MiddleCenter, false, Dim);
        Click(p);
    }

    void Top(string id, Sprite sprite, float x, float y, float w, float h)
    {
        var r = R(frame, id, x, y, w, h);
        Icon(r, "Icon", sprite, x, y, w, h);
        Click(r);
    }

    void Profile()
    {
        ha = HA.Left; va = VA.Top;
        var r = R(frame, "ProfileGroup", 28, 16, 420, 92);

        var p = Panel(r, "Portrait", 32, 20, 93, 84, Color.clear, new Color(.62f, .63f, .66f, .95f), 0, true);
        Art(p, "OwnPortrait", 34, 22, 89, 80, portrait);
        Click(p);

        T(r, "PlayerName", playerName, 155, 46, 260, 22);
        T(r, "Level", "Lv. " + level, 155, 76, 60, 16);
        S(r, "XPTrack", 210, 73, 190, 4, new Color(.2f, .21f, .23f, .9f));
        S(r, "XPFill", 210, 73, 55, 4, accent);
    }

    // banner NEW UPDATE di kiri
    void Left()
    {
        ha = HA.Left; va = VA.Top;
        var b = Panel(frame, "NewUpdate", 32, 163, 416, 129, new Color(.1f, .1f, .12f, .95f), new Color(.34f, .35f, .39f, .9f), 0, true);
        Art(b, "OwnBanner", 33, 164, 414, 127, agentBanner);
        Img(b, "Shade", 33, 164, 414, 127, FillSprite(), new Color(0, 0, 0, .3f), true);
        S(b, "AccentEdge", 32, 190, 4, 102, accent);
        T(b, "Heading", "NEW UPDATE", 68, 234, 280, 26, true, TextAnchor.MiddleLeft, true);
        T(b, "Sub", "Better Experience", 68, 261, 250, 16, false, TextAnchor.MiddleLeft, false, Dim);
        T(b, "Chevron", ">", 408, 264, 30, 26, false, TextAnchor.MiddleCenter);
        Click(b);
    }

    // kolom kanan: Daily Mission, Battle Pass, Event
    void Right()
    {
        ha = HA.Right; va = VA.Top;
        const float X = 1307, W = 333;
        Color gold = new Color(1f, .72f, .25f);

        var d = Panel(frame, "DailyMission", X, 220, W, 79, PanelFill, PanelLine);
        Icon(d, "Calendar", calendar, X + 20, 236, 44, 46);
        T(d, "Title", "Daily Mission", X + 91, 248, 200, 20);
        T(d, "Timer", "12h 45m", X + 91, 274, 150, 16, false, TextAnchor.MiddleLeft, false, Dim);
        T(d, "Arrow", ">", X + W - 36, 260, 26, 26, false, TextAnchor.MiddleCenter);
        Click(d);

        var bp = Panel(frame, "BattlePass", X, 331, W, 88, PanelFill, PanelLine);
        Icon(bp, "Icon", battlePass, X + 8, 338, 70, 76);
        T(bp, "Title", "Battle Pass", X + 98, 359, 200, 20);
        T(bp, "Level", "Lv. 12", X + 98, 385, 80, 16, false, TextAnchor.MiddleLeft, false, gold);
        T(bp, "Progress", "50 / 100", X + 170, 391, 112, 15, false, TextAnchor.MiddleRight, false, Dim);
        S(bp, "Track", X + 99, 402, 183, 3, new Color(.2f, .21f, .23f, 1));
        S(bp, "Fill", X + 99, 402, 91, 3, gold);
        T(bp, "Arrow", ">", X + W - 36, 377, 26, 26, false, TextAnchor.MiddleCenter);
        Click(bp);

        var ev = Panel(frame, "Event", X, 452, W, 85, new Color(.3f, .035f, .06f, .92f), new Color(.62f, .12f, .15f, .95f));
        Img(ev, "Shine", X + 2, 454, W * .55f, 81, FillSprite(), new Color(.75f, .1f, .13f, .28f), true);
        Icon(ev, "Icon", eventStar, X + 14, 466, 58, 58);
        T(ev, "Title", "Event", X + 102, 483, 200, 20);
        T(ev, "Sub", "Time Limited", X + 102, 509, 200, 16, false, TextAnchor.MiddleLeft, false, new Color(.78f, .7f, .7f));
        T(ev, "Arrow", ">", X + W - 36, 495, 26, 26, false, TextAnchor.MiddleCenter);
        Click(ev);
    }

    // menu navigasi bawah + tombol DEPLOY
    void Bottom()
    {
        ha = HA.Left; va = VA.Bottom;
        S(frame, "NavBar", 30, 800, 1027, 100, new Color(0, 0, 0, .38f));
        float[] cx = { 112, 294, 469, 640, 805, 973 };
        float[] dv = { 202, 384, 556, 722, 889 };
        for (int i = 0; i < dv.Length; i++) S(frame, "NavDivider" + i, dv[i], 812, 1, 78, new Color(.4f, .41f, .44f, .5f));
        NavItem("Operations", "Operations", cx[0], navOperations, true);
        NavItem("Agents", "Agents", cx[1], navAgents);
        NavItem("Armory", "Armory", cx[2], navArmory);
        NavItem("Inventory", "Inventory", cx[3], navInventory);
        NavItem("Store", "Store", cx[4], navStore);
        NavItem("Settings", "Settings", cx[5], navSettings);

        ha = HA.Right; va = VA.Bottom;
        const float X = 1195, Y = 790, W = 445, H = 102;
        Color hot = new Color(1f, .27f, .27f);
        var d = Panel(frame, "Deploy", X, Y, W, H, new Color(.5f, .035f, .06f, .97f), new Color(.95f, .2f, .22f, 1f), .55f);
        Img(d, "InnerFrame", X + 6, Y + 6, W - 12, H - 12, LineSprite(), new Color(1f, .3f, .3f, .35f), true);
        S(d, "TopSpecular", X + 90, Y + 4, W - 140, 2, new Color(1f, .45f, .4f));
        for (int i = 0; i < 2; i++)
        {
            var s = S(d, "Slash" + i, X + 22 + i * 26, Y + 12, i == 0 ? 16 : 8, 80, hot);
            s.rectTransform.localEulerAngles = new Vector3(0, 0, -20);
        }
        T(d, "Title", "DEPLOY", X + 110, Y + 52, 260, 54, true, TextAnchor.MiddleCenter, true);
        T(d, "Chevrons", "\u00BB", X + W - 70, Y + 52, 60, 50, true, TextAnchor.MiddleCenter, true, hot);
        Click(d);
    }

    // satu item navigasi bawah: ikon di atas, label di bawah
    void NavItem(string id, string label, float cx, Sprite icon, bool active = false)
    {
        var r = R(frame, id, cx - 80, 806, 160, 92);
        Icon(r, "Icon", icon, cx - 24, 812, 48, 48);
        T(r, "Title", label, cx - 80, 876, 160, 20, false, TextAnchor.MiddleCenter);
        if (active) S(r, "ActiveLine", cx - 80, 902, 160, 2, new Color(.9f, .9f, .92f, .6f));
        Click(r);
    }
}
