using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MonsterHealth : MonoBehaviour
{
    // Number of living monsters. Used by the wave system.
    public static int ActiveCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { ActiveCount = 0; }

    [Header("Monster")]
    [SerializeField] private int maxHealth = 100;

    [Header("Optional UI / effects")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private GameObject defeatedEffect;
    [SerializeField] private Transform healthBarRoot;   // billboarded towards the camera
    [SerializeField] private Transform healthBarFill;   // scaled on X (pivot on the left)
    [SerializeField] private Color hitFlashColor = Color.white;

    private int currentHealth;
    private bool defeated;
    private bool counted;
    private Transform cam;
    private readonly List<Renderer> flashRenderers = new List<Renderer>();
    private readonly List<Color> baseColors = new List<Color>();

    private void Awake()
    {
        ActiveCount++;
        counted = true;
        currentHealth = maxHealth;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (healthBarRoot != null && r.transform.IsChildOf(healthBarRoot)) continue;
            if (r is ParticleSystemRenderer) continue;
            Material shared = r.sharedMaterial;
            if (shared == null) continue;
            if (!shared.HasProperty("_Color") && !shared.HasProperty("_BaseColor")) continue;

            flashRenderers.Add(r);
            baseColors.Add(r.material.color); // creates a per-monster material instance
        }
    }

    private void Start()
    {
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        UpdateHealthUI();
    }

    private void LateUpdate()
    {
        if (healthBarRoot != null && cam != null)
            healthBarRoot.rotation = cam.rotation;
    }

    private void OnDestroy() { Release(); }

    // ---------- Setup (called by the spawner / bootstrap) ----------

    public void SetCamera(Transform cameraTransform) { cam = cameraTransform; }

    public void ConfigureHealthBar(Transform root, Transform fill)
    {
        healthBarRoot = root;
        healthBarFill = fill;
    }

    public void ScaleHealth(float multiplier)
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * multiplier));
        currentHealth = maxHealth;
        UpdateHealthUI();
    }

    // ---------- Gameplay ----------

    public void TakeDamage(int damage)
    {
        if (defeated || damage <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        UpdateHealthUI();

        if (currentHealth == 0)
        {
            Defeat();
        }
        else
        {
            Sfx.Hit();
            StopCoroutine(nameof(HitFlash));
            StartCoroutine(nameof(HitFlash));
        }
    }

    // Removes the monster immediately (used when restarting).
    public void Despawn()
    {
        Release();
        Destroy(gameObject);
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (healthBarFill != null)
        {
            float ratio = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
            healthBarFill.localScale = new Vector3(Mathf.Clamp01(ratio), 1f, 1f);
        }
    }

    private void Defeat()
    {
        defeated = true;
        Release();

        if (GameManager.Instance != null)
            GameManager.Instance.MonsterDefeated();

        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        MonsterAI ai = GetComponent<MonsterAI>();
        if (ai != null) ai.enabled = false;

        if (defeatedEffect != null)
            Instantiate(defeatedEffect, transform.position, Quaternion.identity);

        StopCoroutine(nameof(HitFlash));
        StartCoroutine(ShrinkAndDestroy());
    }

    private IEnumerator HitFlash()
    {
        for (int i = 0; i < flashRenderers.Count; i++)
            if (flashRenderers[i] != null) flashRenderers[i].material.color = hitFlashColor;

        yield return new WaitForSeconds(0.08f);

        for (int i = 0; i < flashRenderers.Count; i++)
            if (flashRenderers[i] != null) flashRenderers[i].material.color = baseColors[i];
    }

    private IEnumerator ShrinkAndDestroy()
    {
        Vector3 start = transform.localScale;
        const float duration = 0.3f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, Vector3.zero, t / duration);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void Release()
    {
        if (!counted) return;
        counted = false;
        ActiveCount = Mathf.Max(0, ActiveCount - 1);
    }
}
