using System;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    [SerializeField] private Camera arCamera;
    [SerializeField] private float range = 30f;
    [SerializeField] private int damage = 25;
    [SerializeField] private float fireCooldown = 0.25f;
    [SerializeField] private float aimAssistRadius = 0.15f; // forgiving aim on a phone
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject hitEffect;

    private float nextFireTime;

    public void Configure(Camera cam) { arCamera = cam; }

    // Returns true if a shot was actually fired.
    public bool TryFire()
    {
        if (arCamera == null) return false;
        if (Time.time < nextFireTime) return false;

        // Ammo is only spent when the shot really happens.
        if (GameManager.Instance != null && !GameManager.Instance.TryUseAmmo())
            return false;

        nextFireTime = Time.time + fireCooldown;
        Sfx.Shot();
        if (muzzleFlash != null) muzzleFlash.Play();

        Ray ray = new Ray(arCamera.transform.position, arCamera.transform.forward);
        RaycastHit[] hits = Physics.SphereCastAll(ray, aimAssistRadius, range, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // Take the closest monster; AR plane colliders and other objects are ignored.
        foreach (RaycastHit hit in hits)
        {
            MonsterHealth monster = hit.collider.GetComponentInParent<MonsterHealth>();
            if (monster == null) continue;

            monster.TakeDamage(damage);

            if (hitEffect != null)
            {
                Vector3 point = hit.point == Vector3.zero ? monster.transform.position : hit.point;
                Destroy(Instantiate(hitEffect, point, Quaternion.identity), 2f);
            }
            break;
        }

        return true;
    }
}
