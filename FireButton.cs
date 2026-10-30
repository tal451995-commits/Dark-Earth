using UnityEngine;

public class FireButton : MonoBehaviour
{
    [SerializeField] private WeaponController weapon;

    public void Configure(WeaponController w) { weapon = w; }

    // Connect this method to the UI Button's OnClick event.
    public void OnFirePressed()
    {
        if (weapon == null) weapon = FindFirstObjectByType<WeaponController>();
        if (weapon == null) return;

        weapon.TryFire(); // ammo, cooldown and game state are all handled inside
    }
}
