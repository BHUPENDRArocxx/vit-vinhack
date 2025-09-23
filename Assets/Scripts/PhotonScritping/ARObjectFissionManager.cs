using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARObjectFissionManager : MonoBehaviour
{
    [Header("AR References")]
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;

    [Header("Prefabs")]
    [SerializeField] private GameObject prefabA;
    [SerializeField] private GameObject prefabB;
    [SerializeField] private ParticleSystem fusionVFX;

    [Header("Spawn Settings")]
    [SerializeField] private float objectSpacing = 0.3f; // distance between A and B

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.5f;

    [Header("Camera Shake")]
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeMagnitude = 0.1f;

    [Header("Haptic Feedback")]
    [SerializeField] private bool enableHaptics = true;

    // --- Internals ---
    private bool objectsSpawned = false;
    private bool fusionStarted = false;
    private GameObject objA, objB;

    private HapticManager hapticManager;

    void Awake()
    {
        hapticManager = new HapticManager();
    }

    void OnEnable()
    {
        if (planeManager != null)
            planeManager.planesChanged += OnPlanesChanged;
    }

    void OnDisable()
    {
        if (planeManager != null)
            planeManager.planesChanged -= OnPlanesChanged;
    }

    // ---------- Plane Detection ----------
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (objectsSpawned) return;

        foreach (var plane in args.added)
        {
            if (plane.alignment == PlaneAlignment.HorizontalUp)
            {
                SpawnObjects(plane.center, plane.transform.right);
                objectsSpawned = true;
                Debug.Log("Horizontal plane detected, objects spawned.");
                break;
            }
        }
    }

    // ---------- Spawn Prefabs ----------
    private void SpawnObjects(Vector3 planeCenter, Vector3 planeRight)
    {
        Vector3 posA = planeCenter - planeRight * objectSpacing;
        Vector3 posB = planeCenter + planeRight * objectSpacing;

        objA = Instantiate(prefabA, posA, Quaternion.identity);
        objB = Instantiate(prefabB, posB, Quaternion.identity);
    }

    // ---------- Button Trigger ----------
    public void StartFusion()
    {
        if (!objectsSpawned || fusionStarted) return;
        fusionStarted = true;

        StartCoroutine(MoveObjectTowards(objA, objB.transform.position));
    }

    // ---------- Reset Button ----------
    public void ResetFusion()
    {
        // Stop all coroutines
        StopAllCoroutines();

        // Destroy old prefabs
        if (objA != null) Destroy(objA);
        if (objB != null) Destroy(objB);

        // Reset flags
        objectsSpawned = false;
        fusionStarted = false;

        // Re-enable plane detection
        if (planeManager != null)
            planeManager.enabled = true;

        Debug.Log("Fusion sequence reset.");
    }

    // ---------- Fusion Sequence ----------
    private IEnumerator MoveObjectTowards(GameObject movingObj, Vector3 targetPos)
    {
        while (movingObj != null && Vector3.Distance(movingObj.transform.position, targetPos) > 0.01f)
        {
            movingObj.transform.position = Vector3.MoveTowards(
                movingObj.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }

        // Trigger VFX + Haptic + Camera Shake
        if (fusionVFX != null)
        {
            ParticleSystem vfx = Instantiate(fusionVFX, targetPos, Quaternion.identity);
            vfx.Play();
            Destroy(vfx.gameObject, vfx.main.duration + vfx.main.startLifetime.constantMax);
        }

        if (enableHaptics)
            hapticManager.TriggerHaptic();

        StartCoroutine(ShakeCamera());
    }

    private IEnumerator ShakeCamera()
    {
        Vector3 originalPos = arCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;
            arCamera.transform.localPosition = originalPos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        arCamera.transform.localPosition = originalPos;
    }

    // ---------- Custom Haptic Manager ----------
    private class HapticManager
    {
        public void TriggerHaptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
