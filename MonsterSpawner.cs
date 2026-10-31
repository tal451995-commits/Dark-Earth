using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MonsterSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private Camera arCamera;
    [SerializeField] private ARRaycastManager raycastManager;      // optional: places monsters on detected planes
    [SerializeField] private ARAnchorManager anchorManager;
    [SerializeField] private EnvironmentController environment;    // optional: night = more monsters

    [Header("Placement")]
    [SerializeField] private float minSpawnDistance = 2.5f;
    [SerializeField] private float maxSpawnDistance = 4.5f;
    [SerializeField] private float maxYawOffset = 40f;             // spread in front of the player (degrees)
    [SerializeField] private float fallbackCameraHeight = 1.3f;    // used when no AR plane has been found yet

    [Header("Waves")]
    [SerializeField] private int baseMonstersPerWave = 2;
    [SerializeField] private int extraMonstersPerWave = 1;
    [SerializeField] private int maxAlive = 3;
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private float timeBetweenWaves = 4f;

    public int CurrentWave { get; private set; }
    public GameObject CurrentMonster { get; private set; }

    private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private Coroutine huntRoutine;
    private bool hasGround;
    private float groundY;
    private bool subscribed;

    public void Configure(
    GameObject prefab,
    Camera cam,
    ARRaycastManager raycast,
    EnvironmentController env)
{
    monsterPrefab = prefab;
    arCamera = cam;
    raycastManager = raycast;
    environment = env;

    if (anchorManager == null)
        anchorManager = FindFirstObjectByType<ARAnchorManager>();
}

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRestart += HandleRestart;
            subscribed = true;
        }
    }

    private void OnDestroy()
    {
        if (subscribed && GameManager.Instance != null)
            GameManager.Instance.OnRestart -= HandleRestart;
    }

    private void HandleRestart()
    {
        StopHunt();
        CurrentWave = 0;
        StartHunt();
    }

    // ---------- Public API (hook to UI buttons) ----------

    // Starts the wave system. Safe to call more than once.
    public void StartHunt()
    {
        if (huntRoutine != null) return;
        if (GameManager.Instance != null) GameManager.Instance.BeginHunt();
        huntRoutine = StartCoroutine(HuntLoop());
    }

    public void StopHunt()
    {
        if (huntRoutine != null) StopCoroutine(huntRoutine);
        huntRoutine = null;
    }

    // Old behaviour kept for compatibility: spawn a single monster in front of the player.
    public void SpawnMonster()
    {
        if (GameManager.Instance != null) GameManager.Instance.BeginHunt();

        if (CurrentMonster != null)
        {
            MonsterHealth old = CurrentMonster.GetComponent<MonsterHealth>();
            if (old != null) old.Despawn(); else Destroy(CurrentMonster);
        }

        SpawnOne(Mathf.Max(1, CurrentWave));
    }

    // ---------- Waves ----------

    private bool IsPlaying()
    {
        return GameManager.Instance == null || GameManager.Instance.IsPlaying;
    }

    private IEnumerator HuntLoop()
    {
        yield return new WaitForSeconds(1f); // give AR a moment to find the floor

        while (IsPlaying())
        {
            CurrentWave++;
            int count = baseMonstersPerWave + (CurrentWave - 1) * extraMonstersPerWave;
            if (GameManager.Instance != null) GameManager.Instance.WaveStarted(CurrentWave);

            for (int i = 0; i < count; i++)
            {
                while (MonsterHealth.ActiveCount >= maxAlive && IsPlaying())
                    yield return null;
                if (!IsPlaying()) break;

                SpawnOne(CurrentWave);

                float multiplier = environment != null ? environment.MonsterSpawnMultiplier : 1f;
                yield return new WaitForSeconds(spawnInterval / Mathf.Max(0.3f, multiplier));
            }

            while (MonsterHealth.ActiveCount > 0 && IsPlaying())
                yield return null;
            if (!IsPlaying()) break;

            if (GameManager.Instance != null) GameManager.Instance.WaveCleared(CurrentWave);
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        huntRoutine = null;
    }

    // ---------- Spawning ----------

    private void SpawnOne(int wave)
    {
        if (monsterPrefab == null || arCamera == null)
        {
            Debug.LogError("MonsterSpawner: assign Monster Prefab and AR Camera (or use DarkEarthBootstrap).");
            return;
        }

        
        Vector3 position;
        bool hasPlane = TryGetPlanePosition(out position);

        if (!hasPlane)
            position = GetFallbackPosition();

        // Face the player.
        Vector3 toCamera = arCamera.transform.position - position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.001f)
            toCamera = -arCamera.transform.forward;

        Quaternion rotation =
            Quaternion.LookRotation(toCamera.normalized, Vector3.up);

        // Create an AR anchor when a detected floor position is available.
        ARAnchor anchor = null;
        GameObject go;

        bool anchored = false;

        if (hasPlane && anchorManager != null)
        {
            Pose anchorPose = new Pose(position, rotation);
            anchored = anchorManager.TryAddAnchor(anchorPose, out anchor);
        }

        if (anchored && anchor != null)
        {
            go = Instantiate(monsterPrefab, anchor.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            MonsterAnchorCleanup cleanup =
                go.AddComponent<MonsterAnchorCleanup>();
            cleanup.Initialize(anchor);
        }
        else
        {
            go = Instantiate(monsterPrefab, position, rotation);
        }

        go.SetActive(true);
        SnapFeetToGround(go, position.y);

        MonsterHealth health = go.GetComponent<MonsterHealth>();
        if (health == null)
            health = go.AddComponent<MonsterHealth>();

        health.SetCamera(arCamera.transform);
        health.ScaleHealth(1f + 0.15f * (wave - 1));

        MonsterAI ai = go.GetComponent<MonsterAI>();
        if (ai == null)
            ai = go.AddComponent<MonsterAI>();

        ai.SetTarget(arCamera.transform);
        ai.SetDifficulty(wave);

        CurrentMonster = go;

        if (GameManager.Instance != null)
            GameManager.Instance.MonsterSpawned();
}

    // Uses AR Foundation plane detection when available.
    private bool TryGetPlanePosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (raycastManager == null || arCamera == null) return false;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 screenPoint = new Vector2(
                Random.Range(0.2f, 0.8f) * Screen.width,
                Random.Range(0.2f, 0.5f) * Screen.height);

            if (!raycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon) || hits.Count == 0)
                continue;

            Pose pose = hits[0].pose;
            if (Vector3.Dot(pose.up, Vector3.up) < 0.8f) continue; // floors only, not walls

            Vector3 flat = pose.position - arCamera.transform.position;
            flat.y = 0f;
            float d = flat.magnitude;
            if (d < minSpawnDistance || d > maxSpawnDistance + 2f) continue;

            hasGround = true;
            groundY = pose.position.y;
            position = pose.position;
            return true;
        }
        return false;
    }

    // Used when no plane is known: a point in front of the player at an estimated floor height.
    // The direction is flattened, so looking down at the floor no longer buries the monster.
    private Vector3 GetFallbackPosition()
    {
        Transform cam = arCamera.transform;

        Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(cam.up, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        forward = Quaternion.AngleAxis(Random.Range(-maxYawOffset, maxYawOffset), Vector3.up) * forward;

        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 position = cam.position + forward * distance;
        position.y = hasGround ? groundY : cam.position.y - fallbackCameraHeight;
        return position;
    }

    // Moves the monster up/down so the lowest point of its mesh touches the ground.
    private static void SnapFeetToGround(GameObject go, float groundHeight)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        go.transform.position += Vector3.up * (groundHeight - bounds.min.y);
    }
}
public class MonsterAnchorCleanup : MonoBehaviour
{
    private ARAnchor anchor;

    public void Initialize(ARAnchor value)
    {
        anchor = value;
    }

    private void OnDestroy()
    {
        if (anchor != null)
            Destroy(anchor.gameObject);
    }
}
