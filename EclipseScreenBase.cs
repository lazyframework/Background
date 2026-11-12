using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Dasar semua layar menu baru (Operations, Armory, Inventory, Store, Settings).
// Tema: gelap + satu aksen amber, gaya game AAA. Semua dibangun lewat kode (Image biasa + sprite 9-slice otomatis),
// dibangun ulang tiap Play. Klik BUILD di Inspector untuk pratinjau di editor.
public abstract class EclipseScreenBase : MonoBehaviour
{
    [Header("Opsional: gambar sendiri (kosong = placeholder polos)")]
    public Font font;
    public Sprite background;
    public Sprite gem, coin, bell;
    [Header("Ikon header (opsional)")]
    public Sprite iconBack, iconHome, iconGear;

    public enum BackdropMode { Stretch, Cover }
    [Header("Latar layar penuh")]
    public BackdropMode backdropMode = BackdropMode.Stretch;

    [Serializable] public class RouteEvent : UnityEvent<string> { }
    [Header("Event")]
    public RouteEvent onNavigate = new RouteEvent();

    public Color cyan = new Color(.92f, .1f, .12f, 1f);   // warna aksen (merah). Diset ulang tiap Build supaya semua layar seragam.

    // ---------------------------------------------------------------- subclass
    protected abstract string RootName { get; }
    protected abstract string TabId { get; }
    protected abstract string PageTitle { get; }
    protected abstract string PageSubtitle { get; }
    protected abstract void BuildContent();
    // layar bergaya khusus (Game Mode, Select Character) mematikan header standar dan memakai RefHeader()
    protected virtual bool UseStandardHeader { get { return true; } }
    protected virtual float ShadeAlpha { get { return .2f; } }
    protected virtual string LobbyScene { get { return EclipseScenes.Lobby; } }
    protected virtual void OnRoute(string id) { }
    protected virtual void OnRefresh() { }
    protected virtual void OnBuilt() { }

    // ---------------------------------------------------------------- state
    protected enum HA { Left, Center, Right }
    protected enum VA { Top, Center, Bottom }
    protected Color glowColor = new Color(.9f, .08f, .1f);
    protected HA ha = HA.Left;
    protected VA va = VA.Top;
    protected RectTransform frame;
    protected bool built;
    protected readonly Dictionary<Transform, Vector2> origin = new Dictionary<Transform, Vector2>();

    Text walletDia, walletCoin;
    CanvasGroup toastGroup;
    Text toastText;
    Coroutine toastCo;

    // ---------------------------------------------------------------- palet
    protected static readonly Color Txt = new Color(.952f, .938f, .914f);
    protected static readonly Color Dim = new Color(.613f, .604f, .589f);
    protected static readonly Color PanelFill = new Color(.083f, .085f, .09f, .86f);
    protected static readonly Color PanelLine = new Color(.307f, .316f, .335f, .95f);
    protected static readonly Color AccentColor = new Color(.92f, .1f, .12f, 1f);
    protected static readonly Color BrightFill = new Color(.72f, .04f, .08f, .98f);
    protected static readonly Color BrightLine = new Color(.95f, .17f, .19f, 1f);
    protected static readonly Color Track = new Color(.146f, .149f, .158f, 1f);
    protected static readonly Color SelFill = new Color(.16f, .03f, .045f, .96f);
    protected static readonly Color Rule = new Color(.272f, .28f, .297f, .6f);
    protected static readonly Color Gold = new Color(1f, .78f, .25f);
    protected static readonly Color Ice = new Color(.55f, .82f, 1f);
    protected static readonly Color Danger = new Color(1f, .42f, .42f);
    protected static readonly Color Ok = new Color(.45f, .9f, .6f);

    protected static Color RarityColor(EclipseRarity r)
    {
        switch (r)
        {
            case EclipseRarity.Rare: return new Color(.35f, .65f, .95f);
            case EclipseRarity.Epic: return new Color(.72f, .42f, .95f);
            case EclipseRarity.Legendary: return new Color(1f, .7f, .2f);
        }
        return new Color(.62f, .64f, .68f);
    }

    // ---------------------------------------------------------------- lifecycle
    void OnEnable()
    {
        EclipseEconomy.OnChanged += HandleEconomy;
        EclipsePlayerData.OnChanged += HandleProfile;
    }

    void OnDisable()
    {
        EclipseEconomy.OnChanged -= HandleEconomy;
        EclipsePlayerData.OnChanged -= HandleProfile;
    }

    void HandleEconomy() { if (!built) return; RefreshWallet(); OnRefresh(); }
    void HandleProfile(EclipsePlayerProfile p) { if (!built) return; RefreshWallet(); OnRefresh(); }

    protected virtual void Start()
    {
        if (Application.isPlaying) Build();
    }

#if UNITY_EDITOR
    void Reset()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && transform.Find(RootName) == null) Build(); };
    }
#endif

    public void Refresh() { if (built) { RefreshWallet(); OnRefresh(); } }

    protected void RefreshWallet()
    {
        if (walletDia != null) walletDia.text = EclipseEconomy.Diamonds.ToString("N0");
        if (walletCoin != null) walletCoin.text = EclipseEconomy.Coins.ToString("N0");
    }

    // ---------------------------------------------------------------- routing
    public void Route(string id)
    {
        onNavigate.Invoke(id);
        if (id == "Back" || id == "Home" || id == "TabHome") { EclipseScenes.Load(LobbyScene, this); return; }
        if (id == "SettingsTop") { if (TabId != "TabSettings") EclipseScenes.Load(EclipseScenes.Settings, this); return; }
        if (id.StartsWith("Tab"))
        {
            if (id != TabId) EclipseScenes.Load(EclipseScenes.FromId(id), this);
            return;
        }
        if ((id == "Diamonds" || id == "Coins") && TabId != "TabStore") { EclipseScenes.Load(EclipseScenes.Store, this); return; }
        OnRoute(id);
    }

    // ---------------------------------------------------------------- build
    [ContextMenu("Build Screen")]
    public void Build()
    {
        StopAllCoroutines();
        Transform old;
        while ((old = transform.Find(RootName)) != null)
        {
            old.gameObject.SetActive(false);
            old.SetParent(null);
            if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
        }
        built = false;
        origin.Clear();
        cyan = AccentColor;

        if (font == null)
        {
#if UNITY_2022_2_OR_NEWER
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        var go = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scale = go.GetComponent<CanvasScaler>();
        scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1672, 941);
        scale.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        frame = new GameObject("ReferenceFrame", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(go.transform, false);
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        frame.gameObject.AddComponent<EclipseSafeArea>();
        origin[frame] = Vector2.zero;

        Backdrop();
        if (UseStandardHeader) Header();
        ha = HA.Left; va = VA.Top;
        BuildContent();
        ha = HA.Center; va = VA.Bottom;
        BuildToast();
        ha = HA.Left; va = VA.Top;

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

        built = true;
        RefreshWallet();
        OnBuilt();
    }

    void Backdrop()
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

        // peredam gambar latar supaya panel tetap terbaca
        var shade = new GameObject("BackdropShade", typeof(RectTransform), typeof(Image));
        shade.transform.SetParent(cont.transform, false);
        var shr = shade.GetComponent<RectTransform>();
        shr.anchorMin = Vector2.zero; shr.anchorMax = Vector2.one; shr.offsetMin = shr.offsetMax = Vector2.zero;
        var shi = shade.GetComponent<Image>();
        shi.color = new Color(0f, 0f, 0f, ShadeAlpha);
        shi.raycastTarget = false;
    }

    // ---------------------------------------------------------------- header (tombol kembali, tab, dompet)
    // header standar: kembali | home | / JUDUL ........ koin  diamond | lonceng | gear
    void Header()
    {
        ha = HA.Left; va = VA.Top;
        HLine("HeaderBar", 0, 84, 0, new Color(0f, 0f, 0f, .5f), false);
        HLine("HeaderRule", 84, 1, 0, new Color(.3f, .31f, .33f, .45f), false);
        Color div = new Color(.45f, .46f, .48f, .5f);

        var back = R(frame, "Back", 20, 8, 96, 68);
        Icon(back, "Icon", iconBack, 48, 26, 32, 36);
        if (iconBack == null) Chevron(back, "BackChevron", 52, 44, 28, 5, Txt, 0);
        Click(back);
        S(frame, "BackDivider", 123, 22, 1, 44, div);

        var home = R(frame, "Home", 136, 8, 90, 68);
        Icon(home, "Icon", iconHome, 162, 24, 38, 40);
        if (iconHome == null) HomeShape(home, 181, 44);
        Click(home);
        S(frame, "HomeDivider", 240, 22, 1, 44, div);

        var sl = S(frame, "TitleSlash", 262, 24, 6, 40, AccentColor);
        sl.rectTransform.localEulerAngles = new Vector3(0, 0, -18);
        T(frame, "Title", PageTitle, 292, 44, 700, 36, true);

        ha = HA.Right;
        walletCoin = HeaderWallet("Coins", 1136, 169, coin, EclipseEconomy.Coins.ToString("N0"), false);
        walletDia = HeaderWallet("Diamonds", 1322, 168, gem, EclipseEconomy.Diamonds.ToString("N0"), true);
        S(frame, "HeaderDivider1", 1498, 24, 1, 44, div);

        var b = R(frame, "Notifications", 1514, 10, 60, 64);
        Icon(b, "Icon", bell, 1526, 24, 38, 40);
        if (bell == null) Envelope(b, 1545, 44);
        Img(b, "AlertBadge", 1553, 24, 12, 12, DiscSprite(), AccentColor);
        Click(b);
        S(frame, "HeaderDivider2", 1585, 24, 1, 44, div);

        var g = R(frame, "SettingsTop", 1596, 10, 52, 64);
        Icon(g, "Icon", iconGear, 1604, 24, 38, 40);
        if (iconGear == null) GearShape(g, 1623, 44);
        Click(g);
        ha = HA.Left;
    }

    protected Text HeaderWallet(string id, float x, float w, Sprite icon, string value, bool isGem)
    {
        var p = R(frame, id, x, 14, w, 60);
        if (icon != null) Icon(p, "Icon", icon, x + 6, 26, 36, 36);
        else if (isGem)
        {
            var d = Img(p, "IconShape", x + 16, 36, 16, 16, null, Ice);
            d.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
        }
        else Img(p, "IconShape", x + 12, 31, 24, 24, DiscSprite(), Gold);
        var t = T(p, "Balance", value, x + 52, 44, w - 100, 22, false);
        T(p, "Plus", "+", x + w - 46, 44, 36, 32, false, TextAnchor.MiddleCenter, false, Dim);
        Click(p);
        return t;
    }

    // tombol jajar-genjang merah tanpa panah (EQUIP, Save Settings, dll). Anak "Fill" dipakai Click().
    protected RectTransform SlantPlain(string id, string label, float x, float y, float w, float h, int size)
    {
        var r = R(frame, id, x, y, w, h);
        Img(r, "Fill", x, y, w, h, SlantSprite(), Color.white, false, true).preserveAspect = false;
        T(r, id + "Label", label, x + w * .1f, y + h / 2f, w * .8f, size, true, TextAnchor.MiddleCenter);
        Click(r);
        return r;
    }

    void Tab(string id, string label, float cx, float w, bool active)
    {
        RectTransform r;
        if (active) r = Panel(frame, id, cx - w / 2f, 22, w, 46, BrightFill, BrightLine, .6f);
        else r = R(frame, id, cx - w / 2f, 22, w, 46);
        T(r, id + "Label", label, cx - w / 2f, 45, w, 14, active, TextAnchor.MiddleCenter, false, active ? Color.white : Txt);
        Click(r);
    }

    protected Text Wallet(string id, float x, float w, Sprite icon, string value, bool isGem, float y = 22)
    {
        var p = Panel(frame, id, x, y, w, 44, new Color(.065f, .066f, .07f, .85f), new Color(.283f, .292f, .31f, .9f));
        if (icon != null) Icon(p, "Icon", icon, x + 8, y + 1, 36, 42);
        else if (isGem)
        {
            var d = Img(p, "IconShape", x + 17, y + 13, 16, 16, null, Ice);
            d.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
        }
        else Img(p, "IconShape", x + 14, y + 11, 22, 22, DiscSprite(), Gold);
        var t = T(p, "Balance", value, x + 56, y + 22, w - 100, 23, true);
        S(p, "PlusDivider", x + w - 40, y + 5, 1, 34, new Color(.272f, .28f, .297f, .5f));
        T(p, "Plus", "+", x + w - 40, y + 22, 40, 32, false, TextAnchor.MiddleCenter);
        Click(p);
        return t;
    }

    // ---------------------------------------------------------------- header bergaya referensi (Game Mode / Select Character)
    protected void RefHeader(string title, Color accent, bool mailAndGear)
    {
        ha = HA.Left; va = VA.Top;
        var back = R(frame, "Back", 24, 10, 96, 72);
        Chevron(back, "BackChevron", 46, 47, 30, 5, Txt, 0);
        Click(back);
        S(frame, "BackDivider", 123, 26, 1, 42, new Color(.45f, .46f, .48f, .55f));
        var tt = T(frame, "Title", title, 160, 50, 640, 34, true, TextAnchor.MiddleLeft);
        float sx = 160f + tt.preferredWidth + 30f;
        var s1 = S(frame, "TitleSlash0", sx, 30, 8, 34, accent); s1.rectTransform.localEulerAngles = new Vector3(0, 0, -28);
        var s2 = S(frame, "TitleSlash1", sx + 16, 30, 5, 34, new Color(accent.r * .6f, accent.g * .6f, accent.b * .6f, .9f)); s2.rectTransform.localEulerAngles = new Vector3(0, 0, -28);
        var s3 = S(frame, "TitleSlash2", sx + 31, 30, 14, 34, new Color(.55f, .56f, .58f, .75f)); s3.rectTransform.localEulerAngles = new Vector3(0, 0, -28);
        HLine("HeaderRule", 84, 1, 0, new Color(.3f, .31f, .33f, .35f), false);

        ha = HA.Right;
        Sprite cn = coin, gm = gem;
        if (mailAndGear)
        {
            Wallet("Coins", 1096, 190, cn, EclipseEconomy.Coins.ToString("N0"), false, 30);
            walletCoin = FindBalance("Coins");
            Wallet("Diamonds", 1302, 172, gm, EclipseEconomy.Diamonds.ToString("N0"), true, 30);
            walletDia = FindBalance("Diamonds");
            S(frame, "HeaderDivider1", 1496, 32, 1, 40, new Color(.45f, .46f, .48f, .5f));
            var m = R(frame, "Notifications", 1512, 26, 56, 52);
            Icon(m, "Icon", bell, 1514, 28, 52, 48);
            if (bell == null) Envelope(m, 1540, 52);
            Img(m, "AlertBadge", 1556, 30, 12, 12, DiscSprite(), new Color(.9f, .1f, .12f));
            Click(m);
            S(frame, "HeaderDivider2", 1586, 32, 1, 40, new Color(.45f, .46f, .48f, .5f));
            var g = R(frame, "Settings", 1598, 26, 48, 52);
            GearShape(g, 1622, 52);
            Click(g);
        }
        else
        {
            Wallet("Coins", 1170, 190, cn, EclipseEconomy.Coins.ToString("N0"), false, 30);
            walletCoin = FindBalance("Coins");
            Wallet("Diamonds", 1376, 172, gm, EclipseEconomy.Diamonds.ToString("N0"), true, 30);
            walletDia = FindBalance("Diamonds");
            S(frame, "HeaderDivider1", 1568, 32, 1, 40, new Color(.45f, .46f, .48f, .5f));
            var h = R(frame, "Home", 1584, 26, 60, 52);
            HomeShape(h, 1614, 52);
            Click(h);
        }
        ha = HA.Left;
    }

    Text FindBalance(string walletId)
    {
        var w = frame.Find(walletId);
        if (w == null) return null;
        var b = w.Find("Balance");
        return b != null ? b.GetComponent<Text>() : null;
    }

    // ---------------------------------------------------------------- bentuk sederhana (pengganti ikon yang belum diisi)
    protected void Envelope(Transform p, float cx, float cy)
    {
        Img(p, "EnvelopeBox", cx - 16, cy - 11, 32, 22, LineSprite(), Txt, true);
        Chevron(p, "EnvelopeFlap", cx, cy - 4, 14, 3, Txt, 2);
    }

    protected void GearShape(Transform p, float cx, float cy)
    {
        Img(p, "GearRing", cx - 12, cy - 12, 24, 24, RingSprite(), Txt);
        Img(p, "GearCore", cx - 5, cy - 5, 10, 10, RingSprite(), Txt);
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            Bar(p, "GearTooth" + i, cx + 15f * Mathf.Sin(a), cy - 15f * Mathf.Cos(a), 8, 5, 90f - i * 45f, Txt);
        }
    }

    protected void HomeShape(Transform p, float cx, float cy)
    {
        Chevron(p, "HomeRoof", cx, cy - 14, 20, 4, Txt, 3);
        Img(p, "HomeBody", cx - 13, cy - 4, 26, 18, LineSprite(), Txt, true);
        S(p, "HomeDoor", cx - 3, cy + 4, 6, 10, Txt);
    }

    // satu batang berputar di sekitar titik pusatnya
    protected Image Bar(Transform p, string n, float cx, float cy, float len, float th, float angle, Color c)
    {
        var i = S(p, n, cx - len / 2f, cy - th / 2f, len, th, c);
        i.rectTransform.pivot = new Vector2(.5f, .5f);
        i.rectTransform.anchoredPosition += new Vector2(len / 2f, -th / 2f);
        i.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
        return i;
    }

    // chevron (siku): ujung di (tipX, tipY). dir: 0 kiri, 1 kanan, 2 bawah, 3 atas
    protected void Chevron(Transform p, string n, float tipX, float tipY, float len, float th, Color c, int dir)
    {
        float theta = dir == 0 ? 0f : (dir == 2 ? 90f : (dir == 1 ? 180f : 270f));
        for (int k = 0; k < 2; k++)
        {
            float phi = theta + (k == 0 ? 45f : -45f);
            float rad = phi * Mathf.Deg2Rad;
            float cx = tipX + (len / 2f) * Mathf.Cos(rad);
            float cy = tipY - (len / 2f) * Mathf.Sin(rad);
            Bar(p, n + k, cx, cy, len, th, phi, c);
        }
    }

    // tombol jajar-genjang merah (SELECT). Nama anak "Fill" supaya dipakai Click().
    protected RectTransform SlantButton(string id, string label, float x, float y, float w, float h, int size)
    {
        var r = R(frame, id, x, y, w, h);
        Img(r, "Fill", x, y, w, h, SlantSprite(), Color.white, false, true).preserveAspect = false;
        float cy = y + h / 2f;
        T(r, id + "Label", label, x + w * .1f, cy, w * .62f, size, true, TextAnchor.MiddleCenter, true);
        float ccx = x + w * .78f;
        Chevron(r, id + "ChevA", ccx - 16, cy, 26, 6, new Color(.95f, .12f, .14f), 1);
        Chevron(r, id + "ChevB", ccx + 6, cy, 26, 6, new Color(.95f, .12f, .14f), 1);
        Click(r);
        return r;
    }
    // ---------------------------------------------------------------- toast
    void BuildToast()
    {
        var p = Panel(frame, "Toast", 546, 836, 580, 52, new Color(.05f, .05f, .055f, .97f), BrightLine);
        toastText = T(p, "Message", "", 546, 862, 580, 17, true, TextAnchor.MiddleCenter);
        toastGroup = p.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
        toastGroup.interactable = false;
    }

    protected void Toast(string msg, bool good = true)
    {
        if (toastGroup == null || !Application.isPlaying) return;
        toastText.text = msg;
        toastText.color = good ? Txt : Danger;
        if (toastCo != null) StopCoroutine(toastCo);
        toastCo = StartCoroutine(ToastRoutine());
    }

    IEnumerator ToastRoutine()
    {
        toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(1.8f);
        float t = 0f;
        while (t < .4f)
        {
            t += Time.unscaledDeltaTime;
            toastGroup.alpha = 1f - t / .4f;
            yield return null;
        }
        toastGroup.alpha = 0f;
    }

    // ---------------------------------------------------------------- helpers UI
    protected RectTransform R(Transform p, string n, float x, float y, float w, float h)
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

    protected Image Img(Transform p, string n, float x, float y, float w, float h, Sprite s, Color c, bool sliced = false, bool ray = false)
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

    protected Image S(Transform p, string n, float x, float y, float w, float h, Color c)
    {
        return Img(p, n, x, y, w, h, null, c);
    }

    protected Text T(Transform p, string n, string s, float x, float cy, float w, int size, bool bold = false,
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

    protected Text TC(Transform p, string n, string s, float x, float cy, float w, int size, Color col, bool bold = false, TextAnchor a = TextAnchor.MiddleLeft)
    {
        return T(p, n, s, x, cy, w, size, bold, a, false, col);
    }

    // teks multi-baris (dibungkus dalam kotak w x h)
    protected Text TW(Transform p, string n, string s, float x, float y, float w, float h, int size, Color col, TextAnchor a = TextAnchor.UpperLeft)
    {
        var t = R(p, n, x, y, w, h).gameObject.AddComponent<Text>();
        t.font = font;
        t.text = s;
        t.fontSize = size;
        t.color = col;
        t.alignment = a;
        t.lineSpacing = 1.15f;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    protected Image Icon(Transform p, string n, Sprite sprite, float x, float y, float w, float h)
    {
        var i = Img(p, n, x, y, w, h, sprite, Color.white);
        i.preserveAspect = true;
        i.gameObject.AddComponent<EclipseIconSlot>();
        return i;
    }

    // slot gambar (tersembunyi selama sprite kosong), digambar di atas panelnya
    protected Image Pic(Transform p, string n, float x, float y, float w, float h, Sprite s, bool keepAspect)
    {
        var i = Img(p, n, x, y, w, h, s, Color.white);
        i.type = Image.Type.Simple;
        i.preserveAspect = keepAspect;
        i.gameObject.AddComponent<EclipseIconSlot>();
        return i;
    }

    protected RectTransform Panel(Transform p, string n, float x, float y, float w, float h, Color fill, Color line, float glow = 0f)
    {
        var r = R(p, n, x, y, w, h);
        if (glow > 0f) Img(r, "Glow", x - 20, y - 20, w + 40, h + 40, GlowSprite(), new Color(glowColor.r, glowColor.g, glowColor.b, glow), true);
        Img(r, "Fill", x, y, w, h, FillSprite(), fill, true);
        Img(r, "Outline", x, y, w, h, LineSprite(), line, true);
        return r;
    }

    // ubah tampilan panel (dipakai untuk state terpilih / tidak)
    protected static void Style(RectTransform p, Color fill, Color line, float glow = 0f)
    {
        if (p == null) return;
        var f = p.Find("Fill"); if (f != null) f.GetComponent<Image>().color = fill;
        var o = p.Find("Outline"); if (o != null) o.GetComponent<Image>().color = line;
        var g = p.Find("Glow");
        if (g != null) { var im = g.GetComponent<Image>(); var c = im.color; c.a = glow; im.color = c; }
    }

    protected void Detail(Transform p, float x, float y, float w, float h)
    {
        S(p, "TopAccent", x + 22, y + 1, Mathf.Min(64, w * .24f), 2, cyan);
        S(p, "LowerTrace", x + 14, y + h - 7, w - 52, 1, new Color(.25f, .26f, .28f, .55f));
        for (int i = 0; i < 3; i++)
        {
            var t = S(p, "Slash" + i, x + w - 30 + i * 7, y + h - 13, 3, 9, new Color(.92f, .1f, .12f, .7f));
            t.rectTransform.localEulerAngles = new Vector3(0, 0, -30);
        }
    }

    protected void HLine(string n, float y, float h, float margin, Color c, bool bottom)
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

    protected void Click(RectTransform root, Action custom = null)
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
        if (custom != null) b.onClick.AddListener(() => custom());
        else b.onClick.AddListener(() => Route(id));
    }

    protected void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var g = t.GetChild(i).gameObject;
            g.SetActive(false);
            g.transform.SetParent(null);
            if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
        }
    }

    // ---------------------------------------------------------------- sprite generator (nama sama dengan lobby, jadi aset dipakai bersama)
    static Sprite sFill, sLine, sGlow, sDisc, sRing, sSlant, sFadeH;

    protected static float Sd(float x, float y, float x0, float y0, float x1, float y1, float c)
    {
        float d = Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(y - y0, y1 - y));
        float tl = ((x - x0) + (y1 - y) - c) * 0.70710678f;
        float br = ((x1 - x) + (y - y0) - c) * 0.70710678f;
        return Mathf.Min(d, Mathf.Min(tl, br));
    }

    protected static Sprite Make(string name, int w, int h, int border, Func<float, float, Color> px)
    {
#if UNITY_EDITOR
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

    protected static Sprite FillSprite()
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

    protected static Sprite LineSprite()
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

    protected static Sprite GlowSprite()
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

    protected static Sprite DiscSprite()
    {
        if (sDisc == null)
            sDisc = Make("EclipseDisc", 32, 32, 0, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 16f) * (x - 16f) + (y - 16f) * (y - 16f));
                return new Color(1, 1, 1, Mathf.Clamp01(15f - r));
            });
        return sDisc;
    }

    protected static Sprite RingSprite()
    {
        if (sRing == null)
            sRing = Make("EclipseRing", 32, 32, 0, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 16f) * (x - 16f) + (y - 16f) * (y - 16f));
                return new Color(1, 1, 1, Mathf.Clamp01(1.6f - Mathf.Abs(r - 13.5f) + .5f));
            });
        return sRing;
    }

    // gradasi horizontal: gelap di kiri, transparan di kanan
    protected static Sprite FadeHSprite()
    {
        if (sFadeH == null)
            sFadeH = Make("EclipseFadeH", 64, 4, 0, (x, y) => new Color(1, 1, 1, Mathf.Clamp01(1f - x / 64f)));
        return sFadeH;
    }

    // jajar-genjang merah bergaris terang (tombol SELECT)
    protected static Sprite SlantSprite()
    {
        if (sSlant == null)
            sSlant = Make("EclipseSlant", 512, 128, 0, (x, y) =>
            {
                const float W = 512f, H = 128f, Sk = 96f;
                float xl = Sk * (y / H);
                float xr = W - Sk * (1f - y / H);
                float cosT = H / Mathf.Sqrt(H * H + Sk * Sk);
                float dh = Mathf.Min(x - xl, xr - x) * cosT;
                float dv = Mathf.Min(y, H - y);
                float d = Mathf.Min(dh, dv);
                if (d < 0f) return new Color(0, 0, 0, 0);
                float a = Mathf.Clamp01(d + .5f);
                float t = y / H;
                // gradasi merah gelap -> merah terang, plus pita diagonal tipis
                float band = Mathf.Clamp01(1f - Mathf.Abs((x + y * .9f) - 300f) / 90f) * .18f;
                Color fill = Color.Lerp(new Color(.42f, .02f, .04f), new Color(.82f, .07f, .1f), t * .8f + band);
                Color edge = new Color(1f, .22f, .22f);
                float e = Mathf.Clamp01(1f - (d - 1.5f) / 3f);
                Color c = Color.Lerp(fill, edge, d < 5f ? e : 0f);
                c.a = a;
                return c;
            });
        return sSlant;
    }
}
