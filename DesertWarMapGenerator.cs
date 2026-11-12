using UnityEngine;

/// Generator map gurun WW2 (4x4 km) untuk Unity.
/// Cara pakai: GameObject > 3D Object > Terrain, tempel script ini,
/// klik kanan komponen > "Generate Map".
[RequireComponent(typeof(Terrain))]
[RequireComponent(typeof(TerrainCollider))]
public class DesertWarMapGenerator : MonoBehaviour
{
    [Header("Ukuran")]
    public int resolution = 1025;
    public Vector3 size = new Vector3(4000f, 200f, 4000f);
    public int seed = 1942;

    [Header("Tekstur (opsional): 0=pasir, 1=batu/tebing, 2=tanah kering")]
    public TerrainLayer[] layers;

    [Header("Fitur")]
    public bool buildStructures = true;

    // Zona datar (posisi 0..1, radius 0..1, tinggi relatif)
    struct Zone { public string name; public Vector2 pos; public float radius; public float h; }
    Zone[] zones = {
        new Zone{ name="BaseSekutu",  pos=new Vector2(0.12f,0.20f), radius=0.06f, h=0.09f },
        new Zone{ name="BasePoros",   pos=new Vector2(0.88f,0.80f), radius=0.06f, h=0.09f },
        new Zone{ name="LapanganUdaraA", pos=new Vector2(0.78f,0.22f), radius=0.07f, h=0.08f },
        new Zone{ name="LapanganUdaraB", pos=new Vector2(0.22f,0.78f), radius=0.07f, h=0.08f },
        new Zone{ name="DesaTengah",  pos=new Vector2(0.50f,0.50f), radius=0.05f, h=0.06f },
    };

    Material matSand, matMetal, matWood;

    [ContextMenu("Generate Map")]
    public void Generate()
    {
        Random.InitState(seed);
        Terrain terrain = GetComponent<Terrain>();
        TerrainData data = new TerrainData();
        data.heightmapResolution = resolution;
        data.size = size;
        terrain.terrainData = data;
        GetComponent<TerrainCollider>().terrainData = data;

        BuildHeights(data);
        PaintTextures(data);

        // bersihkan hasil generate sebelumnya
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        if (buildStructures) BuildStructures(terrain);
        Debug.Log("Map gurun WW2 selesai dibuat.");
    }

    // ---------- TERRAIN ----------
    void BuildHeights(TerrainData data)
    {
        float[,] hm = new float[resolution, resolution];
        float ox = Random.value * 1000f, oy = Random.value * 1000f;

        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float nx = (float)x / (resolution - 1);
            float ny = (float)y / (resolution - 1);

            float h = 0.08f;

            // 1. Bukit pasir memanjang searah angin (peregangan sumbu)
            float dunes = Mathf.PerlinNoise(ox + nx * 5f, oy + ny * 22f) * 0.5f
                        + Mathf.PerlinNoise(ox + nx * 12f, oy + ny * 45f) * 0.25f
                        + Mathf.PerlinNoise(ox + nx * 40f, oy + ny * 40f) * 0.08f;
            h += dunes * 0.12f;

            // 2. Punggungan/escarpment diagonal (seperti Ruweisat Ridge)
            float dRidge = DistToSegment(new Vector2(nx, ny), new Vector2(0.15f, 0.62f), new Vector2(0.85f, 0.38f));
            float ridge = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(dRidge / 0.05f));
            h += ridge * 0.30f * (0.7f + 0.3f * Mathf.PerlinNoise(nx * 30f, ny * 30f));

            // 3. Wadi (parit kering) berkelok
            float wadiY = 0.35f + 0.10f * Mathf.Sin(nx * 9f) + 0.04f * Mathf.Sin(nx * 23f);
            float wadi = Mathf.Exp(-Mathf.Pow((ny - wadiY) / 0.012f, 2f));
            h -= wadi * 0.06f;

            // 4. Cekungan oasis
            float dOasis = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.50f, 0.55f));
            h -= Mathf.Exp(-Mathf.Pow(dOasis / 0.04f, 2f)) * 0.05f;

            // 5. Ratakan zona base/lapangan udara/desa
            foreach (var z in zones)
            {
                float d = Vector2.Distance(new Vector2(nx, ny), z.pos);
                float t = 1f - Mathf.SmoothStep(z.radius, z.radius * 1.8f, d);
                h = Mathf.Lerp(h, z.h, t);
            }

            hm[y, x] = Mathf.Clamp01(h);
        }
        data.SetHeights(0, 0, hm);
    }

    void PaintTextures(TerrainData data)
    {
        if (layers == null || layers.Length == 0) return;
        data.terrainLayers = layers;
        int res = data.alphamapResolution;
        int n = layers.Length;
        float[,,] map = new float[res, res, n];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float nx = (float)x / (res - 1), ny = (float)y / (res - 1);
            float slope = data.GetSteepness(nx, ny);
            float wadiY = 0.35f + 0.10f * Mathf.Sin(nx * 9f) + 0.04f * Mathf.Sin(nx * 23f);
            bool inWadi = Mathf.Abs(ny - wadiY) < 0.015f;

            int idx = 0;
            if (slope > 28f && n > 1) idx = 1;
            else if (inWadi && n > 2) idx = 2;
            map[y, x, idx] = 1f;
        }
        data.SetAlphamaps(0, 0, map);
    }

    // ---------- STRUKTUR ----------
    void BuildStructures(Terrain t)
    {
        matSand  = MakeMat(new Color(0.76f, 0.65f, 0.45f));
        matMetal = MakeMat(new Color(0.35f, 0.37f, 0.30f));
        matWood  = MakeMat(new Color(0.40f, 0.30f, 0.20f));

        // Pangkalan + spawn point
        BuildBase(t, "Sekutu", zones[0].pos);
        BuildBase(t, "Poros",  zones[1].pos);

        // Desa tengah (gaya kota Afrika Utara)
        BuildVillage(t, zones[4].pos, 5, 4, "DesaTengah");
        // Desa kecil di sisi punggungan
        BuildVillage(t, new Vector2(0.30f, 0.50f), 3, 3, "DesaBarat");
        BuildVillage(t, new Vector2(0.72f, 0.58f), 3, 3, "DesaTimur");

        // Lapangan udara: landasan + hanggar
        BuildAirfield(t, zones[2].pos, "LapanganUdaraA");
        BuildAirfield(t, zones[3].pos, "LapanganUdaraB");

        // Garis bunker + karung pasir di sepanjang punggungan
        for (int i = 0; i < 24; i++)
        {
            float k = i / 23f;
            Vector2 p = Vector2.Lerp(new Vector2(0.18f, 0.58f), new Vector2(0.82f, 0.40f), k);
            p += new Vector2(Random.Range(-0.01f, 0.01f), Random.Range(-0.03f, 0.03f));
            Bunker(t, p);
        }

        // Bangkai tank & kendaraan tersebar di medan terbuka
        for (int i = 0; i < 60; i++)
            TankWreck(t, new Vector2(Random.Range(0.1f, 0.9f), Random.Range(0.1f, 0.9f)));

        // Titik capture
        Vector2[] caps = {
            new Vector2(0.50f,0.50f), new Vector2(0.50f,0.55f), new Vector2(0.30f,0.50f),
            new Vector2(0.72f,0.58f), new Vector2(0.78f,0.22f), new Vector2(0.22f,0.78f),
            new Vector2(0.50f,0.35f)
        };
        string[] names = { "Desa","Oasis","BaratPunggungan","TimurPunggungan","LapanganA","LapanganB","Wadi" };
        for (int i = 0; i < caps.Length; i++) CapturePoint(t, caps[i], "Capture_" + names[i]);
    }

    void BuildBase(Terrain t, string team, Vector2 c)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 p = c + new Vector2(Random.Range(-0.025f, 0.025f), Random.Range(-0.025f, 0.025f));
            GameObject sp = new GameObject("Spawn_" + team + "_" + i);
            sp.transform.parent = transform;
            sp.transform.position = WorldPos(t, p) + Vector3.up * 0.5f;
            sp.tag = "Untagged";
        }
        // tenda & tembok karung pasir keliling
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.035f;
            Box("Tenda_" + team, WorldPos(t, p), new Vector3(6, 3, 8), matWood, a * Mathf.Rad2Deg);
        }
    }

    void BuildVillage(Terrain t, Vector2 c, int cols, int rows, string name)
    {
        float step = 40f;
        Vector3 origin = WorldPos(t, c) - new Vector3(cols * step / 2f, 0, rows * step / 2f);
        for (int i = 0; i < cols; i++)
        for (int j = 0; j < rows; j++)
        {
            if (Random.value < 0.15f) continue; // celah jalan/lapangan
            Vector3 pos = origin + new Vector3(i * step, 0, j * step);
            pos.y = t.SampleHeight(pos) + t.transform.position.y;
            float w = Random.Range(10f, 18f), d = Random.Range(10f, 18f), h = Random.Range(4f, 9f);
            GameObject b = Box(name + "_Bangunan", pos + Vector3.up * h / 2f, new Vector3(w, h, d), matSand, Random.Range(-8f, 8f));
            // tembok halaman
            Box(name + "_Tembok", pos + new Vector3(w, 1.2f, 0), new Vector3(0.6f, 2.4f, d), matSand, 0);
        }
    }

    void BuildAirfield(Terrain t, Vector2 c, string name)
    {
        Vector3 p = WorldPos(t, c);
        Box(name + "_Landasan", p + Vector3.up * 0.1f, new Vector3(600, 0.2f, 30), MakeMat(new Color(0.25f,0.25f,0.23f)), 0);
        for (int i = 0; i < 3; i++)
            Box(name + "_Hanggar", p + new Vector3(-120 + i * 120, 5f, 50), new Vector3(40, 10, 30), matMetal, 0);
        Box(name + "_MenaraKontrol", p + new Vector3(0, 8f, -50), new Vector3(8, 16, 8), matSand, 0);
    }

    void Bunker(Terrain t, Vector2 p)
    {
        Vector3 w = WorldPos(t, p);
        Box("Bunker", w + Vector3.up * 1.2f, new Vector3(8, 2.4f, 6), matSand, Random.Range(0, 360));
        for (int i = 0; i < 3; i++)
            Box("KarungPasir", w + new Vector3(i * 3 - 3, 0.6f, -6), new Vector3(3, 1.2f, 1), matSand, 0);
    }

    void TankWreck(Terrain t, Vector2 p)
    {
        Vector3 w = WorldPos(t, p);
        float yaw = Random.Range(0, 360);
        Box("BangkaiTank", w + Vector3.up * 1.1f, new Vector3(3.2f, 2.2f, 6.5f), matMetal, yaw);
        Box("BangkaiTank_Turret", w + Vector3.up * 2.6f, new Vector3(2.2f, 1f, 2.5f), matMetal, yaw);
    }

    void CapturePoint(Terrain t, Vector2 p, string name)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = name;
        c.transform.parent = transform;
        c.transform.localScale = new Vector3(30, 0.2f, 30);
        c.transform.position = WorldPos(t, p) + Vector3.up * 0.2f;
        c.GetComponent<Renderer>().sharedMaterial = MakeMat(new Color(1f, 0.8f, 0.1f, 1f));
        c.GetComponent<Collider>().isTrigger = true; // pakai untuk logika capture
    }

    // ---------- HELPER ----------
    Vector3 WorldPos(Terrain t, Vector2 n)
    {
        float x = n.x * size.x, z = n.y * size.z;
        Vector3 pos = t.transform.position + new Vector3(x, 0, z);
        pos.y = t.SampleHeight(pos) + t.transform.position.y;
        return pos;
    }

    GameObject Box(string name, Vector3 pos, Vector3 scale, Material m, float yaw)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.parent = transform;
        g.transform.position = pos;
        g.transform.localScale = scale;
        g.transform.rotation = Quaternion.Euler(0, yaw, 0);
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    Material MakeMat(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        Material m = new Material(s);
        m.color = c;
        return m;
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}
