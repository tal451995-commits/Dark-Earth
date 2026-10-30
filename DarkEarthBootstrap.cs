using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.XR.ARFoundation;

// One-component setup: add this to the "GameSystems" object and press Play / build.
// It adds the other game components, builds a placeholder monster and the whole HUD
// (score, ammo, wave, health, FIRE / RELOAD / HUNT buttons, crosshair, game-over screen).
// Anything you assign yourself in the Inspector (monster prefab, sun, font) is used instead.
[DefaultExecutionOrder(-100)]
public class DarkEarthBootstrap : MonoBehaviour
{
    [Header("Optional - auto-detected when empty")]
    [SerializeField] private Camera arCamera;
    [SerializeField] private GameObject monsterPrefab;   // your own monster; empty = built-in placeholder
    [SerializeField] private Light sceneSun;

    [Header("Options")]
    [SerializeField] private bool buildHud = true;
    [SerializeField] private bool autoStartHunt = false; // false = player presses HUNT
    [SerializeField] private bool hebrewTexts = false;   // needs a Hebrew font, see README
    [SerializeField] private TMP_FontAsset hudFont;

    private GameManager gm;
    private MonsterSpawner spawner;
    private WeaponController weapon;
    private FireButton fireButton;
    private EnvironmentController envController;
    private GameObject huntButtonObject;

    private void Awake()
    {
        Loc.Hebrew = hebrewTexts;

        arCamera = ResolveCamera();
        if (arCamera == null)
        {
            Debug.LogError("DarkEarthBootstrap: no camera found. Add an XR Origin with an AR Camera.");
            enabled = false;
            return;
        }

#if UNITY_EDITOR
        if (arCamera.GetComponent<EditorAimHelper>() == null)
            arCamera.gameObject.AddComponent<EditorAimHelper>();
#endif

        gm = GetOrAdd<GameManager>();
        envController = GetOrAdd<EnvironmentController>();
        envController.Configure(sceneSun, null);

        if (monsterPrefab == null) monsterPrefab = BuildDefaultMonster();

        spawner = GetOrAdd<MonsterSpawner>();
        spawner.Configure(monsterPrefab, arCamera, FindFirstObjectByType<ARRaycastManager>(), envController);

        weapon = GetOrAdd<WeaponController>();
        weapon.Configure(arCamera);

        fireButton = GetOrAdd<FireButton>();
        fireButton.Configure(weapon);

        if (buildHud) BuildHud();
    }

    private void Start()
    {
        if (autoStartHunt)
        {
            if (huntButtonObject != null) huntButtonObject.SetActive(false);
            spawner.StartHunt();
        }
    }

    private T GetOrAdd<T>() where T : Component
    {
        T c = GetComponent<T>();
        if (c == null) c = gameObject.AddComponent<T>();
        return c;
    }

    private Camera ResolveCamera()
    {
        if (arCamera != null) return arCamera;
        if (Camera.main != null) return Camera.main;

        ARCameraManager manager = FindFirstObjectByType<ARCameraManager>();
        if (manager != null) return manager.GetComponent<Camera>();

        return FindFirstObjectByType<Camera>();
    }

    // ------------------------------------------------------------------
    // Placeholder monster (capsule body, glowing eyes, floating health bar)
    // The root pivot is at the monster's feet. Eyes are on the +Z (front) side.
    // ------------------------------------------------------------------
    private GameObject BuildDefaultMonster()
    {
        GameObject root = new GameObject("DefaultMonsterTemplate");
        root.transform.SetParent(transform, false);
        root.SetActive(false); // template only; spawned copies are activated by the spawner

        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.6f, 0f);
        col.height = 1.2f;
        col.radius = 0.38f;

        GameObject body = MakePrimitive(PrimitiveType.Capsule, root.transform, "Body",
            new Vector3(0f, 0.6f, 0f), new Vector3(0.75f, 0.6f, 0.75f), new Color(0.55f, 0.08f, 0.10f));

        MakePrimitive(PrimitiveType.Sphere, root.transform, "EyeL",
            new Vector3(-0.14f, 0.85f, 0.31f), Vector3.one * 0.14f, new Color(1f, 0.9f, 0.2f));
        MakePrimitive(PrimitiveType.Sphere, root.transform, "EyeR",
            new Vector3(0.14f, 0.85f, 0.31f), Vector3.one * 0.14f, new Color(1f, 0.9f, 0.2f));

        // Floating health bar (fill pivot is on the left edge)
        GameObject barRoot = new GameObject("HealthBar");
        barRoot.transform.SetParent(root.transform, false);
        barRoot.transform.localPosition = new Vector3(0f, 1.45f, 0f);

        MakePrimitive(PrimitiveType.Cube, barRoot.transform, "BarBack",
            Vector3.zero, new Vector3(0.84f, 0.1f, 0.02f), new Color(0.1f, 0.1f, 0.1f));

        GameObject fillPivot = new GameObject("BarFillPivot");
        fillPivot.transform.SetParent(barRoot.transform, false);
        fillPivot.transform.localPosition = new Vector3(-0.4f, 0f, -0.015f);

        MakePrimitive(PrimitiveType.Cube, fillPivot.transform, "BarFill",
            new Vector3(0.4f, 0f, 0f), new Vector3(0.8f, 0.07f, 0.02f), new Color(0.2f, 0.9f, 0.25f));

        MonsterHealth health = root.AddComponent<MonsterHealth>();
        health.ConfigureHealthBar(barRoot.transform, fillPivot.transform);
        root.AddComponent<MonsterAI>();

        return root;
    }

    private static GameObject MakePrimitive(PrimitiveType type, Transform parent, string name,
                                            Vector3 localPos, Vector3 localScale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider c = go.GetComponent<Collider>();
        if (c != null) DestroyImmediate(c); // only the root capsule collider is used for hits
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().material.color = color;
        return go;
    }

    // ------------------------------------------------------------------
    // HUD
    // ------------------------------------------------------------------
    private void BuildHud()
    {
        EnsureEventSystem();

        GameObject canvasGo = new GameObject("DarkEarthHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        Transform root = canvasGo.transform;
        Vector2 topLeft = new Vector2(0f, 1f), topRight = new Vector2(1f, 1f), topMid = new Vector2(0.5f, 1f);
        Vector2 botLeft = new Vector2(0f, 0f), botRight = new Vector2(1f, 0f), botMid = new Vector2(0.5f, 0f);
        Vector2 mid = new Vector2(0.5f, 0.5f);

        // Damage flash (full screen)
        Image flash = CreateImage(root, "DamageFlash", new Color(1f, 0f, 0f, 0f), mid, mid, Vector2.zero, Vector2.zero);
        flash.rectTransform.anchorMin = Vector2.zero;
        flash.rectTransform.anchorMax = Vector2.one;
        flash.rectTransform.offsetMin = Vector2.zero;
        flash.rectTransform.offsetMax = Vector2.zero;

        // Crosshair
        CreateImage(root, "CrosshairH", new Color(1f, 1f, 1f, 0.85f), mid, mid, Vector2.zero, new Vector2(56f, 4f));
        CreateImage(root, "CrosshairV", new Color(1f, 1f, 1f, 0.85f), mid, mid, Vector2.zero, new Vector2(4f, 56f));

        // Top HUD
        TMP_Text score = CreateText(root, "Score", "", 44, TextAlignmentOptions.TopLeft, topLeft, topLeft, new Vector2(30f, -90f), new Vector2(520f, 60f));
        TMP_Text ammo = CreateText(root, "Ammo", "", 44, TextAlignmentOptions.TopLeft, topLeft, topLeft, new Vector2(30f, -155f), new Vector2(520f, 60f));
        TMP_Text wave = CreateText(root, "Wave", "", 44, TextAlignmentOptions.TopRight, topRight, topRight, new Vector2(-30f, -90f), new Vector2(520f, 60f));
        TMP_Text status = CreateText(root, "Status", "", 42, TextAlignmentOptions.Top, topMid, topMid, new Vector2(0f, -240f), new Vector2(1000f, 130f));
        TMP_Text env = CreateText(root, "Environment", "", 30, TextAlignmentOptions.Top, topMid, topMid, new Vector2(0f, -375f), new Vector2(1000f, 50f));
        envController.Configure(null, env);

        // Player health bar (bottom centre)
        Image healthBack = CreateImage(root, "HealthBack", new Color(0f, 0f, 0f, 0.6f), botMid, botMid, new Vector2(0f, 330f), new Vector2(360f, 36f));
        Image healthFillImg = CreateImage(healthBack.transform, "HealthFill", new Color(0.85f, 0.15f, 0.15f, 0.95f), mid, mid, Vector2.zero, Vector2.zero);
        RectTransform fillRect = healthFillImg.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        TMP_Text health = CreateText(healthBack.transform, "HealthText", "", 28, TextAlignmentOptions.Center, mid, mid, Vector2.zero, new Vector2(360f, 36f));

        // Buttons
        CreateButton(root, "FireButton", Loc.Fire, new Color(0.8f, 0.1f, 0.1f, 0.85f), botRight, botRight,
                     new Vector2(-50f, 120f), new Vector2(300f, 300f), 64, fireButton.OnFirePressed);
        CreateButton(root, "ReloadButton", Loc.Reload, new Color(0.15f, 0.35f, 0.75f, 0.85f), botLeft, botLeft,
                     new Vector2(50f, 120f), new Vector2(240f, 160f), 40, gm.Reload);

        Button hunt = CreateButton(root, "HuntButton", Loc.Hunt, new Color(0.1f, 0.6f, 0.2f, 0.9f), botMid, botMid,
                     new Vector2(0f, 120f), new Vector2(300f, 160f), 48, null);
        huntButtonObject = hunt.gameObject;
        hunt.onClick.AddListener(() => { spawner.StartHunt(); huntButtonObject.SetActive(false); });

        // Game over panel
        Image panelImg = CreateImage(root, "GameOverPanel", new Color(0f, 0f, 0f, 0.8f), mid, mid, Vector2.zero, Vector2.zero);
        panelImg.raycastTarget = true; // block taps behind the panel
        panelImg.rectTransform.anchorMin = Vector2.zero;
        panelImg.rectTransform.anchorMax = Vector2.one;
        panelImg.rectTransform.offsetMin = Vector2.zero;
        panelImg.rectTransform.offsetMax = Vector2.zero;
        TMP_Text overText = CreateText(panelImg.transform, "GameOverText", "", 70, TextAlignmentOptions.Center, mid, mid, new Vector2(0f, 150f), new Vector2(900f, 500f));
        Button restart = CreateButton(panelImg.transform, "RestartButton", Loc.Restart, new Color(0.1f, 0.6f, 0.2f, 0.95f), mid, mid,
                     new Vector2(0f, -250f), new Vector2(480f, 160f), 52, null);
        restart.onClick.AddListener(() =>
        {
            gm.RestartGame();
            if (huntButtonObject != null) huntButtonObject.SetActive(false); // hunt restarts automatically
        });
        panelImg.gameObject.SetActive(false);

        gm.BindHud(status, score, ammo, health, wave, fillRect, flash, panelImg.gameObject, overText);
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private Image CreateImage(Transform parent, string name, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        SetRect(img.rectTransform, anchor, pivot, pos, size);
        return img;
    }

    private TMP_Text CreateText(Transform parent, string name, string text, int fontSize, TextAlignmentOptions align,
                                Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(0, 0, 0, 255);
        if (hudFont != null) t.font = hudFont;
        SetRect(t.rectTransform, anchor, pivot, pos, size);
        return t;
    }

    private Button CreateButton(Transform parent, string name, string label, Color color, Vector2 anchor, Vector2 pivot,
                                Vector2 pos, Vector2 size, int fontSize, UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetRect((RectTransform)go.transform, anchor, pivot, pos, size);

        Image img = go.GetComponent<Image>();
        img.color = color;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = img;
        if (onClick != null) button.onClick.AddListener(onClick);

        CreateText(go.transform, "Label", label, fontSize, TextAlignmentOptions.Center,
                   new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return button;
    }
}
