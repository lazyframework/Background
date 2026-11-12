using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// Layar ARMORY: kategori senjata di kiri, tampilan senjata di tengah, statistik + upgrade di kanan,
// daftar senjata kategori terpilih di bawah, tombol EQUIP di kanan bawah.
public class EclipseArmoryBuilder : EclipseScreenBase
{
    [Header("Senjata")]
    public List<EclipseWeaponData> weapons = new List<EclipseWeaponData>();
    public int selectedIndex = 0;
    public int maxStat = 100;
    public string equippedWeaponPrefsKey = "EclipseEquippedWeapon";

    [Header("Ikon (opsional, kosong = tidak ditampilkan)")]
    public Sprite iconCatAssault, iconCatSmg, iconCatShotgun, iconCatSniper, iconCatLmg, iconCatPistol, iconCatMelee;
    public Sprite iconType, iconInfo;
    [Tooltip("5 slot attachment di panel kanan.")]
    public Sprite[] attachmentIcons = new Sprite[5];

    [Serializable] public class UpgradeEvent : UnityEvent<string, int> { }
    [Tooltip("(id senjata, level baru)")]
    public UpgradeEvent onUpgraded = new UpgradeEvent();
    [Serializable] public class EquipEvent : UnityEvent<string> { }
    public EquipEvent onEquipped = new EquipEvent();

    const int Cards = 5;
    const float BarW = 156f;
    static readonly string[] CatNames = { "ASSAULT RIFLES", "SMGs", "SHOTGUNS", "SNIPER RIFLES", "LMGs", "PISTOLS", "MELEE" };
    static readonly string[] StatNames = { "Damage", "Fire Rate", "Accuracy", "Range", "Mobility" };

    int cat;
    readonly List<int> view = new List<int>();
    readonly List<RectTransform> catRows = new List<RectTransform>();
    readonly List<Image> catIcons = new List<Image>();
    readonly List<Text> catLabels = new List<Text>();
    readonly RectTransform[] cardRoots = new RectTransform[Cards];
    readonly Image[] cardIcons = new Image[Cards];
    readonly Text[] cardLetters = new Text[Cards];
    readonly Text[] cardNames = new Text[Cards];
    readonly GameObject[] cardChecks = new GameObject[Cards];
    readonly Text[] statVal = new Text[5];
    readonly Image[] statFill = new Image[5];
    readonly Image[] statPrev = new Image[5];
    Text wName, wType, wLevel, wDesc, upLabel, equipLabel;
    Image wArt, equipFill;
    GameObject silhouette;
    RectTransform upBtn, equipBtn;

    protected override string RootName { get { return "EclipseArmoryCanvas_v1"; } }
    protected override string TabId { get { return "TabArmory"; } }
    protected override string PageTitle { get { return "ARMORY"; } }
    protected override string PageSubtitle { get { return ""; } }

    static EclipseWeaponData W(string id, string n, string t, int d, int r, int a, int g, int m, int cost, string desc)
    {
        return new EclipseWeaponData(id, n, t, d, r, a, g, m, cost, desc);
    }

    void EnsureDefaults()
    {
        if (weapons != null && weapons.Count > 0) return;
        weapons = new List<EclipseWeaponData>();
        weapons.Add(W("ar_m4a1", "M4A1", "Assault Rifle", 75, 82, 70, 68, 85, 1000, "A reliable and versatile assault rifle for any combat situation."));
        weapons.Add(W("ar_ak47", "AK-47", "Assault Rifle", 85, 66, 60, 72, 60, 1100, "Hard-hitting rifle with heavy recoil."));
        weapons.Add(W("ar_scarl", "SCAR-L", "Assault Rifle", 78, 74, 76, 70, 65, 1200, "Stable and accurate at medium range."));
        weapons.Add(W("ar_aug", "AUG", "Assault Rifle", 70, 80, 82, 74, 70, 1200, "Bullpup rifle with excellent control."));
        weapons.Add(W("ar_type95", "Type 95", "Assault Rifle", 72, 78, 78, 70, 72, 1300, "Compact and well balanced."));
        weapons.Add(W("smg_hornet", "Hornet SMG", "Submachine Gun", 30, 92, 58, 40, 85, 800, "Extreme fire rate and mobility for close quarters."));
        weapons.Add(W("sg_breaker", "Breaker-12", "Shotgun", 88, 30, 35, 22, 60, 1200, "Devastating at short range. Clears rooms fast."));
        weapons.Add(W("sr_longshot", "Longshot SR", "Sniper Rifle", 95, 18, 94, 98, 30, 1500, "Bolt-action precision rifle with extreme range."));
        weapons.Add(W("lmg_hammer", "Hammer LMG", "LMG", 55, 70, 48, 70, 35, 1400, "Sustained suppressive fire with a huge magazine."));
        weapons.Add(W("pst_sidearm", "S7 Sidearm", "Pistol", 28, 55, 76, 35, 95, 500, "Compact backup weapon. Fast to draw."));
        weapons.Add(W("ml_knife", "Combat Knife", "Melee", 60, 90, 95, 5, 100, 300, "Silent and deadly up close."));
    }

    static int CatOf(string type)
    {
        string t = (type ?? "").ToLowerInvariant();
        if (t.Contains("sniper")) return 3;
        if (t.Contains("submachine") || t.Contains("smg")) return 1;
        if (t.Contains("shotgun")) return 2;
        if (t.Contains("lmg") || t.Contains("light machine") || t.Contains("machine gun")) return 4;
        if (t.Contains("pistol") || t.Contains("handgun") || t.Contains("revolver")) return 5;
        if (t.Contains("melee") || t.Contains("knife") || t.Contains("blade")) return 6;
        return 0;
    }

    Sprite CatIcon(int i)
    {
        switch (i)
        {
            case 0: return iconCatAssault;
            case 1: return iconCatSmg;
            case 2: return iconCatShotgun;
            case 3: return iconCatSniper;
            case 4: return iconCatLmg;
            case 5: return iconCatPistol;
        }
        return iconCatMelee;
    }

    protected override void BuildContent()
    {
        EnsureDefaults();
        catRows.Clear(); catIcons.Clear(); catLabels.Clear();

        // ---------------- kategori senjata (kiri)
        ha = HA.Left; va = VA.Top;
        for (int i = 0; i < CatNames.Length; i++)
        {
            float y = 153 + i * 74.5f;
            var r = Panel(frame, "Cat" + i, 18, y, 270, 74, new Color(.04f, .042f, .05f, .8f), new Color(.17f, .18f, .2f, .8f), .001f);
            catIcons.Add(Icon(r, "Icon", CatIcon(i), 42, y + 14, 92, 46));
            catLabels.Add(T(r, "Label", CatNames[i], 150, y + 37, 130, 18, true));
            Click(r);
            catRows.Add(r);
        }

        // ---------------- senjata (tengah)
        ha = HA.Center;
        var sil = R(frame, "Silhouette", 400, 340, 820, 260);
        var sc = new Color(.16f, .17f, .19f, .9f);
        S(sil, "Stock", 440, 420, 120, 60, sc);
        S(sil, "Body", 550, 400, 400, 70, new Color(.19f, .2f, .22f, .9f));
        S(sil, "Handguard", 940, 392, 190, 52, sc);
        S(sil, "Barrel", 1120, 412, 120, 22, sc);
        S(sil, "Sight", 620, 372, 130, 24, sc);
        S(sil, "Grip", 700, 470, 50, 100, sc);
        S(sil, "Mag", 820, 470, 60, 120, sc);
        silhouette = sil.gameObject;
        wArt = Pic(frame, "WeaponArt", 330, 190, 920, 470, null, true);

        // ---------------- statistik (kanan)
        ha = HA.Right;
        Panel(frame, "StatsPanel", 1285, 135, 365, 563, new Color(.03f, .032f, .038f, .84f), new Color(.15f, .16f, .18f, .6f));
        wName = T(frame, "WeaponName", "", 1309, 182, 330, 42, true);
        var tm = Img(frame, "TypeMark", 1313, 211, 14, 14, LineSprite(), Txt, true);
        tm.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
        Icon(frame, "TypeIcon", iconType, 1311, 208, 22, 22);
        wType = T(frame, "WeaponType", "", 1341, 220, 300, 20, false, TextAnchor.MiddleLeft, false, new Color(.88f, .89f, .92f));
        wDesc = TW(frame, "WeaponDesc", "", 1309, 250, 330, 70, 20, Dim);
        for (int i = 0; i < 5; i++)
        {
            float cy = 359 + i * 35;
            T(frame, "StatLabel" + i, StatNames[i], 1309, cy, 110, 19);
            S(frame, "StatTrack" + i, 1421, cy - 2.5f, BarW, 5, new Color(.16f, .165f, .175f, 1f));
            statPrev[i] = S(frame, "StatPrev" + i, 1421, cy - 2.5f, 0, 5, new Color(.92f, .1f, .12f, .6f));
            statFill[i] = S(frame, "StatFill" + i, 1421, cy - 2.5f, 0, 5, Txt);
            statVal[i] = T(frame, "StatValue" + i, "", 1550, cy, 80, 19, false, TextAnchor.MiddleRight);
        }
        S(frame, "StatsRule", 1309, 541, 322, 1, Rule);
        T(frame, "AttachHeading", "Attachments", 1309, 564, 300, 19);
        for (int k = 0; k < 5; k++)
        {
            float ax = 1309 + k * 67;
            Panel(frame, "Attach" + k, ax, 584, 58, 58, new Color(.05f, .052f, .06f, .9f), new Color(.3f, .31f, .34f, .9f));
            Pic(frame, "AttachIcon" + k, ax + 5, 589, 48, 48, attachmentIcons != null && k < attachmentIcons.Length ? attachmentIcons[k] : null, true);
        }
        wLevel = TC(frame, "WeaponLevel", "", 1309, 674, 140, 16, Dim, true);
        var ub = Panel(frame, "Upgrade", 1459, 654, 172, 38, BrightFill, BrightLine, .001f);
        upLabel = T(ub, "Title", "UPGRADE", 1459, 673, 172, 15, true, TextAnchor.MiddleCenter);
        Click(ub);
        upBtn = ub;

        // ---------------- daftar senjata (bawah)
        ha = HA.Left;
        for (int k = 0; k < Cards; k++)
        {
            float x = 30 + k * 264, y = 700;
            var r = Panel(frame, "Weapon" + k, x, y, 254, 120, new Color(.05f, .052f, .06f, .86f), new Color(.2f, .21f, .24f, .9f), .001f);
            cardLetters[k] = TC(r, "Letter", "", x, y + 48, 254, 40, new Color(.25f, .26f, .28f), true, TextAnchor.MiddleCenter);
            cardIcons[k] = Pic(r, "Icon", x + 12, y + 8, 230, 84, null, true);
            cardNames[k] = T(r, "Name", "", x + 14, y + 101, 220, 21);
            var chk = R(r, "Check", x + 254 - 34, y, 34, 34);
            Img(chk, "Back", x + 254 - 34, y, 34, 34, SlantSprite(), AccentColor).preserveAspect = false;
            Bar(chk, "CheckA", x + 254 - 20, y + 19, 7, 3, 45f, Color.white);
            Bar(chk, "CheckB", x + 254 - 14, y + 16, 12, 3, -50f, Color.white);
            cardChecks[k] = chk.gameObject;
            Click(r);
            cardRoots[k] = r;
        }

        // ---------------- tombol bawah
        var wd = Panel(frame, "WeaponDetails", 28, 868, 214, 48, new Color(.04f, .042f, .05f, .88f), new Color(.2f, .21f, .24f, .8f));
        Icon(wd, "Icon", iconInfo, 40, 878, 26, 26);
        if (iconInfo == null) { Img(wd, "InfoRing", 41, 879, 24, 24, RingSprite(), Txt); T(wd, "InfoI", "i", 41, 891, 24, 16, true, TextAnchor.MiddleCenter); }
        T(wd, "Label", "Weapon Details", 91, 892, 140, 19, false, TextAnchor.MiddleLeft, false, new Color(.88f, .89f, .92f));
        Click(wd);

        ha = HA.Right;
        equipBtn = SlantPlain("Equip", "EQUIP", 1350, 842, 300, 72, 36);
        equipLabel = equipBtn.Find("EquipLabel").GetComponent<Text>();
        equipFill = equipBtn.Find("Fill").GetComponent<Image>();
        ha = HA.Left;
    }

    protected override void OnBuilt()
    {
        EnsureDefaults();
        selectedIndex = Mathf.Clamp(selectedIndex, 0, weapons.Count - 1);
        cat = CatOf(weapons[selectedIndex].type);
        Apply();
    }

    protected override void OnRefresh() { if (built) Apply(); }

    protected override void OnRoute(string id)
    {
        int i;
        if (id.StartsWith("Cat") && int.TryParse(id.Substring(3), out i)) { cat = Mathf.Clamp(i, 0, CatNames.Length - 1); selectedIndex = -1; Apply(); }
        else if (id.StartsWith("Weapon") && id != "WeaponDetails" && int.TryParse(id.Substring(6), out i)) { if (i < view.Count) { selectedIndex = view[i]; Apply(); } }
        else if (id == "Upgrade") Upgrade();
        else if (id == "Equip") Equip();
        else if (id == "WeaponDetails") ShowDetails();
    }

    public void Select(int weaponIndex)
    {
        EnsureDefaults();
        if (weapons.Count == 0) return;
        selectedIndex = Mathf.Clamp(weaponIndex, 0, weapons.Count - 1);
        cat = CatOf(weapons[selectedIndex].type);
        Apply();
    }

    string EquippedId() { return PlayerPrefs.GetString(equippedWeaponPrefsKey, ""); }

    void Apply()
    {
        if (!built || wName == null) return;
        EnsureDefaults();

        view.Clear();
        for (int i = 0; i < weapons.Count; i++) if (CatOf(weapons[i].type) == cat) view.Add(i);
        if (view.Count > 0 && !view.Contains(selectedIndex)) selectedIndex = view[0];

        for (int i = 0; i < catRows.Count; i++)
        {
            bool on = i == cat;
            Style(catRows[i], on ? BrightFill : new Color(.04f, .042f, .05f, .8f), on ? BrightLine : new Color(.17f, .18f, .2f, .8f), on ? .5f : 0f);
            catIcons[i].color = on ? Color.white : new Color(.72f, .73f, .76f);
            catLabels[i].color = on ? Color.white : new Color(.75f, .76f, .8f);
        }

        string eq = EquippedId();
        for (int k = 0; k < Cards; k++)
        {
            bool has = k < view.Count;
            cardRoots[k].gameObject.SetActive(has);
            if (!has) continue;
            var w = weapons[view[k]];
            bool sel = view[k] == selectedIndex;
            Style(cardRoots[k], new Color(.05f, .052f, .06f, .86f), sel ? BrightLine : new Color(.2f, .21f, .24f, .9f), sel ? .5f : 0f);
            cardNames[k].text = w.weaponName;
            cardNames[k].color = sel ? Color.white : new Color(.85f, .86f, .89f);
            cardIcons[k].sprite = w.icon;
            cardIcons[k].enabled = w.icon != null;
            cardLetters[k].text = w.icon == null && !string.IsNullOrEmpty(w.weaponName) ? w.weaponName.Substring(0, 1) : "";
            cardChecks[k].SetActive(w.id == eq);
        }

        if (view.Count == 0)
        {
            wName.text = "NO WEAPONS"; wType.text = CatNames[cat]; wDesc.text = "No weapons in this category yet.";
            wLevel.text = ""; wArt.enabled = false; silhouette.SetActive(false);
            for (int s = 0; s < 5; s++) { statFill[s].rectTransform.sizeDelta = new Vector2(0, 5); statPrev[s].rectTransform.sizeDelta = new Vector2(0, 5); statVal[s].text = ""; }
            upLabel.text = "-"; Style(upBtn, new Color(.12f, .12f, .13f, .96f), PanelLine, 0f);
            equipLabel.text = "EQUIP"; equipFill.color = new Color(.4f, .4f, .42f);
            return;
        }

        var cur = weapons[selectedIndex];
        int lv = cur.Level;
        bool max = lv >= cur.maxLevel;
        wName.text = cur.weaponName;
        wType.text = cur.type;
        wDesc.text = cur.description;
        wLevel.text = "LV " + lv + " / " + cur.maxLevel;
        wArt.sprite = cur.icon;
        wArt.enabled = cur.icon != null;
        silhouette.SetActive(cur.icon == null);

        int[] bases = { cur.damage, cur.fireRate, cur.accuracy, cur.range, cur.mobility };
        float ms = Mathf.Max(1, maxStat);
        for (int s = 0; s < 5; s++)
        {
            int c = cur.Stat(bases[s], lv);
            int n = max ? c : cur.Stat(bases[s], lv + 1);
            statFill[s].rectTransform.sizeDelta = new Vector2(BarW * Mathf.Clamp01(c / ms), 5);
            statPrev[s].rectTransform.sizeDelta = new Vector2(BarW * Mathf.Clamp01(n / ms), 5);
            statVal[s].text = c.ToString();
        }

        if (max)
        {
            upLabel.text = "MAX LEVEL"; upLabel.color = Dim;
            Style(upBtn, new Color(.12f, .12f, .13f, .96f), PanelLine, 0f);
        }
        else
        {
            int cost = cur.CostFor(lv);
            bool afford = EclipseEconomy.Coins >= cost;
            upLabel.text = "UPGRADE  " + cost.ToString("N0");
            upLabel.color = afford ? Color.white : Danger;
            Style(upBtn, BrightFill, BrightLine, .001f);
        }

        bool isEq = cur.id == eq;
        equipLabel.text = isEq ? "EQUIPPED" : "EQUIP";
        equipFill.color = isEq ? new Color(.62f, .62f, .64f) : Color.white;
    }

    void Equip()
    {
        if (view.Count == 0) return;
        var w = weapons[selectedIndex];
        PlayerPrefs.SetString(equippedWeaponPrefsKey, w.id);
        PlayerPrefs.Save();
        onEquipped.Invoke(w.id);
        Toast(w.weaponName + " equipped.");
        Apply();
    }

    void Upgrade()
    {
        if (view.Count == 0) return;
        var w = weapons[selectedIndex];
        int lv = w.Level;
        if (lv >= w.maxLevel) { Toast("Already at max level.", false); return; }
        if (!EclipseEconomy.Spend(EclipseCurrency.Coins, w.CostFor(lv))) { Toast("Not enough coins.", false); return; }
        w.Level = lv + 1;
        onUpgraded.Invoke(w.id, lv + 1);
        Toast(w.weaponName + " upgraded to Lv. " + (lv + 1));
        Apply();
    }

    void ShowDetails()
    {
        if (view.Count == 0) return;
        var w = weapons[selectedIndex];
        Toast(w.weaponName + "  -  Lv. " + w.Level + " / " + w.maxLevel + "  -  each level adds about " + Mathf.RoundToInt(w.growth * 100f) + "% to all stats");
    }
}
