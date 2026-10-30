using System.Collections;
using UnityEngine;

// Walks towards the player (on the horizontal plane only, so it stays on the
// ground) and attacks when close enough.
public class MonsterAI : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 0.7f;      // meters / second
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float attackRange = 1.1f;    // horizontal distance to the camera
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackCooldown = 1.4f;
    [SerializeField] private float firstAttackDelay = 1f; // grace period after spawning

    private Transform target;
    private float nextAttackTime;
    private Vector3 baseScale;

    private void Awake() { baseScale = transform.localScale; }

    private void Start() { nextAttackTime = Time.time + firstAttackDelay; }

    public void SetTarget(Transform t) { target = t; }

    // Gets tougher every wave.
    public void SetDifficulty(int wave)
    {
        int w = Mathf.Max(1, wave);
        moveSpeed *= Mathf.Min(2f, 1f + 0.08f * (w - 1));
        attackDamage += 2 * (w - 1);
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        if (target == null)
        {
            if (Camera.main == null) return;
            target = Camera.main.transform;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance < 0.01f) return;

        Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);

        if (distance > attackRange)
        {
            transform.position += toTarget.normalized * moveSpeed * Time.deltaTime;
        }
        else if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            if (GameManager.Instance != null)
                GameManager.Instance.DamagePlayer(attackDamage);
            StartCoroutine(AttackPunch());
        }
    }

    private IEnumerator AttackPunch()
    {
        const float duration = 0.18f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(t / duration * Mathf.PI);
            transform.localScale = baseScale * (1f + 0.3f * k);
            yield return null;
        }
        transform.localScale = baseScale;
    }
}
