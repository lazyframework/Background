using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[Serializable]
public class EclipseOperationMode
{
    public string title = "Story Operations";
    public string subtitle = "Follow the storyline";
    [TextArea(2, 4)] public string description = "";
    public Sprite icon;
    [Tooltip("Gambar latar kartu (opsional).")]
    public Sprite cardArt;
    public EclipseOperationMode() { }
    public EclipseOperationMode(string t, string s, string d) { title = t; subtitle = s; description = d; }
}

// Layar OPERATIONS: pilih mode (kanan), info peta + kesulitan (bawah), tombol PLAY.
public class EclipseOperationsBuilder : EclipseScreenBase
{
    [Header("Mode (maks 3 kartu)")]
    public List<EclipseOperationMode> modes = new List<EclipseOperationMode>();
    public int modeIndex = 0;

    [Header("Peta / misi (tombol > di Map Info berpindah ke misi berikutnya)")]
    public List<EclipseMissionData> missions = new List<EclipseMissionData>();
    public int selectedIndex = 0;
    [Tooltip("Kosong = EclipseScenes.Game.")]
    public string gameSceneName = "";
    public string selectedMissionPrefsKey = "EclipseSelectedMission";
    public string difficultyPrefsKey = "EclipseDifficulty";
    public string modePrefsKey = "EclipseOpsMode";

    [Header("Ikon (opsional)")]
    public Sprite iconStory, iconSpecial, iconCoop, iconMail;

    [Serializable] public class MissionEvent : UnityEvent<int, int> { }
    [Tooltip("(index misi, kesulitan 0-3)")]
    public MissionEvent onStartMission = new MissionEvent();

    const int ModeCards = 3;
    static readonly string[] DiffNames = { "Easy", "Normal", "Hard", "Nightmare" };

    int difficulty = 1;
    readonly List<RectTransform> modeRows = new List<RectTransform>();
    readonly RectTransform[] diffBtns = new RectTransform[4];
    readonly Text[] diffLabels = new Text[4];
    Image mapThumb;
    Text bigTitle, bigDesc, mapName, mapDesc, startLabel;
    RectTransform startBtn;
    Image startFill;

    protected override string RootName { get { return "EclipseOperationsCanvas_v1"; } }
    protected override string TabId { get { return "TabOperations"; } }
    protected override string PageTitle { get { return "OPERATIONS"; } }
    protected override string PageSubtitle { get { return ""; } }
    protected override bool UseStandardHeader { get { return false; } }

    void EnsureDefaults()
    {
        if (missions == null || missions.Count == 0) missions = EclipseMissionData.CreateDefaults();
        if (modes == null || modes.Count == 0)
        {
            modes = new List<EclipseOperationMode>();
            modes.Add(new EclipseOperationMode("Story Operations", "Follow the storyline", "Complete tactical missions and neutralize the threat."));
            modes.Add(new EclipseOperationMode("Special Operations", "High-risk missions", "Take on high-risk missions for bigger rewards."));
            modes.Add(new EclipseOperationMode("Co-op Operations", "Team up for missions", "Team up with other agents and complete missions together."));
        }
    }

    Sprite ModeIcon(int i)
    {
        var m = modes[i];
        if (m.icon != null) return m.icon;
        return i == 0 ? iconStory : (i == 1 ? iconSpecial : iconCoop);
    }

    static string FirstSentence(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        int i = s.IndexOf(". ");
        return i > 0 ? s.Substring(0, i + 1) : s;
    }

    protected override void BuildContent()
    {
        EnsureDefaults();
        modeRows.Clear();
        difficulty = Mathf.Clamp(PlayerPrefs.GetInt(difficultyPrefsKey, 1), 0, 3);

        var keepBell = bell;
        if (iconMail != null) bell = iconMail;
        RefHeader("OPERATIONS", AccentColor, true);
        bell = keepBell;
        ha = HA.Left; va = VA.Top;

        // ---------------- judul besar (kiri bawah)
        Img(frame, "TitleShade", 32, 500, 580, 190, FadeHSprite(), new Color(0f, 0f, 0f, .62f));
        var bs = S(frame, "BigSlash", 62, 531, 14, 60, AccentColor);
        bs.rectTransform.localEulerAngles = new Vector3(0, 0, -10);
        bigTitle = T(frame, "BigTitle", "", 102, 560, 760, 66, true, TextAnchor.MiddleLeft, true);
        bigDesc = TW(frame, "BigDesc", "", 98, 602, 430, 76, 26, Txt);

        // ---------------- pilih mode (kanan)
        ha = HA.Right;
        var mh = Panel(frame, "ModeHeader", 1186, 187, 449, 41, new Color(.03f, .032f, .04f, .88f), new Color(.17f, .18f, .2f, .7f));
        S(mh, "Bar", 1207, 197, 4, 22, AccentColor);
        T(mh, "Label", "MODE SELECT", 1226, 208, 300, 22, true);
        int n = Mathf.Min(modes.Count, ModeCards);
        for (int i = 0; i < n; i++) BuildMode(i, 1186, 242 + i * 128);

        // ---------------- info peta (kiri bawah)
        ha = HA.Left;
        var mp = Panel(frame, "MapInfo", 32, 712, 640, 180, new Color(.03f, .032f, .038f, .9f), new Color(.2f, .21f, .24f, .85f));
        S(mp, "Bar", 50, 724, 4, 22, AccentColor);
        T(mp, "Label", "MAP INFO", 65, 735, 300, 22, true);
        T(mp, "Arrow", ">", 632, 735, 26, 24, false, TextAnchor.MiddleCenter, false, Dim);
        mapThumb = Pic(mp, "MapThumb", 56, 757, 272, 115, null, false);
        Img(mp, "ThumbFrame", 56, 757, 272, 115, LineSprite(), new Color(.62f, .63f, .67f, .8f), true);
        mapName = T(mp, "MapName", "", 361, 785, 290, 25);
        mapDesc = TW(mp, "MapDesc", "", 361, 806, 290, 62, 19, Dim);
        foreach (var g in mp.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;
        Click(mp);

        // ---------------- kesulitan
        var dp = Panel(frame, "DifficultyPanel", 672, 712, 488, 180, new Color(.03f, .032f, .038f, .9f), new Color(.2f, .21f, .24f, .85f));
        S(dp, "Bar", 690, 724, 4, 22, AccentColor);
        T(dp, "Label", "DIFFICULTY", 704, 736, 300, 22, true);
        float[] bx = { 697, 810, 923, 1034 };
        float[] bw = { 103, 102, 102, 103 };
        for (int k = 0; k < 4; k++)
        {
            var b = Panel(frame, "Diff" + k, bx[k], 779, bw[k], 59, new Color(.04f, .042f, .05f, .9f), new Color(.25f, .26f, .29f, .9f), .001f);
            diffLabels[k] = T(b, "Label", DiffNames[k], bx[k], 808, bw[k], 20, false, TextAnchor.MiddleCenter);
            foreach (var g in b.GetComponentsInChildren<Graphic>(true))
               g.raycastTarget = false;
            Click(b);
            diffBtns[k] = b;
        }

        // ---------------- PLAY
        ha = HA.Right;
        startBtn = SlantButton("StartMission", "PLAY", 1230, 762, 407, 110, 56);
        startLabel = startBtn.Find("StartMissionLabel").GetComponent<Text>();
        startFill = startBtn.Find("Fill").GetComponent<Image>();
        ha = HA.Left;
    }

    void BuildMode(int i, float x, float y)
    {
        const float W = 449, H = 116;
        var m = modes[i];
        var r = Panel(frame, "Mode" + i, x, y, W, H, new Color(.04f, .042f, .05f, .9f), new Color(.2f, .21f, .24f, .9f), .001f);
        Pic(r, "Art", x + 2, y + 2, W - 4, H - 4, m.cardArt, false);
        Img(r, "Fade", x + 2, y + 2, W - 4, H - 4, FadeHSprite(), new Color(.03f, .03f, .035f, .92f));
        Icon(r, "Icon", ModeIcon(i), x + 28, y + 22, 76, 72);
        T(r, "Title", m.title, x + 139, y + 44, 270, 25);
        T(r, "Sub", m.subtitle, x + 139, y + 73, 270, 19, false, TextAnchor.MiddleLeft, false, Dim);
        Chevron(r, "Chev", x + W - 22, y + H / 2f, 12, 3, Txt, 1);
        foreach (var g in r.GetComponentsInChildren<Graphic>(true))
           g.raycastTarget = false;
        Click(r);
        modeRows.Add(r);
    }

    protected override void OnBuilt()
    {
        EnsureDefaults();
        modeIndex = Mathf.Clamp(PlayerPrefs.GetInt(modePrefsKey, modeIndex), 0, Mathf.Min(modes.Count, ModeCards) - 1);
        selectedIndex = Mathf.Clamp(PlayerPrefs.GetInt(selectedMissionPrefsKey, selectedIndex), 0, missions.Count - 1);
        Apply();
    }

    protected override void OnRoute(string id)
    {
        int i;
        if (id.StartsWith("Mode") && id != "ModeHeader" && int.TryParse(id.Substring(4), out i)) { modeIndex = i; PlayerPrefs.SetInt(modePrefsKey, i); Apply(); }
        else if (id.StartsWith("Diff") && int.TryParse(id.Substring(4), out i)) { difficulty = Mathf.Clamp(i, 0, 3); PlayerPrefs.SetInt(difficultyPrefsKey, difficulty); Apply(); }
        else if (id == "MapInfo") { selectedIndex = (selectedIndex + 1) % Mathf.Max(1, missions.Count); Apply(); }
        else if (id == "StartMission") StartMission();
    }

    void Apply()
    {
        if (!built || bigTitle == null) return;
        EnsureDefaults();
        modeIndex = Mathf.Clamp(modeIndex, 0, Mathf.Min(modes.Count, ModeCards) - 1);
        for (int k = 0; k < modeRows.Count; k++)
        {
            bool on = k == modeIndex;
            Style(modeRows[k], on ? new Color(.09f, .02f, .03f, .92f) : new Color(.04f, .042f, .05f, .9f), on ? BrightLine : new Color(.2f, .21f, .24f, .9f), on ? .8f : 0f);
        }
        var md = modes[modeIndex];
        if (md.title != null && md.title.ToLowerInvariant().Contains("operations")) bigTitle.text = "OPERATIONS";
        else bigTitle.text = (md.title ?? "").ToUpperInvariant();
        bigDesc.text = md.description;

        selectedIndex = Mathf.Clamp(selectedIndex, 0, missions.Count - 1);
        var m = missions[selectedIndex];
        mapThumb.sprite = m.artwork;
        mapThumb.enabled = m.artwork != null;
        mapName.text = m.missionName;
        mapDesc.text = FirstSentence(m.description);

        for (int k = 0; k < 4; k++)
        {
            bool on = k == difficulty;
            Style(diffBtns[k], on ? new Color(.1f, .02f, .035f, .95f) : new Color(.04f, .042f, .05f, .9f), on ? BrightLine : new Color(.25f, .26f, .29f, .9f), on ? .6f : 0f);
        }

        startLabel.text = m.locked ? "LOCKED" : "PLAY";
        startFill.color = m.locked ? new Color(.45f, .45f, .47f) : Color.white;
    }

    void StartMission()
    {
        var m = missions[selectedIndex];
        if (m.locked) { Toast("This mission is still locked.", false); return; }
        PlayerPrefs.SetInt(selectedMissionPrefsKey, selectedIndex);
        PlayerPrefs.SetInt(difficultyPrefsKey, difficulty);
        PlayerPrefs.SetInt(modePrefsKey, modeIndex);
        PlayerPrefs.Save();
        onStartMission.Invoke(selectedIndex, difficulty);
        string scene = !string.IsNullOrEmpty(m.sceneName) ? m.sceneName : (!string.IsNullOrEmpty(gameSceneName) ? gameSceneName : EclipseScenes.Game);
        EclipseScenes.Load(scene, this);
    }
}
