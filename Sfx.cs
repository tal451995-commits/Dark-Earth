using UnityEngine;

// Tiny procedural sound effects so the prototype has audio feedback
// without needing any audio assets. Replace with real clips later.
public static class Sfx
{
    private static AudioSource source;
    private static AudioClip shot, hit, hurt, kill, reload;

    public static void Shot()   { if (shot == null)   shot   = Make("shot",   0.15f, 520f, 90f,  0.55f, 0.45f); Play(shot); }
    public static void Hit()    { if (hit == null)    hit    = Make("hit",    0.10f, 300f, 160f, 0.35f, 0.55f); Play(hit); }
    public static void Hurt()   { if (hurt == null)   hurt   = Make("hurt",   0.25f, 140f, 60f,  0.30f, 0.70f); Play(hurt); }
    public static void Kill()   { if (kill == null)   kill   = Make("kill",   0.30f, 200f, 700f, 0.10f, 0.50f); Play(kill); }
    public static void Reload() { if (reload == null) reload = Make("reload", 0.12f, 900f, 500f, 0.05f, 0.40f); Play(reload); }

    private static AudioClip Make(string name, float duration, float f0, float f1, float noise, float volume)
    {
        const int rate = 22050;
        int n = Mathf.Max(1, (int)(rate * duration));
        float[] data = new float[n];
        float phase = 0f;
        System.Random rnd = new System.Random(name.GetHashCode());

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = Mathf.Lerp(f0, f1, t);
            phase += 2f * Mathf.PI * f / rate;
            float tone = Mathf.Sin(phase);
            float nz = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float env = (1f - t) * (1f - t);
            data[i] = Mathf.Lerp(tone, nz, noise) * env * volume;
        }

        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static void Play(AudioClip clip)
    {
        if (source == null)
        {
            GameObject go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.playOnAwake = false;
        }
        source.PlayOneShot(clip);
    }
}
