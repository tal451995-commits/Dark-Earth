using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Editor-only convenience: hold the RIGHT mouse button and drag to look around,
// so you can test aiming and shooting without a phone. Does nothing in builds.
public class EditorAimHelper : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField] private float sensitivity = 0.15f;
    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x > 180f ? e.x - 360f : e.x;
    }

    private void Update()
    {
        bool held;
        Vector2 delta;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Mouse.current == null) return;
        held = Mouse.current.rightButton.isPressed;
        delta = Mouse.current.delta.ReadValue();
#else
        held = Input.GetMouseButton(1);
        delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#endif
        if (!held) return;

        yaw += delta.x * sensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -80f, 80f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
#endif
}
