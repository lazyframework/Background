using UnityEngine;

// Pengaturan pemain (disimpan di PlayerPrefs). Diterapkan otomatis saat game mulai.
// Baca dari game-mu: EclipseSettings.Sensitivity, EclipseSettings.Music, dst.
public static class EclipseSettings
{
    public const string KMaster = "Eclipse.Master", KMusic = "Eclipse.Music", KSfx = "Eclipse.Sfx",
        KSensitivity = "Eclipse.Sensitivity", KQuality = "Eclipse.Quality", KFps = "Eclipse.Fps", KVSync = "Eclipse.VSync",
        KVibration = "Eclipse.Vibration", KAimAssist = "Eclipse.AimAssist", KDamageNumbers = "Eclipse.DamageNumbers",
        KScreenShake = "Eclipse.ScreenShake", KNotifications = "Eclipse.Notifications",
        KVoice = "Eclipse.Voice", KMuteAll = "Eclipse.MuteAll", KVoiceChat = "Eclipse.VoiceChat", KBgMusic = "Eclipse.BgMusic",
        KAimSensitivity = "Eclipse.AimSensitivity", KInvertY = "Eclipse.InvertY", KAutoPickUp = "Eclipse.AutoPickUp",
        KHeadshotConfirm = "Eclipse.HeadshotConfirm", KResolution = "Eclipse.Resolution", KShadow = "Eclipse.Shadow",
        KAA = "Eclipse.AntiAliasing", KLanguage = "Eclipse.Language";

    static readonly string[] All = { KMaster, KMusic, KSfx, KSensitivity, KQuality, KFps, KVSync, KVibration, KAimAssist, KDamageNumbers, KScreenShake, KNotifications,
        KVoice, KMuteAll, KVoiceChat, KBgMusic, KAimSensitivity, KInvertY, KAutoPickUp, KHeadshotConfirm, KResolution, KShadow, KAA, KLanguage };

    static float F(string k, float d) { return PlayerPrefs.GetFloat(k, d); }
    static void SetF(string k, float v) { PlayerPrefs.SetFloat(k, v); PlayerPrefs.Save(); }
    static bool B(string k, bool d) { return PlayerPrefs.GetInt(k, d ? 1 : 0) == 1; }
    static void SetB(string k, bool v) { PlayerPrefs.SetInt(k, v ? 1 : 0); PlayerPrefs.Save(); }

    public static float Master { get { return F(KMaster, 1f); } set { SetF(KMaster, value); ApplyVolume(); } }
    public static float Music { get { return F(KMusic, .8f); } set { SetF(KMusic, value); } }
    public static float Sfx { get { return F(KSfx, 1f); } set { SetF(KSfx, value); } }
    public static float Sensitivity { get { return F(KSensitivity, .5f); } set { SetF(KSensitivity, value); } }
    public static int Quality { get { return PlayerPrefs.GetInt(KQuality, 2); } set { PlayerPrefs.SetInt(KQuality, value); PlayerPrefs.Save(); ApplyQuality(); } }
    public static int Fps { get { return PlayerPrefs.GetInt(KFps, 60); } set { PlayerPrefs.SetInt(KFps, value); PlayerPrefs.Save(); Application.targetFrameRate = value; } }
    public static bool VSync { get { return B(KVSync, false); } set { SetB(KVSync, value); QualitySettings.vSyncCount = value ? 1 : 0; } }
    public static bool Vibration { get { return B(KVibration, true); } set { SetB(KVibration, value); } }
    public static bool AimAssist { get { return B(KAimAssist, true); } set { SetB(KAimAssist, value); } }
    public static bool DamageNumbers { get { return B(KDamageNumbers, true); } set { SetB(KDamageNumbers, value); } }
    public static bool ScreenShake { get { return B(KScreenShake, true); } set { SetB(KScreenShake, value); } }
    public static float Voice { get { return F(KVoice, .75f); } set { SetF(KVoice, value); } }
    public static bool MuteAll { get { return B(KMuteAll, false); } set { SetB(KMuteAll, value); ApplyVolume(); } }
    public static bool VoiceChat { get { return B(KVoiceChat, true); } set { SetB(KVoiceChat, value); } }
    public static bool BackgroundMusic { get { return B(KBgMusic, true); } set { SetB(KBgMusic, value); } }
    public static float AimSensitivity { get { return F(KAimSensitivity, .5f); } set { SetF(KAimSensitivity, value); } }
    public static bool InvertY { get { return B(KInvertY, false); } set { SetB(KInvertY, value); } }
    public static bool AutoPickUp { get { return B(KAutoPickUp, true); } set { SetB(KAutoPickUp, value); } }
    public static bool HeadshotConfirm { get { return B(KHeadshotConfirm, false); } set { SetB(KHeadshotConfirm, value); } }
    // 0.5 - 1.25 (1 = 100%). Hubungkan ke render scale URP / resolusi layarmu sendiri.
    public static float ResolutionScale { get { return F(KResolution, 1f); } set { SetF(KResolution, Mathf.Clamp(value, .5f, 1.25f)); } }
    // 0 Low, 1 Medium, 2 High
    public static int ShadowDetail { get { return PlayerPrefs.GetInt(KShadow, 2); } set { PlayerPrefs.SetInt(KShadow, value); PlayerPrefs.Save(); ApplyShadows(); } }
    // 0 Off, 1 FXAA, 2 TAA (diterapkan sebagai MSAA 0x / 2x / 4x; sambungkan ke post-processing-mu kalau perlu)
    public static int AntiAliasing { get { return PlayerPrefs.GetInt(KAA, 2); } set { PlayerPrefs.SetInt(KAA, value); PlayerPrefs.Save(); ApplyAA(); } }
    public static string Language { get { return PlayerPrefs.GetString(KLanguage, "English"); } set { PlayerPrefs.SetString(KLanguage, value ?? "English"); PlayerPrefs.Save(); } }
    public static bool Notifications { get { return B(KNotifications, true); } set { SetB(KNotifications, value); } }

    static void ApplyVolume() { AudioListener.volume = MuteAll ? 0f : Master; }

    static void ApplyShadows()
    {
        int s = Mathf.Clamp(ShadowDetail, 0, 2);
        QualitySettings.shadows = s == 0 ? ShadowQuality.HardOnly : ShadowQuality.All;
        QualitySettings.shadowResolution = s == 0 ? ShadowResolution.Low : (s == 1 ? ShadowResolution.Medium : ShadowResolution.High);
    }

    static void ApplyAA()
    {
        int a = Mathf.Clamp(AntiAliasing, 0, 2);
        QualitySettings.antiAliasing = a == 0 ? 0 : (a == 1 ? 2 : 4);
    }

    static void ApplyQuality()
    {
        int n = QualitySettings.names.Length;
        if (n > 0) QualitySettings.SetQualityLevel(Mathf.RoundToInt(Mathf.Clamp(Quality, 0, 3) / 3f * (n - 1)), true);
    }

    public static void ApplyAll()
    {
        ApplyVolume();
        if (PlayerPrefs.HasKey(KQuality)) ApplyQuality();
        if (PlayerPrefs.HasKey(KShadow)) ApplyShadows();
        if (PlayerPrefs.HasKey(KAA)) ApplyAA();
        if (PlayerPrefs.HasKey(KFps)) Application.targetFrameRate = Fps;
        if (PlayerPrefs.HasKey(KVSync)) QualitySettings.vSyncCount = VSync ? 1 : 0;
    }

    public static void ResetAll()
    {
        foreach (var k in All) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        AudioListener.volume = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() { ApplyAll(); }
}
