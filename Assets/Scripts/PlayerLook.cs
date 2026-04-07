using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform playerBody;
    [SerializeField] private float defaultSensitivity = 2f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float mouseSensitivity;
    private float xRotation;

    public float CurrentSensitivity => mouseSensitivity;

    private void Awake()
    {
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", defaultSensitivity);
    }

    private void Update()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (playerBody != null)
            playerBody.Rotate(Vector3.up * mouseX);
    }

    public void SetSensitivity(float value)
    {
        mouseSensitivity = Mathf.Max(0.01f, value);
        PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
        PlayerPrefs.Save();
    }

    public void AddRecoil(float pitchUpDegrees, float yawDegrees)
    {
        xRotation -= pitchUpDegrees;
        xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (playerBody != null)
            playerBody.Rotate(Vector3.up * yawDegrees);
    }

    public void ResetLookInstant(float pitch = 0f)
    {
        xRotation = Mathf.Clamp(pitch, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}