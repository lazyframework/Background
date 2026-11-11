using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Layar Masuk (Login) dan Daftar (Register) Project Eclipse.
// Satu layar dengan dua tab. Validasi form sudah ada. Hubungkan ke backend lewat event
// On Login / On Register, atau pakai "Use Local Demo Accounts" untuk uji coba tanpa server.
public class EclipseAuthBuilder : MonoBehaviour
{
    [Header("Navigasi scene")]
    public string lobbySceneName = "EclipseLobby";
    public bool loadSceneOnSuccess = true;
    [Tooltip("Nama tampilan pemain disimpan di PlayerPrefs dengan kunci ini (lobby bisa membacanya).")]
    public string playerNamePrefsKey = "EclipsePlayerName";

    [Header("Opsional: gambar sendiri (kosong = placeholder)")]
    public Font font;
    public Sprite background;
    public Sprite logo;

    [Header("Ikon (opsional, kosong = tidak ditampilkan)")]
    public Sprite iconUser, iconUserAdd, iconMail, iconLock, iconEye, iconEyeOff, iconGoogle, iconDiscord, iconBack;

    [Header("Tombol Back to Menu")]
    [Tooltip("Scene tujuan tombol Back to Menu. Kosong = hanya memanggil event On Back.")]
    public string backSceneName = "";
    public UnityEvent onBack = new UnityEvent();

    [Header("Teks")]
    public string versionText = "v1.0.0";
    public int minPasswordLength = 6;
    public int minUsernameLength = 3;
    public bool startOnRegister = false;

    [Header("Akun uji coba lokal (HANYA untuk demo, bukan untuk produksi)")]
    public bool useLocalDemoAccounts = true;

    [Header("Mode server (diatur otomatis oleh EclipseFirebaseAuth)")]
    [Tooltip("Login hanya dengan email (Firebase tidak mendukung login username).")]
    public bool loginWithEmailOnly = false;
    [Tooltip("Tombol Guest ditangani skrip lain (mis. Firebase anonymous).")]
    public bool handleGuestExternally = false;

    [Header("Login sosial (dihubungkan ke EclipseSocialLogin)")]
    public UnityEvent onGoogle = new UnityEvent();
    public UnityEvent onFacebook = new UnityEvent();
    public UnityEvent onDiscord = new UnityEvent();

    [Serializable] public class LoginEvent : UnityEvent<string, string> { }
    [Serializable] public class RegisterEvent : UnityEvent<string, string, string> { }
    [Serializable] public class StringEvent : UnityEvent<string> { }
    [Tooltip("(email/username, password) setelah lolos validasi.")]
    public LoginEvent onLogin = new LoginEvent();
    [Tooltip("(username, email, password) setelah lolos validasi.")]
    public RegisterEvent onRegister = new RegisterEvent();
    public UnityEvent onGuest = new UnityEvent();
    public UnityEvent onForgotPassword = new UnityEvent();
    [Tooltip("Dipanggil dengan nama tampilan saat masuk/daftar berhasil.")]
    public StringEvent onAuthSuccess = new StringEvent();

    public Color accent = new Color(.92f, .1f, .12f, 1);

    public enum BackdropMode { Stretch, Cover }
    [Header("Latar layar penuh")]
    public BackdropMode backdropMode = BackdropMode.Stretch;

    const string Root = "EclipseAuthCanvas_v1";

    RectTransform frame;
    readonly Dictionary<Transform, Vector2> origin = new Dictionary<Transform, Vector2>();
    bool registerMode;
    bool onLanding;
    bool showMore;              // popup More sedang terbuka
    RectTransform landingRoot;

    enum HA { Left, Center, Right }
    enum VA { Top, Center, Bottom }
    HA ha = HA.Center;
    VA va = VA.Center;

    static Sprite sFill, sLine, sGlow;

    static readonly Color Txt = new Color(.952f, .938f, .914f);
    static readonly Color Dim = new Color(.613f, .604f, .589f);
    static readonly Color PanelFill = new Color(.083f, .085f, .09f, .90f);
    static readonly Color PanelLine = new Color(.307f, .316f, .335f, .95f);
    static readonly Color BrightFill = new Color(.72f, .04f, .08f, .98f);
    static readonly Color BrightLine = new Color(.95f, .17f, .19f, 1f);
    static readonly Color FieldFill = new Color(.04f, .042f, .048f, .9f);
    static readonly Color FieldLine = new Color(.26f, .27f, .3f, .9f);
    static readonly Color ErrorColor = new Color(1f, .42f, .42f);
    static readonly Color OkColor = new Color(.45f, .9f, .6f);

    // ---------------------------------------------------------------- lifecycle

    void Start()
    {
        var root = transform.Find(Root);
        if (root == null) Build();
        else
        {
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                string id = b.gameObject.name;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => Route(id));
            }
        }
        registerMode = startOnRegister;
        onLanding = false;
        if (Application.isPlaying) RestoreRemembered();
        Refresh();
    }

#if UNITY_EDITOR
    void Reset()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && transform.Find(Root) == null) Build(); };
    }
#endif

    // ---------------------------------------------------------------- state

    public string LoginIdentityText { get { return Val("LoginIdentity"); } }
    public void ShowLogin() { registerMode = false; onLanding = false; Refresh(); }
    public void ShowRegister() { registerMode = true; onLanding = false; Refresh(); }
    public void ShowLanding() { ShowLogin(); }

    Transform FindDeep(string n)
    {
        var root = transform.Find(Root);
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    void SetText(string n, string v)
    {
        var t = FindDeep(n);
        if (t == null) return;
        var tx = t.GetComponent<Text>();
        if (tx != null) tx.text = v;
    }

    InputField GetInput(string n)
    {
        var t = FindDeep(n);
        return t != null ? t.GetComponent<InputField>() : null;
    }

    string Val(string n)
    {
        var f = GetInput(n);
        return f != null ? f.text : "";
    }

    bool Checked(string n)
    {
        var t = FindDeep(n);
        var tg = t != null ? t.GetComponent<Toggle>() : null;
        return tg != null && tg.isOn;
    }

    public void Refresh()
    {
        if (transform.Find(Root) == null) return;
        var lf = FindDeep("LoginForm");
        var rf = FindDeep("RegisterForm");
        if (lf != null) lf.gameObject.SetActive(!registerMode);
        if (rf != null) rf.gameObject.SetActive(registerMode);

        SetActiveByName("AuthCard", !onLanding);
        SetActiveByName("Landing", onLanding);
        SetActiveByName("MorePanel", onLanding && showMore);
        SetActiveByName("LandingTitle", logo == null);

        SetText("AuthTitle", registerMode ? "REGISTER" : "LOGIN");
        SetText("AuthSubtitle", registerMode ? "Create your agent account." : "Sign in to continue your mission.");
        SetText("AuthVersion", versionText);
        SetText("LoginIdentityPlaceholder", loginWithEmailOnly ? "Email Address" : "Email or Username");
        SetTab("TabLogin", !registerMode);
        SetTab("TabRegister", registerMode);

        foreach (var n in new[] { "Logo", "LandingLogo" })
        {
            var lg = FindDeep(n);
            var im = lg != null ? lg.GetComponent<Image>() : null;
            if (im != null && im.sprite != logo) { im.sprite = logo; im.enabled = logo != null; }
        }
    }

    void SetActiveByName(string n, bool on)
    {
        var t = FindDeep(n);
        if (t != null) t.gameObject.SetActive(on);
    }

    void SetTab(string id, bool active)
    {
        var t = FindDeep(id);
        if (t == null) return;
        var f = t.Find("Fill");
        var o = t.Find("Outline");
        if (f != null) f.GetComponent<Image>().color = active ? BrightFill : new Color(.04f, .042f, .05f, .9f);
        if (o != null) o.GetComponent<Image>().color = active ? BrightLine : new Color(.25f, .26f, .29f, .7f);
        var g = t.Find("Glow"); if (g != null) g.gameObject.SetActive(active);
        var al = t.Find("ActiveLine"); if (al != null) al.gameObject.SetActive(active);
        var l = t.Find(id + "Label");
        if (l != null) l.GetComponent<Text>().color = active ? Color.white : new Color(.85f, .86f, .89f);
    }

    // ---------------------------------------------------------------- routing

    public void Route(string id)
    {
        if (id == "TabLogin") { ShowLogin(); ClearMessages(); }
        else if (id == "TabRegister") { ShowRegister(); ClearMessages(); }
        else if (id == "LoginSubmit") SubmitLogin();
        else if (id == "RegisterSubmit") SubmitRegister();
        else if (id == "Guest") { onGuest.Invoke(); if (!handleGuestExternally) Success("Guest"); }
        else if (id == "Forgot") onForgotPassword.Invoke();
        else if (id == "ShowLoginPassword") TogglePassword("LoginPassword", "ShowLoginPassword");
        else if (id == "ShowRegPassword") TogglePassword("RegPassword", "ShowRegPassword");
        else if (id == "ShowRegConfirm") TogglePassword("RegConfirm", "ShowRegConfirm");
        else if (id == "Google") onGoogle.Invoke();
        else if (id == "Facebook") onFacebook.Invoke();
        else if (id == "Discord") onDiscord.Invoke();
        else if (id == "GoRegister") { ShowRegister(); ClearMessages(); }
        else if (id == "GoLogin") { ShowLogin(); ClearMessages(); }
        else if (id == "Back") GoBack();
    }

    void GoBack()
    {
        onBack.Invoke();
        if (!string.IsNullOrEmpty(backSceneName)) EclipseScenes.Load(backSceneName, this);
    }

    void TogglePassword(string field, string button)
    {
        var f = GetInput(field);
        if (f == null) return;
        bool hide = f.contentType != InputField.ContentType.Password;
        f.contentType = hide ? InputField.ContentType.Password : InputField.ContentType.Standard;
        f.ForceLabelUpdate();
        var ic = FindDeep(button + "Icon");
        var im = ic != null ? ic.GetComponent<Image>() : null;
        if (im != null)
        {
            var s = hide ? iconEyeOff : iconEye;
            if (s != null) im.sprite = s;
        }
    }

    // ---------------------------------------------------------------- pesan

    public void ShowError(string msg) { Message(msg, true); }
    public void ShowInfo(string msg) { Message(msg, false); }

    void Message(string msg, bool error)
    {
        foreach (var n in new[] { "LoginMessage", "RegisterMessage", "LandingMessage" })
        {
            var t = FindDeep(n);
            if (t == null) continue;
            var tx = t.GetComponent<Text>();
            if (tx == null) continue;
            tx.text = msg;
            tx.color = error ? ErrorColor : OkColor;
        }
    }

    void ClearMessages() { Message("", false); }

    // ---------------------------------------------------------------- validasi + submit

    static bool ValidEmail(string e)
    {
        return Regex.IsMatch(e ?? "", @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    void SubmitLogin()
    {
        string id = Val("LoginIdentity").Trim();
        string pw = Val("LoginPassword");
        if (id.Length == 0) { ShowError("Enter your email or username."); return; }
        if (pw.Length == 0) { ShowError("Enter your password."); return; }
        if (loginWithEmailOnly && !ValidEmail(id)) { ShowError("Enter your email address."); return; }

        RememberIdentity(id);
        onLogin.Invoke(id, pw);
        if (useLocalDemoAccounts) LocalLogin(id, pw);
    }

    void SubmitRegister()
    {
        string user = Val("RegUsername").Trim();
        string mail = Val("RegEmail").Trim();
        string pw = Val("RegPassword");
        string pw2 = Val("RegConfirm");

        if (user.Length < minUsernameLength) { ShowError("Username must be at least " + minUsernameLength + " characters."); return; }
        if (!Regex.IsMatch(user, @"^[A-Za-z0-9_\-\.]+$")) { ShowError("Username may only contain letters, numbers, _ - ."); return; }
        if (!ValidEmail(mail)) { ShowError("Enter a valid email address."); return; }
        if (pw.Length < minPasswordLength) { ShowError("Password must be at least " + minPasswordLength + " characters."); return; }
        if (pw != pw2) { ShowError("Passwords do not match."); return; }
        if (!Checked("AcceptTerms")) { ShowError("Please accept the Terms of Service."); return; }

        onRegister.Invoke(user, mail, pw);
        if (useLocalDemoAccounts) LocalRegister(user, mail, pw);
    }

    // ---------------------------------------------------------------- akun lokal (demo)

    static string KeyUser(string u) { return "eclipse_user_" + u.Trim().ToLowerInvariant(); }
    static string KeyMail(string m) { return "eclipse_mail_" + m.Trim().ToLowerInvariant(); }

    static string NewSalt()
    {
        var b = new byte[16];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(b);
        return Convert.ToBase64String(b);
    }

    static string Hash(string pass, string salt)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(salt + pass)));
    }

    void LocalRegister(string user, string mail, string pw)
    {
        if (PlayerPrefs.HasKey(KeyUser(user))) { ShowError("Username is already taken."); return; }
        if (PlayerPrefs.HasKey(KeyMail(mail))) { ShowError("Email is already registered."); return; }
        string salt = NewSalt();
        PlayerPrefs.SetString(KeyUser(user), salt + "|" + Hash(pw, salt) + "|" + mail + "|" + user);
        PlayerPrefs.SetString(KeyMail(mail), user);
        PlayerPrefs.Save();
        Success(user);
    }

    void LocalLogin(string id, string pw)
    {
        string user = id;
        if (id.Contains("@"))
        {
            if (!PlayerPrefs.HasKey(KeyMail(id))) { ShowError("Account not found."); return; }
            user = PlayerPrefs.GetString(KeyMail(id));
        }
        if (!PlayerPrefs.HasKey(KeyUser(user))) { ShowError("Account not found."); return; }
        var parts = PlayerPrefs.GetString(KeyUser(user)).Split('|');
        if (parts.Length < 4 || Hash(pw, parts[0]) != parts[1]) { ShowError("Wrong password."); return; }
        Success(parts[3]);
    }

    // ---------------------------------------------------------------- sukses / ingat saya

    // Panggil ini dari backend-mu saat masuk/daftar berhasil.
    public void Success(string displayName)
    {
        if (!string.IsNullOrEmpty(playerNamePrefsKey)) PlayerPrefs.SetString(playerNamePrefsKey, displayName);
        PlayerPrefs.Save();
        ShowInfo("Welcome, " + displayName + "!");
        onAuthSuccess.Invoke(displayName);
        if (loadSceneOnSuccess && !string.IsNullOrEmpty(lobbySceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(lobbySceneName)) SceneManager.LoadScene(lobbySceneName);
            else Debug.LogError("Scene '" + lobbySceneName + "' belum ada di File > Build Profiles > Scene List.", this);
        }
    }

    void RememberIdentity(string id)
    {
        if (Checked("RememberMe")) PlayerPrefs.SetString("eclipse_last_identity", id);
        else PlayerPrefs.DeleteKey("eclipse_last_identity");
    }

    void RestoreRemembered()
    {
        if (!PlayerPrefs.HasKey("eclipse_last_identity")) return;
        var f = GetInput("LoginIdentity");
        if (f != null) f.text = PlayerPrefs.GetString("eclipse_last_identity");
        var t = FindDeep("RememberMe");
        var tg = t != null ? t.GetComponent<Toggle>() : null;
        if (tg != null) tg.isOn = true;
    }

    // ---------------------------------------------------------------- helpers

    RectTransform NewRT(Transform p, string n)
    {
        var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(p, false);
        return rt;
    }

    RectTransform R(Transform p, string n, float x, float y, float w, float h)
    {
        var r = NewRT(p, n);
        Vector2 o;
        origin.TryGetValue(p, out o);
        r.pivot = new Vector2(0, 1);
        if (p == frame || p == landingRoot)
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

    Image S(Transform p, string n, float x, float y, float w, float h, Color c) { return Img(p, n, x, y, w, h, null, c); }

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

    RectTransform Panel(Transform p, string n, float x, float y, float w, float h, Color fill, Color line, float glow = 0)
    {
        var r = R(p, n, x, y, w, h);
        if (glow > 0) Img(r, "Glow", x - 20, y - 20, w + 40, h + 40, GlowSprite(), new Color(.9f, .08f, .1f, glow), true);
        Img(r, "Fill", x, y, w, h, FillSprite(), fill, true);
        Img(r, "Outline", x, y, w, h, LineSprite(), line, true);
        return r;
    }

    void Click(RectTransform root)
    {
        Graphic target = null;
        var outline = root.Find("Outline");
        if (outline != null) target = outline.GetComponent<Graphic>();
        var fill = root.Find("Fill");
        if (fill != null) fill.GetComponent<Graphic>().raycastTarget = true;
        else
        {
            var hit = NewRT(root, "Hit");
            hit.anchorMin = Vector2.zero; hit.anchorMax = Vector2.one; hit.offsetMin = hit.offsetMax = Vector2.zero;
            var im = hit.gameObject.AddComponent<Image>();
            im.color = new Color(0, 0, 0, 0);
            im.raycastTarget = true;
            if (target == null) target = root.GetComponentInChildren<Text>();
            if (target == null) target = im;
        }
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

    Image Icon(Transform p, string n, Sprite s, float x, float y, float w, float h)
    {
        var i = Img(p, n, x, y, w, h, s, Color.white);
        i.preserveAspect = true;
        i.gameObject.AddComponent<EclipseIconSlot>();
        return i;
    }

    // kolom input (legacy InputField): ikon + garis pemisah di kiri, tombol mata di kanan (password)
    InputField Field(Transform p, string id, float x, float y, float w, float h, string placeholder, bool password, string showButtonId, Sprite leftIcon)
    {
        var root = Panel(p, id, x, y, w, h, FieldFill, FieldLine);
        root.Find("Fill").GetComponent<Graphic>().raycastTarget = true;
        float left = 94, right = password ? 96 : 20;
        int fs = h >= 68 ? 24 : 21;

        float ih = Mathf.Min(40, h - 24);
        Icon(root, id + "Icon", leftIcon, x + 18, y + (h - ih) / 2f, 40, ih);
        S(root, id + "Divider", x + 78, y + 16, 1, h - 32, new Color(.4f, .41f, .44f, .5f));

        var text = T(root, id + "Text", "", x + left, y + h / 2f, w - left - right, fs, false, TextAnchor.MiddleLeft);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.supportRichText = false;
        var ph = T(root, id + "Placeholder", placeholder, x + left, y + h / 2f, w - left - right, fs, false, TextAnchor.MiddleLeft, false, new Color(.62f, .62f, .65f, .85f));
        ph.horizontalOverflow = HorizontalWrapMode.Wrap;
        ph.verticalOverflow = VerticalWrapMode.Truncate;

        var inp = root.gameObject.AddComponent<InputField>();
        inp.targetGraphic = root.Find("Fill").GetComponent<Graphic>();
        inp.transition = Selectable.Transition.None;
        inp.textComponent = text;
        inp.placeholder = ph;
        inp.lineType = InputField.LineType.SingleLine;
        inp.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
        inp.caretColor = Color.white;
        inp.selectionColor = new Color(.92f, .1f, .12f, .45f);

        if (password && showButtonId != null)
        {
            var b = R(root, showButtonId, x + w - 84, y + 8, 68, h - 16);
            Icon(b, showButtonId + "Icon", iconEyeOff, x + w - 78, y + (h - 34) / 2f, 46, 34);
            Click(b);
        }
        return inp;
    }

    // kotak centang
    void Check(Transform p, string id, float x, float y, string label, float size = 29, int fs = 21)
    {
        var r = R(p, id, x, y, size, size);
        var bg = Img(r, id + "Box", x, y, size, size, FillSprite(), FieldFill, true, true);
        Img(r, id + "BoxLine", x, y, size, size, LineSprite(), new Color(.6f, .61f, .65f, .9f), true);
        var mark = Img(r, id + "Mark", x + 7, y + 7, size - 14, size - 14, null, accent);
        var tg = r.gameObject.AddComponent<Toggle>();
        tg.targetGraphic = bg;
        tg.graphic = mark;
        tg.isOn = false;
        T(p, id + "Label", label, x + size + 14, y + size / 2f, 520, fs, false, TextAnchor.MiddleLeft, false, Txt);
    }

    // ---------------------------------------------------------------- latar & sprite

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

        // peredam supaya latar tetap terbaca di belakang kartu
        var dim = new GameObject("BackdropDim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(cont.transform, false);
        var dr = dim.GetComponent<RectTransform>();
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;
        var di = dim.GetComponent<Image>();
        di.color = new Color(.038f, .039f, .041f, .12f);
        di.raycastTarget = false;
    }

    static float Sd(float x, float y, float x0, float y0, float x1, float y1, float c)
    {
        float d = Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(y - y0, y1 - y));
        float tl = ((x - x0) + (y1 - y) - c) * 0.70710678f;
        float br = ((x1 - x) + (y - y0) - c) * 0.70710678f;
        return Mathf.Min(d, Mathf.Min(tl, br));
    }

    static Sprite Make(string name, int w, int h, int border, Func<float, float, Color> px)
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

    // ---------------------------------------------------------------- build

    [ContextMenu("Build Login Register")]
    public void Build()
    {
        Transform old;
        while ((old = transform.Find(Root)) != null)
        {
            old.gameObject.SetActive(false);
            old.SetParent(null);
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
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
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
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

        BackdropStretch();
        BuildCard();
        BuildBackButton();

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

        registerMode = startOnRegister;
        onLanding = false;
        showMore = false;
        Refresh();
    }

    void BuildCard()
    {
        ha = HA.Right; va = VA.Center;
        var card = R(frame, "AuthCard", 830, 167, 705, 700);

        var body = Panel(card, "CardBody", 830, 243, 705, 590, new Color(.035f, .036f, .042f, .80f), new Color(.22f, .23f, .26f, .7f));
        foreach (var g in body.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;

        var t1 = Panel(card, "TabLogin", 830, 167, 354, 76, BrightFill, BrightLine, .5f);
        S(t1, "ActiveLine", 885, 240, 244, 3, new Color(1f, .42f, .42f));
        Icon(t1, "Icon", iconUser, 940, 185, 38, 40);
        T(t1, "TabLoginLabel", "Login", 1002, 205, 150, 34, true);
        Click(t1);

        var t2 = Panel(card, "TabRegister", 1185, 167, 350, 76, BrightFill, BrightLine, .5f);
        S(t2, "ActiveLine", 1240, 240, 240, 3, new Color(1f, .42f, .42f));
        Icon(t2, "Icon", iconUserAdd, 1258, 185, 48, 40);
        T(t2, "TabRegisterLabel", "Register", 1329, 205, 190, 34, true);
        Click(t2);

        BuildLogin(card);
        BuildRegister(card);

        ha = HA.Right; va = VA.Bottom;
        T(frame, "AuthVersion", "", 1135, 920, 400, 12, false, TextAnchor.MiddleRight, false, Dim);
    }

    // tombol "Back to Menu" di kiri bawah
    void BuildBackButton()
    {
        ha = HA.Left; va = VA.Bottom;
        var b = Panel(frame, "Back", 0, 852, 242, 58, new Color(.045f, .047f, .053f, .82f), new Color(.2f, .21f, .24f, .6f));
        Icon(b, "Icon", iconBack, 36, 866, 32, 30);
        T(b, "BackLabel", "Back to Menu", 90, 881, 140, 21);
        Click(b);
    }

    void BuildLogin(RectTransform card)
    {
        var f = R(card, "LoginForm", 830, 243, 705, 590);

        Field(f, "LoginIdentity", 852, 291, 661, 71, "Email Address", false, null, iconMail);
        Field(f, "LoginPassword", 852, 388, 661, 71, "Password", true, "ShowLoginPassword", iconLock);

        Check(f, "RememberMe", 853, 481, "Remember Me");
        var fg = R(f, "Forgot", 1303, 478, 210, 34);
        T(fg, "ForgotLabel", "Forgot Password?", 1303, 495, 210, 21, false, TextAnchor.MiddleRight);
        S(fg, "ForgotUnderline", 1349, 508, 164, 1, Txt);
        Click(fg);

        T(f, "LoginMessage", "", 852, 524, 661, 15, false, TextAnchor.MiddleCenter, false, ErrorColor);

        BigButton(f, "LoginSubmit", "LOGIN", 912, 540, 545, 72, 40);

        S(f, "OrLineL", 852, 653, 298, 1, new Color(.4f, .41f, .44f, .5f));
        T(f, "OrText", "OR", 1150, 653, 72, 20, false, TextAnchor.MiddleCenter, false, new Color(.82f, .83f, .86f));
        S(f, "OrLineR", 1222, 653, 291, 1, new Color(.4f, .41f, .44f, .5f));

        SocialButton(f, "Google", "Continue with Google", iconGoogle, 860, 684, 319, 69, 44, 103);
        SocialButton(f, "Discord", "Continue with Discord", iconDiscord, 1194, 684, 319, 69, 40, 102);

        T(f, "NoAccountText", "Don't have an account?", 1000, 801, 244, 22, false, TextAnchor.MiddleRight);
        var g = R(f, "GoRegister", 1250, 786, 100, 30);
        T(g, "GoRegisterLabel", "Register", 1256, 801, 100, 22, false, TextAnchor.MiddleLeft, false, accent);
        Click(g);
    }

    void BuildRegister(RectTransform card)
    {
        var f = R(card, "RegisterForm", 830, 243, 705, 590);

        Field(f, "RegUsername", 852, 271, 661, 64, "Choose a username", false, null, iconUser);
        Field(f, "RegEmail", 852, 349, 661, 64, "Email Address", false, null, iconMail);
        Field(f, "RegPassword", 852, 427, 661, 64, "Create a password", true, "ShowRegPassword", iconLock);
        Field(f, "RegConfirm", 852, 505, 661, 64, "Repeat your password", true, "ShowRegConfirm", iconLock);

        Check(f, "AcceptTerms", 853, 590, "I agree to the Terms of Service and Privacy Policy", 29, 19);

        T(f, "RegisterMessage", "", 852, 640, 661, 15, false, TextAnchor.MiddleCenter, false, ErrorColor);

        BigButton(f, "RegisterSubmit", "CREATE ACCOUNT", 912, 656, 545, 72, 36);

        T(f, "HaveAccountText", "Already have an account?", 1000, 774, 258, 22, false, TextAnchor.MiddleRight);
        var g = R(f, "GoLogin", 1264, 759, 80, 30);
        T(g, "GoLoginLabel", "Login", 1270, 774, 80, 22, false, TextAnchor.MiddleLeft, false, accent);
        Click(g);
    }

    // tombol merah besar bergaya miring dengan tanda panah
    void BigButton(Transform p, string id, string label, float x, float y, float w, float h, int size)
    {
        var b = Panel(p, id, x, y, w, h, BrightFill, BrightLine, .6f);
        Img(b, "InnerFrame", x + 5, y + 5, w - 10, h - 10, LineSprite(), new Color(1f, .35f, .35f, .35f), true);
        for (int i = 0; i < 2; i++)
        {
            var s = S(b, "Slash" + i, x + 14 + i * 20, y + 8, i == 0 ? 12 : 6, h - 14, new Color(1f, .27f, .27f));
            s.rectTransform.localEulerAngles = new Vector3(0, 0, -20);
        }
        T(b, id + "Label", label, x, y + h / 2f, w, size, true, TextAnchor.MiddleCenter);
        T(b, "Chevron", ">", x + w - 82, y + h / 2f, 36, 36, true, TextAnchor.MiddleCenter);
        Click(b);
    }

    void SocialButton(Transform p, string id, string label, Sprite icon, float x, float y, float w, float h, float iconDx, float labelDx)
    {
        var g = Panel(p, id, x, y, w, h, new Color(.045f, .047f, .053f, .9f), new Color(.3f, .31f, .34f, .9f));
        Icon(g, "Icon", icon, x + iconDx, y + (h - 42) / 2f, 42, 42);
        T(g, id + "Label", label, x + labelDx, y + h / 2f, w - labelDx - 8, 21);
        Click(g);
    }
}
