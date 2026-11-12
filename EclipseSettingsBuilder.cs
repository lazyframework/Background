using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// Layar SETTINGS: kategori di kiri; kanan menampilkan Graphics, Audio, Controls dan Gameplay sekaligus
// (Language dan About punya halaman sendiri). Nilai disimpan lewat EclipseSettings dan langsung diterapkan.
public class EclipseSettingsBuilder : EclipseScreenBase
{
    [Header("Akun")]
    public string versionText = "v1.0.0     |     FOR A BETTER TOMORROW";
    public bool loadLoginOnSignOut = true;
    public UnityEvent onSignOut = new UnityEvent();
    [Tooltip("Dibuka tombol Privacy Policy. Kosong = hanya menampilkan pesan.")]
    public string privacyUrl = "";

    [Header("Bahasa (nama yang tampil di halaman Language)")]
    public string[] languages = { "English", "Bahasa Indonesia", "Espanol", "Francais" };

    [Header("Gambar pratinjau grafis (opsional)")]
    public Sprite graphicsPreview;

    [Header("Ikon (opsional, kosong = tidak ditampilkan)")]
    public Sprite iconGraphics, iconAudio, iconControls, iconGameplay, iconLanguage, iconAbout;
    public Sprite iconVolume, iconMusic, iconSfx, iconMic, iconMute, iconVoiceChat, iconBgMusic, iconVibration;
    public Sprite iconReset, iconPrivacy, iconCheck;

    static readonly string[] Cats = { "Graphics", "Audio", "Controls", "Gameplay", "Language", "About" };
    static readonly Color Dark = new Color(.04f, .042f, .05f, .9f);
    static readonly Color DarkLine = new Color(.25f, .26f, .29f, .9f);
    static readonly Color SecFill = new Color(.03f, .032f, .038f, .84f);
    static readonly Color SecLine = new Color(.15f, .16f, .18f, .6f);
    static readonly Color RowRule = new Color(.2f, .205f, .22f, .55f);

    int cat;
    readonly RectTransform[] catBtns = new RectTransform[6];
    readonly Image[] catIcons = new Image[6];
    readonly Text[] catLabels = new Text[6];
    RectTransform content;

    protected override string RootName { get { return "EclipseSettingsCanvas_v1"; } }
    protected override string TabId { get { return "TabSettings"; } }
    protected override string PageTitle { get { return "SETTINGS"; } }
    protected override string PageSubtitle { get { return ""; } }

    Sprite CatIcon(int i)
    {
        switch (i)
        {
            case 0: return iconGraphics;
            case 1: return iconAudio;
            case 2: return iconControls;
            case 3: return iconGameplay;
            case 4: return iconLanguage;
        }
        return iconAbout;
    }

    protected override void BuildContent()
    {
        // Seluruh blok (kategori + isi) dipusatkan, jadi tetap rapi di rasio layar apa pun.
        // Tombol bawah menempel ke bawah layar; header tetap menempel ke tepi kiri/kanan.
        ha = HA.Center; va = VA.Top;
        for (int i = 0; i < Cats.Length; i++)
        {
            float y = 113 + i * 83;
            var b = Panel(frame, "Cat" + i, 22, y, 250, 82, Dark, DarkLine, .001f);
            catIcons[i] = Icon(b, "Icon", CatIcon(i), 54, y + 19, 44, 44);
            catLabels[i] = T(b, "Label", Cats[i], 120, y + 41, 140, 22);
            Click(b);
            catBtns[i] = b;
        }
        content = R(frame, "Content", 305, 112, 1340, 720);

        va = VA.Bottom;
        var rs = Panel(frame, "ResetSettings", 23, 861, 257, 57, Dark, DarkLine);
        Icon(rs, "Icon", iconReset, 55, 874, 34, 34);
        T(rs, "Label", "Reset to Default", 109, 889, 160, 21);
        Click(rs);
        var pp = Panel(frame, "PrivacyPolicy", 315, 861, 243, 57, Dark, DarkLine);
        Icon(pp, "Icon", iconPrivacy, 347, 874, 34, 34);
        T(pp, "Label", "Privacy Policy", 399, 889, 150, 21);
        Click(pp);

        var sv = SlantPlain("SaveSettings", "Save Settings", 1345, 860, 305, 60, 22);
        Icon(sv, "Icon", iconCheck, 1424, 875, 30, 30);
        var svl = sv.Find("SaveSettingsLabel") as RectTransform;
        if (svl != null) svl.anchoredPosition += new Vector2(28f, 0f);
        ha = HA.Left;
    }

    protected override void OnBuilt() { ShowCat(cat); }

    protected override void OnRoute(string id)
    {
        int i;
        if (id.StartsWith("Cat") && int.TryParse(id.Substring(3), out i)) ShowCat(i);
        else if (id == "ResetSettings") { EclipseSettings.ResetAll(); ShowCat(cat); Toast("Settings restored to default."); }
        else if (id == "PrivacyPolicy")
        {
            if (!string.IsNullOrEmpty(privacyUrl)) Application.OpenURL(privacyUrl);
            else Toast("Privacy policy link is not set.", false);
        }
        else if (id == "SaveSettings") { PlayerPrefs.Save(); Toast("Settings saved."); }
    }

    void ShowCat(int i)
    {
        if (content == null) return;
        cat = Mathf.Clamp(i, 0, Cats.Length - 1);
        for (int k = 0; k < catBtns.Length; k++)
        {
            bool on = k == cat;
            Style(catBtns[k], on ? BrightFill : Dark, on ? BrightLine : DarkLine, on ? .5f : 0f);
            catIcons[k].color = on ? Color.white : new Color(.75f, .76f, .8f);
            catLabels[k].color = on ? Color.white : new Color(.85f, .86f, .89f);
        }
        ClearChildren(content);
        if (cat == 4) BuildLanguage();
        else if (cat == 5) BuildAbout();
        else BuildOverview();
    }

    // ---------------------------------------------------------------- halaman
    void BuildOverview()
    {
        // ---- Graphics
        Section("Graphics", 305, 112, 1340, 306, null);
        RowLabel("Graphics Quality", 357, 187, 21);
        Seg(new[] { "Low", "Medium", "High", "Ultra" }, 581, 187, 488, Mathf.Clamp(EclipseSettings.Quality, 0, 3), v => EclipseSettings.Quality = v);
        HRule(322, 212, 787);
        RowLabel("Frame Rate", 357, 239, 21);
        Seg(new[] { "30 FPS", "45 FPS", "60 FPS" }, 581, 239, 488, FpsIndex(EclipseSettings.Fps), v => EclipseSettings.Fps = FpsValue(v));
        HRule(322, 264, 787);
        RowLabel("Resolution Scale", 357, 291, 21);
        SliderRow(592, 291, 378, (EclipseSettings.ResolutionScale - .5f) / .75f, v => EclipseSettings.ResolutionScale = .5f + .75f * v,
               v => Mathf.RoundToInt((.5f + .75f * v) * 100f) + "%", 988);
        HRule(322, 315, 787);
        RowLabel("Shadow Quality", 357, 340, 21);
        Seg(new[] { "Low", "Medium", "High" }, 592, 340, 476, Mathf.Clamp(EclipseSettings.ShadowDetail, 0, 2), v => EclipseSettings.ShadowDetail = v);
        HRule(322, 364, 787);
        RowLabel("Anti-Aliasing", 357, 389, 21);
        Seg(new[] { "Off", "FXAA", "TAA" }, 592, 389, 476, Mathf.Clamp(EclipseSettings.AntiAliasing, 0, 2), v => EclipseSettings.AntiAliasing = v);

        Panel(content, "PreviewBack", 1131, 154, 496, 254, new Color(.05f, .052f, .06f, .95f), new Color(.3f, .31f, .34f, .9f));
        Pic(content, "Preview", 1133, 156, 492, 250, graphicsPreview, false);
        Img(content, "CaptionBack", 1133, 356, 492, 50, FillSprite(), new Color(.02f, .02f, .025f, .82f), true);
        TW(content, "Caption", "Higher graphics for better visuals,\nbut may affect performance.", 1133, 360, 492, 46, 17, Txt, TextAnchor.UpperCenter);

        // ---- Audio
        Section("Audio", 305, 436, 1340, 216, null);
        Sprite[] ai = { iconVolume, iconMusic, iconSfx, iconMic };
        string[] an = { "Master Volume", "Music Volume", "SFX Volume", "Voice Volume" };
        float[] cy = { 505, 545, 584, 624 };
        Action<float>[] aset =
        {
            v => EclipseSettings.Master = v, v => EclipseSettings.Music = v, v => EclipseSettings.Sfx = v, v => EclipseSettings.Voice = v
        };
        float[] av = { EclipseSettings.Master, EclipseSettings.Music, EclipseSettings.Sfx, EclipseSettings.Voice };
        for (int i = 0; i < 4; i++)
        {
            Icon(content, "AIcon" + i, ai[i], 356, cy[i] - 14, 28, 28);
            RowLabel(an[i], 404, cy[i], 21);
            SliderRow(592, cy[i], 436, av[i], aset[i], v => Mathf.RoundToInt(v * 100f) + "%", 1050);
        }
        Sprite[] ti = { iconMute, iconVoiceChat, iconBgMusic, iconVibration };
        string[] tn = { "Mute All", "Voice Chat", "Background Music", "Vibration" };
        bool[] tv = { EclipseSettings.MuteAll, EclipseSettings.VoiceChat, EclipseSettings.BackgroundMusic, EclipseSettings.Vibration };
        Action<bool>[] tset =
        {
            v => EclipseSettings.MuteAll = v, v => EclipseSettings.VoiceChat = v, v => EclipseSettings.BackgroundMusic = v, v => EclipseSettings.Vibration = v
        };
        for (int i = 0; i < 4; i++)
        {
            Icon(content, "TIcon" + i, ti[i], 1168, cy[i] - 15, 30, 30);
            RowLabel(tn[i], 1222, cy[i], 21);
            ToggleRow(1541, cy[i], tv[i], tset[i]);
        }

        // ---- Controls
        Section("Controls", 305, 668, 735, 165, iconControls);
        Marker(342, 733); RowLabel("Camera Sensitivity", 362, 733, 20);
        SliderRow(581, 733, 330, EclipseSettings.Sensitivity, v => EclipseSettings.Sensitivity = v, v => Mathf.RoundToInt(v * 100f) + "%", 945);
        Marker(342, 770); RowLabel("Aim Sensitivity", 362, 770, 20);
        SliderRow(581, 770, 330, EclipseSettings.AimSensitivity, v => EclipseSettings.AimSensitivity = v, v => Mathf.RoundToInt(v * 100f) + "%", 945);
        Marker(342, 807); RowLabel("Invert Y-Axis", 362, 807, 20);
        ToggleRow(940, 807, EclipseSettings.InvertY, v => EclipseSettings.InvertY = v);

        // ---- Gameplay
        Section("Gameplay", 1058, 668, 587, 165, null);
        Marker(1092, 735); RowLabel("Auto Pick Up", 1110, 735, 20);
        ToggleRow(1541, 735, EclipseSettings.AutoPickUp, v => EclipseSettings.AutoPickUp = v);
        Marker(1092, 771); RowLabel("Headshot Confirm", 1110, 771, 20);
        ToggleRow(1541, 771, EclipseSettings.HeadshotConfirm, v => EclipseSettings.HeadshotConfirm = v);
        Marker(1092, 807); RowLabel("Show Damage Numbers", 1110, 807, 20);
        ToggleRow(1541, 807, EclipseSettings.DamageNumbers, v => EclipseSettings.DamageNumbers = v);
    }

    void BuildLanguage()
    {
        Section("Language", 305, 112, 1340, 150 + languages.Length * 62, null);
        string cur = EclipseSettings.Language;
        for (int i = 0; i < languages.Length; i++)
        {
            string lang = languages[i];
            bool on = lang == cur;
            float y = 172 + i * 62;
            var b = Panel(content, "Lang" + i, 340, y, 620, 50, on ? new Color(.1f, .02f, .035f, .95f) : Dark, on ? BrightLine : DarkLine, on ? .5f : 0f);
            T(b, "Label", lang, 366, y + 25, 400, 21);
            if (on) T(b, "Mark", "ACTIVE", 840, y + 25, 100, 15, true, TextAnchor.MiddleRight, false, AccentColor);
            Click(b, () => { EclipseSettings.Language = lang; Toast("Language set to " + lang + "."); ShowCat(4); });
        }
        TW(content, "LangHint", "Translations are not bundled yet. Read EclipseSettings.Language from your own localization code.", 340, 190 + languages.Length * 62, 900, 40, 17, Dim);
    }

    void BuildAbout()
    {
        Section("About", 305, 112, 1340, 330, null);
        string pname = EclipsePlayerData.Current != null ? EclipsePlayerData.Current.name : PlayerPrefs.GetString("EclipsePlayerName", "Agent");
        string uid = PlayerPrefs.GetString("EclipseUserId", "-");
        if (uid.Length > 10) uid = uid.Substring(0, 10) + "...";
        InfoRow("Player Name", pname, 190);
        InfoRow("Player ID", uid, 240);
        InfoRow("Version", versionText, 290);
        var b = Panel(content, "SignOut", 340, 340, 300, 58, BrightFill, BrightLine, .6f);
        T(b, "Label", "SIGN OUT", 340, 369, 300, 19, true, TextAnchor.MiddleCenter);
        Click(b, SignOut);
    }

    static int FpsIndex(int fps) { return fps <= 30 ? 0 : (fps <= 45 ? 1 : 2); }
    static int FpsValue(int idx) { return idx == 0 ? 30 : (idx == 1 ? 45 : 60); }

    // ---------------------------------------------------------------- komponen
    void Section(string title, float x, float y, float w, float h, Sprite icon)
    {
        Panel(content, "Sec" + title, x, y, w, h, SecFill, SecLine);
        if (icon != null)
        {
            var ic = Icon(content, "SecIcon" + title, icon, x + 22, y + 8, 30, 30);
            ic.color = AccentColor;
        }
        else
        {
            var sl = S(content, "SecSlash" + title, x + 26, y + 9, 5, 28, AccentColor);
            sl.rectTransform.localEulerAngles = new Vector3(0, 0, -18);
        }
        T(content, "SecTitle" + title, title, x + 60, y + 23, 300, 26, true);
    }

    void RowLabel(string text, float x, float cy, int size)
    {
        T(content, "L_" + text, text, x, cy, 280, size, false, TextAnchor.MiddleLeft, false, new Color(.9f, .9f, .92f));
    }

    void Marker(float x, float cy)
    {
        T(content, "Mk", ">", x, cy, 16, 14, false, TextAnchor.MiddleCenter, false, Dim);
    }

    void HRule(float x, float y, float w) { S(content, "Rule", x, y, w, 1, RowRule); }

    void Seg(string[] opts, float x, float cy, float w, int current, Action<int> set)
    {
        var btns = new RectTransform[opts.Length];
        float bw = (w - 2f * (opts.Length - 1)) / opts.Length;
        for (int i = 0; i < opts.Length; i++)
        {
            float bx = x + i * (bw + 2f);
            var b = Panel(content, "Seg" + i, bx, cy - 19, bw, 38, Dark, DarkLine, .001f);
            T(b, "Label", opts[i], bx, cy, bw, 20, false, TextAnchor.MiddleCenter);
            btns[i] = b;
            int idx = i;
            Click(b, () => { set(idx); PaintSeg(btns, idx); });
        }
        PaintSeg(btns, current);
    }

    static void PaintSeg(RectTransform[] btns, int cur)
    {
        for (int k = 0; k < btns.Length; k++)
        {
            bool on = k == cur;
            Style(btns[k], on ? BrightFill : Dark, on ? BrightLine : DarkLine, on ? .4f : 0f);
        }
    }

    void SliderRow(float x, float cy, float w, float value, Action<float> set, Func<float, string> fmt, float valueX)
    {
        value = Mathf.Clamp01(value);
        S(content, "Track", x, cy - 3, w, 6, new Color(.15f, .155f, .165f, 1f));
        var fill = S(content, "Fill", x, cy - 3, w * value, 6, AccentColor);
        var knob = Img(content, "Knob", x + w * value - 11, cy - 11, 22, 22, DiscSprite(), new Color(.78f, .78f, .8f));
        var val = T(content, "Val", fmt(value), valueX, cy, 90, 21, false, TextAnchor.MiddleLeft, false, new Color(.88f, .89f, .92f));
        var hit = Img(content, "SliderHit", x, cy - 20, w, 40, null, new Color(0, 0, 0, 0), false, true);
        var bar = hit.gameObject.AddComponent<EclipseSliderBar>();
        float ox = 305f;
        bar.onChanged = v =>
        {
            v = Mathf.Round(v * 100f) / 100f;
            set(v);
            fill.rectTransform.sizeDelta = new Vector2(w * v, 6);
            var ap = knob.rectTransform.anchoredPosition;
            ap.x = (x + w * v - 11) - ox;
            knob.rectTransform.anchoredPosition = ap;
            val.text = fmt(v);
        };
    }

    void ToggleRow(float x, float cy, bool on, Action<bool> set)
    {
        var sw = Panel(content, "Switch", x, cy - 13, 55, 26, Dark, DarkLine, .001f);
        var knob = Img(content, "SwitchKnob", x + 3, cy - 10, 20, 20, DiscSprite(), Txt);
        bool state = on;
        Action paint = () =>
        {
            Style(sw, state ? BrightFill : new Color(.13f, .135f, .145f, .96f), state ? BrightLine : DarkLine, state ? .4f : 0f);
            var ap = knob.rectTransform.anchoredPosition;
            ap.x = (state ? x + 55 - 23 : x + 3) - 305f;
            knob.rectTransform.anchoredPosition = ap;
            knob.color = state ? Color.white : new Color(.7f, .7f, .72f);
        };
        paint();
        Click(sw, () => { state = !state; set(state); paint(); });
    }

    void InfoRow(string label, string value, float cy)
    {
        RowLabel(label, 357, cy, 21);
        T(content, "V_" + label, value, 640, cy, 800, 21, false, TextAnchor.MiddleLeft, false, Gold);
        HRule(340, cy + 24, 1270);
    }

    void SignOut()
    {
#if ECLIPSE_FIREBASE
        try { Firebase.Auth.FirebaseAuth.DefaultInstance.SignOut(); } catch (Exception e) { Debug.LogWarning(e.Message); }
#endif
        PlayerPrefs.DeleteKey("EclipsePlayerName");
        PlayerPrefs.DeleteKey("EclipseUserId");
        onSignOut.Invoke();
        if (loadLoginOnSignOut) EclipseScenes.Load(EclipseScenes.Login, this);
    }
}
