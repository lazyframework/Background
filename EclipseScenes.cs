using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================ Navigasi scene
// Ubah nama scene di sini (sekali) kalau nama scene projectmu berbeda.
public static class EclipseScenes
{
    public static string Lobby = "EclipseLobby";
    public static string Operations = "Operations";
    public static string Agents = "CharacterSelect";
    public static string Armory = "Armory";
    public static string Inventory = "Inventory";
    public static string Store = "Store";
    public static string Settings = "Settings";
    public static string Login = "Login";
    public static string Game = "GameScene";

    public static string FromId(string id)
    {
        switch (id)
        {
            case "Home": case "TabHome": return Lobby;
            case "Operations": case "TabOperations": return Operations;
            case "Agents": case "TabAgents": return Agents;
            case "Armory": case "TabArmory": return Armory;
            case "Inventory": case "TabInventory": return Inventory;
            case "Store": case "TabStore": case "Diamonds": case "Coins": return Store;
            case "Settings": case "TabSettings": case "SettingsTop": return Settings;
        }
        return null;
    }

    public static bool Load(string scene, UnityEngine.Object ctx)
    {
        if (string.IsNullOrEmpty(scene)) return false;
        if (Application.CanStreamedLevelBeLoaded(scene)) { SceneManager.LoadScene(scene); return true; }
        Debug.LogError("Scene '" + scene + "' belum ada di File > Build Profiles > Scene List.", ctx);
        return false;
    }
}

// ============================================================ Dompet
public enum EclipseCurrency { Coins, Diamonds, RealMoney }

// Dompet pemain. Pakai data Firebase (EclipsePlayerData) kalau ada, kalau tidak pakai PlayerPrefs lokal.
public static class EclipseEconomy
{
    public static event Action OnChanged;
    public static int DefaultCoins = 15340, DefaultDiamonds = 1200;
    const string KCoin = "EclipseLocalCoins", KDia = "EclipseLocalDiamonds";

    public static int Coins
    {
        get { var p = EclipsePlayerData.Current; return p != null ? p.coins : PlayerPrefs.GetInt(KCoin, DefaultCoins); }
    }
    public static int Diamonds
    {
        get { var p = EclipsePlayerData.Current; return p != null ? p.diamonds : PlayerPrefs.GetInt(KDia, DefaultDiamonds); }
    }

    public static int Balance(EclipseCurrency c)
    {
        return c == EclipseCurrency.Coins ? Coins : (c == EclipseCurrency.Diamonds ? Diamonds : 0);
    }

    public static bool CanAfford(EclipseCurrency c, int amount)
    {
        if (c == EclipseCurrency.RealMoney) return false;
        return Balance(c) >= amount;
    }

    public static bool Spend(EclipseCurrency c, int amount)
    {
        if (amount <= 0) return true;
        if (!CanAfford(c, amount)) return false;
        Change(c, -amount);
        return true;
    }

    public static void Add(EclipseCurrency c, int amount)
    {
        if (amount <= 0 || c == EclipseCurrency.RealMoney) return;
        Change(c, amount);
    }

    static void Change(EclipseCurrency c, int delta)
    {
        bool dia = c == EclipseCurrency.Diamonds;
        if (EclipsePlayerData.Current != null)
        {
            if (dia) EclipsePlayerData.AddDiamonds(delta); else EclipsePlayerData.AddCoins(delta);
        }
        else
        {
            string key = dia ? KDia : KCoin;
            int cur = PlayerPrefs.GetInt(key, dia ? DefaultDiamonds : DefaultCoins);
            PlayerPrefs.SetInt(key, Mathf.Max(0, cur + delta));
            PlayerPrefs.Save();
        }
        if (OnChanged != null) OnChanged();
    }
}

// ============================================================ Item & inventori
public enum EclipseItemCategory { Consumable, Material, Gear, Cosmetic, Weapon }
public enum EclipseRarity { Common, Rare, Epic, Legendary }

[Serializable]
public class EclipseItemStat
{
    public string label = "Stat";
    public int value = 0;
    public EclipseItemStat() { }
    public EclipseItemStat(string l, int v) { label = l; value = v; }
}

[Serializable]
public class EclipseItemData
{
    public string id = "item";
    public string itemName = "Item";
    public EclipseItemCategory category = EclipseItemCategory.Consumable;
    public EclipseRarity rarity = EclipseRarity.Common;
    [Tooltip("Jumlah awal (dipakai kalau pemain belum pernah punya item ini).")]
    public int defaultQuantity = 0;
    [TextArea(2, 4)] public string description = "";
    public Sprite icon;
    [Tooltip("Teks kecil di bawah nama, mis. Primary Weapon.")]
    public string subtitle = "";
    [Tooltip("Slot perlengkapan: Helmet, Vest, Gloves, Pants, Boots (Gear) atau Primary, Secondary, Melee (Weapon). Kosong = satu item per kategori.")]
    public string gearSlot = "";
    [Tooltip("Statistik yang ditampilkan di panel detail (maks 4).")]
    public List<EclipseItemStat> stats = new List<EclipseItemStat>();
    public EclipseItemData() { }
    public EclipseItemData(string id, string name, EclipseItemCategory cat, EclipseRarity r, int qty, string desc)
    {
        this.id = id; itemName = name; category = cat; rarity = r; defaultQuantity = qty; description = desc;
    }
}

public static class EclipseItemCatalog
{
    static EclipseItemData Gear(string id, string name, EclipseRarity r, string slot, string desc)
    {
        var it = new EclipseItemData(id, name, EclipseItemCategory.Gear, r, 1, desc);
        it.gearSlot = slot; it.subtitle = slot;
        return it;
    }

    static EclipseItemData Weapon(string id, string name, EclipseRarity r, string subtitle, string slot, string desc, int dmg, int acc, int rate, int mag)
    {
        var it = new EclipseItemData(id, name, EclipseItemCategory.Weapon, r, 1, desc);
        it.subtitle = subtitle; it.gearSlot = slot;
        it.stats.Add(new EclipseItemStat("Damage", dmg));
        it.stats.Add(new EclipseItemStat("Accuracy", acc));
        if (rate > 0) it.stats.Add(new EclipseItemStat("Fire Rate", rate));
        if (mag > 0) it.stats.Add(new EclipseItemStat("Magazine", mag));
        return it;
    }

    public static List<EclipseItemData> Defaults()
    {
        var l = new List<EclipseItemData>();
        l.Add(new EclipseItemData("medkit", "Medkit", EclipseItemCategory.Consumable, EclipseRarity.Common, 5, "Restores 40% HP over a short time. Can be used during a mission."));
        l.Add(new EclipseItemData("armor_plate", "Armor Plate", EclipseItemCategory.Consumable, EclipseRarity.Rare, 3, "Instantly repairs 50 armor points."));
        l.Add(new EclipseItemData("adrenaline", "Adrenaline Shot", EclipseItemCategory.Consumable, EclipseRarity.Rare, 2, "Boosts movement speed by 20% for 15 seconds."));
        l.Add(new EclipseItemData("frag", "Frag Grenade", EclipseItemCategory.Consumable, EclipseRarity.Common, 4, "Standard fragmentation grenade. Wide blast radius."));
        l.Add(new EclipseItemData("scrap", "Scrap Metal", EclipseItemCategory.Material, EclipseRarity.Common, 120, "Basic crafting material salvaged from the field."));
        l.Add(new EclipseItemData("circuit", "Circuit Board", EclipseItemCategory.Material, EclipseRarity.Rare, 18, "Used to craft and upgrade advanced gear."));
        l.Add(new EclipseItemData("energy_cell", "Energy Cell", EclipseItemCategory.Material, EclipseRarity.Epic, 6, "Rare power source. Required for high-tier upgrades."));
        l.Add(Gear("vest", "Tactical Vest", EclipseRarity.Rare, "Vest", "Lightweight vest. +15 armor."));
        l.Add(Gear("helmet", "Recon Helmet", EclipseRarity.Epic, "Helmet", "Integrated optics. +10 armor, improved target spotting."));
        l.Add(Gear("gloves", "Combat Gloves", EclipseRarity.Common, "Gloves", "Improves weapon handling slightly."));
        l.Add(Gear("pants", "Combat Pants", EclipseRarity.Common, "Pants", "Reinforced pants with extra pockets."));
        l.Add(Gear("boots", "Combat Boots", EclipseRarity.Common, "Boots", "Sturdy boots. Slightly improves movement."));
        l.Add(Weapon("wpn_ar", "Assault Rifle", EclipseRarity.Rare, "Primary Weapon", "Primary", "A reliable assault rifle with balanced performance.", 45, 72, 680, 30));
        l.Add(Weapon("wpn_pistol", "Pistol", EclipseRarity.Common, "Secondary Weapon", "Secondary", "Compact sidearm. Fast to draw.", 28, 76, 420, 15));
        l.Add(Weapon("wpn_knife", "Combat Knife", EclipseRarity.Common, "Melee Weapon", "Melee", "Silent and deadly up close.", 60, 95, 0, 0));
        l.Add(new EclipseItemData("ghost_camo", "Ghost Camo", EclipseItemCategory.Cosmetic, EclipseRarity.Epic, 0, "Matte black camouflage skin."));
        l.Add(new EclipseItemData("gold_emblem", "Golden Emblem", EclipseItemCategory.Cosmetic, EclipseRarity.Legendary, 0, "Show off your status with a golden profile emblem."));
        return l;
    }
}

public static class EclipseInventory
{
    public static event Action OnChanged;
    const string P = "EclipseItem_", E = "EclipseEquipped_";
    static readonly Dictionary<string, int> defaults = new Dictionary<string, int>();

    public static void Register(IEnumerable<EclipseItemData> items)
    {
        if (items == null) return;
        foreach (var it in items) if (it != null && !string.IsNullOrEmpty(it.id)) defaults[it.id] = it.defaultQuantity;
    }

    static int Default(string id) { int d; return defaults.TryGetValue(id, out d) ? d : 0; }

    public static int Get(string id) { return PlayerPrefs.GetInt(P + id, Default(id)); }

    public static void Add(string id, int n)
    {
        if (string.IsNullOrEmpty(id) || n == 0) return;
        PlayerPrefs.SetInt(P + id, Mathf.Max(0, Get(id) + n));
        PlayerPrefs.Save();
        if (OnChanged != null) OnChanged();
    }

    public static bool Consume(string id, int n)
    {
        if (Get(id) < n) return false;
        Add(id, -n);
        return true;
    }

    public static string Equipped(EclipseItemCategory c) { return PlayerPrefs.GetString(E + c, ""); }

    public static void Equip(EclipseItemCategory c, string id)
    {
        PlayerPrefs.SetString(E + c, id ?? "");
        PlayerPrefs.Save();
        if (OnChanged != null) OnChanged();
    }
}

// ============================================================ Senjata (Armory)
[Serializable]
public class EclipseWeaponData
{
    public string id = "rifle";
    public string weaponName = "Weapon";
    public string type = "Assault Rifle";
    public Sprite icon;
    [TextArea(2, 4)] public string description = "";
    public int damage = 40, fireRate = 60, accuracy = 70, range = 65, mobility = 55;
    public int startLevel = 1, maxLevel = 10;
    public int baseCost = 1000;
    [Range(0f, .3f)] public float growth = .08f;

    public EclipseWeaponData() { }
    public EclipseWeaponData(string id, string name, string type, int dmg, int rate, int acc, int rng, int mob, int cost, string desc)
    {
        this.id = id; weaponName = name; this.type = type; damage = dmg; fireRate = rate; accuracy = acc; range = rng; mobility = mob; baseCost = cost; description = desc;
    }

    public int Level
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt("EclipseWeaponLv_" + id, startLevel), 1, Mathf.Max(1, maxLevel)); }
        set { PlayerPrefs.SetInt("EclipseWeaponLv_" + id, Mathf.Clamp(value, 1, Mathf.Max(1, maxLevel))); PlayerPrefs.Save(); }
    }

    public int Stat(int baseValue, int level) { return Mathf.RoundToInt(baseValue * (1f + growth * (level - 1))); }
    public int CostFor(int level) { return Mathf.RoundToInt(baseCost * (1f + .6f * (level - 1))); }

    public static List<EclipseWeaponData> CreateDefaults()
    {
        var l = new List<EclipseWeaponData>();
        l.Add(new EclipseWeaponData("ar_vanguard", "VX-9 Vanguard", "Assault Rifle", 42, 68, 72, 66, 55, 1000, "Reliable all-round rifle used by frontline operatives."));
        l.Add(new EclipseWeaponData("smg_hornet", "Hornet SMG", "Submachine Gun", 30, 92, 58, 40, 85, 800, "Extreme fire rate and mobility for close quarters."));
        l.Add(new EclipseWeaponData("sg_breaker", "Breaker-12", "Shotgun", 88, 30, 35, 22, 60, 1200, "Devastating at short range. Clears rooms fast."));
        l.Add(new EclipseWeaponData("sr_longshot", "Longshot SR", "Sniper Rifle", 95, 18, 94, 98, 30, 1500, "Bolt-action precision rifle with extreme range."));
        l.Add(new EclipseWeaponData("pst_sidearm", "S7 Sidearm", "Pistol", 28, 55, 76, 35, 95, 500, "Compact backup weapon. Fast to draw."));
        return l;
    }
}

// ============================================================ Misi (Operations)
[Serializable]
public class EclipseMissionData
{
    public string missionName = "Mission";
    public string chapter = "Chapter 1";
    [TextArea(2, 5)] public string description = "";
    public string[] objectives = new string[3];
    public int recommendedLevel = 1;
    public int rewardCoins = 500, rewardExp = 300, rewardDiamonds = 0;
    public bool locked = false;
    public Sprite artwork;
    [Tooltip("Kosong = pakai Game Scene Name milik layar Operations.")]
    public string sceneName = "";

    public EclipseMissionData() { }
    public EclipseMissionData(string name, string chapter, int lv, int coins, int exp, int dia, bool locked, string desc, params string[] obj)
    {
        missionName = name; this.chapter = chapter; recommendedLevel = lv; rewardCoins = coins; rewardExp = exp; rewardDiamonds = dia;
        this.locked = locked; description = desc; objectives = obj;
    }

    public static List<EclipseMissionData> CreateDefaults()
    {
        var l = new List<EclipseMissionData>();
        l.Add(new EclipseMissionData("THE LOST SIGNAL", "Chapter 1", 1, 500, 300, 0, false,
            "A distress beacon was traced to an abandoned relay station. Secure the area and recover the data core before hostile forces arrive.",
            "Reach the relay station", "Eliminate hostile patrols", "Recover the data core"));
        l.Add(new EclipseMissionData("BLACKOUT", "Chapter 2", 4, 750, 450, 0, false,
            "The city grid has gone dark. Restore power to the district while holding off waves of scavengers.",
            "Restore 3 power nodes", "Defend the generator", "Extract with the engineer"));
        l.Add(new EclipseMissionData("RESEARCH FACILITY", "Chapter 3", 8, 1100, 650, 10, false,
            "Eclipse Corp's research lab holds the key to the signal. Infiltrate, gather intel, and avoid triggering the lockdown.",
            "Infiltrate the lab", "Download research logs", "Escape before lockdown"));
        l.Add(new EclipseMissionData("IRON HARBOR", "Chapter 4", 12, 1500, 900, 15, false,
            "A smuggling ring is moving experimental weapons through the harbor. Shut it down.",
            "Destroy 3 cargo crates", "Neutralize the harbor boss", "Secure the manifest"));
        l.Add(new EclipseMissionData("ECHO PROTOCOL", "Chapter 5", 16, 2200, 1300, 20, true,
            "Unlocks after completing Iron Harbor.", "???", "???", "???"));
        l.Add(new EclipseMissionData("ECLIPSE", "Chapter 6", 20, 3000, 2000, 30, true,
            "Unlocks after completing Echo Protocol.", "???", "???", "???"));
        return l;
    }
}

// ============================================================ Produk (Store)
public enum EclipseStoreTab { Featured, Bundles, Items, Currency }

[Serializable]
public class EclipseProductData
{
    public string id = "product";
    public string productName = "Product";
    [TextArea(1, 3)] public string description = "";
    public EclipseStoreTab tab = EclipseStoreTab.Items;
    public EclipseCurrency currency = EclipseCurrency.Coins;
    public int price = 1000;
    [Tooltip("Khusus Real Money, mis. $0.99")] public string priceLabel = "";
    public string grantItemId = "";
    public int grantItemQty = 1;
    public string grantItemId2 = "";
    public int grantItemQty2 = 1;
    public int grantCoins = 0, grantDiamonds = 0;
    public Sprite icon;
    [Tooltip("Label kecil di pojok kartu, mis. NEW / HOT")] public string tag = "";
    [Range(0, 90)] public int discountPercent = 0;

    public int FinalPrice { get { return Mathf.RoundToInt(price * (100 - discountPercent) / 100f); } }

    static EclipseProductData P(string id, string name, string desc, EclipseStoreTab tab, EclipseCurrency cur, int price)
    {
        var p = new EclipseProductData();
        p.id = id; p.productName = name; p.description = desc; p.tab = tab; p.currency = cur; p.price = price;
        return p;
    }

    public static List<EclipseProductData> CreateDefaults()
    {
        var l = new List<EclipseProductData>();
        var a = P("starter_bundle", "Starter Bundle", "5,000 coins, Medkits and Armor Plates.", EclipseStoreTab.Featured, EclipseCurrency.Diamonds, 300);
        a.grantCoins = 5000; a.grantItemId = "medkit"; a.grantItemQty = 5; a.grantItemId2 = "armor_plate"; a.grantItemQty2 = 3; a.tag = "HOT"; a.discountPercent = 20; l.Add(a);
        var b = P("ghost_camo", "Ghost Camo", "Matte black camouflage skin.", EclipseStoreTab.Featured, EclipseCurrency.Diamonds, 800);
        b.grantItemId = "ghost_camo"; b.tag = "NEW"; l.Add(b);
        var c = P("vest", "Tactical Vest", "Lightweight vest. +15 armor.", EclipseStoreTab.Featured, EclipseCurrency.Coins, 12000);
        c.grantItemId = "vest"; l.Add(c);
        var d = P("helmet", "Recon Helmet", "Integrated optics and armor.", EclipseStoreTab.Featured, EclipseCurrency.Diamonds, 450);
        d.grantItemId = "helmet"; l.Add(d);
        var e = P("gold_emblem", "Golden Emblem", "Legendary profile emblem.", EclipseStoreTab.Featured, EclipseCurrency.Diamonds, 1500);
        e.grantItemId = "gold_emblem"; e.tag = "RARE"; l.Add(e);

        var f = P("medic_pack", "Medic Pack", "10 Medkits and 5 Adrenaline Shots.", EclipseStoreTab.Bundles, EclipseCurrency.Coins, 6000);
        f.grantItemId = "medkit"; f.grantItemQty = 10; f.grantItemId2 = "adrenaline"; f.grantItemQty2 = 5; l.Add(f);
        var g = P("engineer_pack", "Engineer Pack", "Scrap, circuits and energy cells for upgrades.", EclipseStoreTab.Bundles, EclipseCurrency.Coins, 9000);
        g.grantItemId = "scrap"; g.grantItemQty = 200; g.grantItemId2 = "circuit"; g.grantItemQty2 = 20; l.Add(g);
        var h = P("assault_pack", "Assault Pack", "Grenades and armor plates for the frontline.", EclipseStoreTab.Bundles, EclipseCurrency.Diamonds, 120);
        h.grantItemId = "frag"; h.grantItemQty = 10; h.grantItemId2 = "armor_plate"; h.grantItemQty2 = 5; h.discountPercent = 10; l.Add(h);

        var i = P("medkit_x5", "Medkit x5", "Restores HP during missions.", EclipseStoreTab.Items, EclipseCurrency.Coins, 1200);
        i.grantItemId = "medkit"; i.grantItemQty = 5; l.Add(i);
        var j = P("armor_x3", "Armor Plate x3", "Instantly repairs armor.", EclipseStoreTab.Items, EclipseCurrency.Coins, 1800);
        j.grantItemId = "armor_plate"; j.grantItemQty = 3; l.Add(j);
        var k = P("adrenaline_x2", "Adrenaline x2", "Temporary speed boost.", EclipseStoreTab.Items, EclipseCurrency.Coins, 1500);
        k.grantItemId = "adrenaline"; k.grantItemQty = 2; l.Add(k);
        var m = P("frag_x4", "Frag Grenade x4", "Wide blast radius.", EclipseStoreTab.Items, EclipseCurrency.Coins, 1000);
        m.grantItemId = "frag"; m.grantItemQty = 4; l.Add(m);
        var n = P("scrap_x50", "Scrap Metal x50", "Basic crafting material.", EclipseStoreTab.Items, EclipseCurrency.Coins, 800);
        n.grantItemId = "scrap"; n.grantItemQty = 50; l.Add(n);
        var o = P("circuit_x10", "Circuit Board x10", "Advanced crafting material.", EclipseStoreTab.Items, EclipseCurrency.Coins, 2000);
        o.grantItemId = "circuit"; o.grantItemQty = 10; l.Add(o);

        var q = P("dia_500", "500 Diamonds", "Premium currency pack.", EclipseStoreTab.Currency, EclipseCurrency.RealMoney, 0);
        q.priceLabel = "$0.99"; q.grantDiamonds = 500; l.Add(q);
        var r = P("dia_1200", "1,200 Diamonds", "Premium currency pack.", EclipseStoreTab.Currency, EclipseCurrency.RealMoney, 0);
        r.priceLabel = "$1.99"; r.grantDiamonds = 1200; r.tag = "BEST"; l.Add(r);
        var s = P("dia_2600", "2,600 Diamonds", "Premium currency pack.", EclipseStoreTab.Currency, EclipseCurrency.RealMoney, 0);
        s.priceLabel = "$4.99"; s.grantDiamonds = 2600; l.Add(s);
        var t = P("coin_5k", "5,000 Coins", "Exchange diamonds for coins.", EclipseStoreTab.Currency, EclipseCurrency.Diamonds, 50);
        t.grantCoins = 5000; l.Add(t);
        var u = P("coin_25k", "25,000 Coins", "Exchange diamonds for coins.", EclipseStoreTab.Currency, EclipseCurrency.Diamonds, 220);
        u.grantCoins = 25000; u.discountPercent = 10; l.Add(u);
        return l;
    }
}
