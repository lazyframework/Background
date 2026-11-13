using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// Cek internet SUNGGUHAN (bukan sekadar "ada WiFi/data seluler").
// Mengirim request ke endpoint generate_204 dan hanya menganggap online kalau server membalas HTTP 204.
// WiFi tanpa internet, data habis, atau captive portal (halaman login WiFi hotel/kafe, yang membalas 200 + HTML) dianggap OFFLINE.
//
// Pemakaian di coroutine:
//   bool ok = false;
//   yield return EclipseInternet.Check(r => ok = r);
public static class EclipseInternet
{
    // Dicoba berurutan; cukup satu yang membalas 204.
    public static string[] Endpoints =
    {
        "https://clients3.google.com/generate_204",
        "https://www.gstatic.com/generate_204",
        "https://cp.cloudflare.com/generate_204"
    };

    // Batas waktu per endpoint (detik).
    public static int TimeoutSeconds = 3;

    public static IEnumerator Check(Action<bool> done)
    {
        // Tidak ada koneksi sama sekali -> tidak perlu request
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            if (done != null) done(false);
            yield break;
        }

        for (int i = 0; i < Endpoints.Length; i++)
        {
            // parameter acak supaya tidak diambil dari cache
            string url = Endpoints[i] + "?t=" + DateTime.UtcNow.Ticks;
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = TimeoutSeconds;
                req.SetRequestHeader("Cache-Control", "no-cache");
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && req.responseCode == 204)
                {
                    if (done != null) done(true);
                    yield break;
                }
            }
        }
        if (done != null) done(false);
    }
}
