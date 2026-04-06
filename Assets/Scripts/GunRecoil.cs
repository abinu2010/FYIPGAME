using UnityEngine;

public class GunRecoil : MonoBehaviour
{
    [SerializeField] private Transform gunTransform;
    [SerializeField] private float kickBack = 0.12f;
    [SerializeField] private float kickUp = 0.06f;
    [SerializeField] private float returnSpeed = 12f;

    private Vector3 defaultLocalPos;

    void Awake()
    {
        if (gunTransform != null)
            defaultLocalPos = gunTransform.localPosition;
    }

    void Update()
    {
        if (gunTransform == null)
            return;

        gunTransform.localPosition = Vector3.Lerp(gunTransform.localPosition, defaultLocalPos, returnSpeed * Time.deltaTime);
    }

    public void ApplyRecoil()
    {
        if (gunTransform == null)
            return;

        Vector3 kick = new Vector3(0f, kickUp, -kickBack);
        gunTransform.localPosition += kick;
    }

    public void ResetInstant()
    {
        if (gunTransform == null)
            return;

        gunTransform.localPosition = defaultLocalPos;
    }
}
