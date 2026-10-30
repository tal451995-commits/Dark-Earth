using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Ready, Playing, GameOver }

    [Header("HUD (optional - DarkEarthBootstrap can build it for you)")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private RectTransform playerHealthFill;
    [SerializeField] private Image damageFlash;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverText;

    [Header("Game values")]
    [SerializeField] private int startingAmmo = 12;
    [SerializeField] private int pointsPerMonster = 100;
    [SerializeField] private int ammoPerKill = 2;
    [SerializeField] private int playerMaxHealth = 100;
    [SerializeField] private float reloadTime = 1f;

    [Header("Language")]
    [SerializeField] private bool hebrewTexts = false;

    public event Action OnRestart;

    public GameState State { get; private set; } = GameState.Ready;
    public bool IsPlaying => State == GameState.Playing;
    public bool IsReloading { get; private set; }
    public int Ammo { get; private set; }
    public int Score { get; private set; }
    public int PlayerHealth { get; private set; }
    public int Wave { get; private set; }

    private float flashAlpha;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this); // destroy only the duplicate component, never the whole object
            return;
        }
        Instance = this;
        if (hebrewTexts) Loc.Hebrew = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        ResetValues();
        State = GameState.Ready;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateHUD();
        SetStatus(Loc.MissionReady);
    }

    private void Update()
    {
        if (damageFlash != null && flashAlpha > 0f)
        {
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.deltaTime * 1.5f);
            Color c = damageFlash.color;
            c.a = flashAlpha;
            damageFlash.color = c;
        }
    }

    // Lets DarkEarthBootstrap hand over the runtime-built HUD.
    public void BindHud(TMP_Text status, TMP_Text score, TMP_Text ammo, TMP_Text health, TMP_Text wave,
                        RectTransform healthFill, Image flash, GameObject overPanel, TMP_Text overText)
    {
        statusText = status; scoreText = score; ammoText = ammo; healthText = health; waveText = wave;
        playerHealthFill = healthFill; damageFlash = flash; gameOverPanel = overPanel; gameOverText = overText;
        UpdateHUD();
    }

    // ---------- Flow ----------

    public void BeginHunt()
    {
        if (State == GameState.Playing) return;
        State = GameState.Playing;
        SetStatus(Loc.HuntBegins);
    }

    public void RestartGame()
    {
        foreach (MonsterHealth m in FindObjectsByType<MonsterHealth>(FindObjectsSortMode.None))
            m.Despawn();

        ResetValues();
        State = GameState.Ready;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateHUD();
        SetStatus(Loc.MissionReady);

        if (OnRestart != null) OnRestart.Invoke();
    }

    private void ResetValues()
    {
        StopAllCoroutines();
        IsReloading = false;
        Ammo = startingAmmo;
        Score = 0;
        Wave = 0;
        PlayerHealth = playerMaxHealth;
        flashAlpha = 0f;
        if (damageFlash != null)
        {
            Color c = damageFlash.color;
            c.a = 0f;
            damageFlash.color = c;
        }
    }

    private void GameOver()
    {
        State = GameState.GameOver;
        string msg = Loc.GameOver(Score, Wave);
        SetStatus(msg.Replace("\n", " | "));
        if (gameOverText != null) gameOverText.text = msg;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // ---------- Ammo ----------

    public bool TryUseAmmo()
    {
        if (!IsPlaying || IsReloading) return false;

        if (Ammo <= 0)
        {
            SetStatus(Loc.NoAmmo);
            return false;
        }

        Ammo--;
        UpdateHUD();
        if (Ammo == 0) SetStatus(Loc.NoAmmo);
        return true;
    }

    public void Reload()
    {
        if (!IsPlaying || IsReloading || Ammo >= startingAmmo) return;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        IsReloading = true;
        Sfx.Reload();
        SetStatus(Loc.Reloading);
        yield return new WaitForSeconds(reloadTime);
        Ammo = startingAmmo;
        IsReloading = false;
        UpdateHUD();
        SetStatus(Loc.Reloaded);
    }

    // ---------- Events from the world ----------

    public void WaveStarted(int wave)
    {
        Wave = wave;
        UpdateHUD();
        SetStatus(Loc.WaveStart(wave));
    }

    public void WaveCleared(int wave)
    {
        SetStatus(Loc.WaveCleared(wave));
    }

    public void MonsterSpawned()
    {
        SetStatus(Loc.MonsterSpotted);
    }

    public void MonsterDefeated()
    {
        Score += pointsPerMonster;
        Ammo = Mathf.Min(startingAmmo, Ammo + ammoPerKill);
        Sfx.Kill();
        UpdateHUD();
        SetStatus(Loc.Defeated(pointsPerMonster));
    }

    public void DamagePlayer(int damage)
    {
        if (!IsPlaying || damage <= 0) return;

        PlayerHealth = Mathf.Max(0, PlayerHealth - damage);
        flashAlpha = 0.6f;
        Sfx.Hurt();
        Handheld.Vibrate(); // no-op in the Editor / on devices without a vibrator
        UpdateHUD();

        if (PlayerHealth == 0) GameOver();
    }

    // ---------- HUD ----------

    private void UpdateHUD()
    {
        if (scoreText != null) scoreText.text = Loc.ScoreLabel(Score);
        if (ammoText != null) ammoText.text = Loc.AmmoLabel(Ammo, startingAmmo);
        if (healthText != null) healthText.text = Loc.HealthLabel(PlayerHealth);
        if (waveText != null) waveText.text = Loc.WaveLabel(Wave);

        if (playerHealthFill != null)
        {
            float ratio = playerMaxHealth > 0 ? (float)PlayerHealth / playerMaxHealth : 0f;
            playerHealthFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log(message);
    }
}
