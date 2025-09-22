using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using TMPro;
using UnityEngine.UI;

public class PlanetWalkARManager : MonoBehaviour
{
    PlanetWalkLocationManager locationManager;

    [Header("References")]
    [SerializeField] private Camera arCamera = null;
    [SerializeField] private ARAnchorManager anchorManager = null;
    [SerializeField] private TextMeshProUGUI DebugText = null;
    [SerializeField] private GameObject[] CelestialObjectsToPlace = null;

    [Header("ScriptableObject Data")]
    [Tooltip("Assign PlanetData ScriptableObjects in order (0 = Sun, 1 = Mercury, etc.)")]
    [SerializeField] private PlanetData[] planetDataArray = null;

    [Header("UI")]
    [SerializeField] private GameObject StartButton = null;
    [SerializeField] private GameObject BeginPlacementButton = null;
    [SerializeField] private GameObject PlacementButton = null;
    [Header("Manual Placement UI")]
    [Tooltip("Hook Next Planet button's OnClick to OnNextPlanetButton()")]
    [SerializeField] private GameObject NextPlanetButton = null;

    // New: Text fields to show planet data (TextMeshPro)
    [Header("Planet Data UI (TextMeshPro)")]
    [SerializeField] private TextMeshProUGUI planetNameText = null;
    [SerializeField] private TextMeshProUGUI planetDescriptionText = null;

    // New: Image field to show the planet sprite
    [Header("Planet Data UI (Image)")]
    [Tooltip("Assign a UI Image (UnityEngine.UI.Image) to display the planet sprite.")]
    [SerializeField] private Image planetImageUI = null;

    [Header("AR")]
    [SerializeField] private ARPlaneManager arPlaneManager = null;

    [Header("Placement")]
    [Tooltip("All objects will be placed exactly this many meters in front of the camera.")]
    [SerializeField] private float placementDistance = 1.5f; // constant distance in meters

    // auto-placement state (used by the threshold flow)
    private bool waitingForPlanes = false;
    private List<GameObject> placedObjects = new List<GameObject>();
    private int walkThresholdIndex = 0;
    private int celestialObjectIndex = 0;
    string[] celestialObjectsNames = { "the Sun", "the Terrestrial Planets", "the Asteroid Belt", "Jupiter", "Saturn", "Uranus", "Neptune", "Pluto" };

    // manual placement state (Next Planet button)
    private int manualPlanetIndex = 0;
    private GameObject lastManualSpawned = null;
    [Tooltip("If true, manual placement will deactivate the previously spawned planet.")]
    [SerializeField] private bool deactivateOldOnNext = true;

    void Start()
    {
        Application.targetFrameRate = 60;
        locationManager = this.GetComponent<PlanetWalkLocationManager>();

        if (arPlaneManager != null)
            arPlaneManager.planesChanged += PlanesChanged;
        else
            Debug.LogWarning("ARPlaneManager reference is missing on PlanetWalkARManager.");

        // disable plane detection by default, enable when user starts placement
        if (arPlaneManager != null)
            arPlaneManager.enabled = false;

        // safety UI text
        if (DebugText != null && StartButton != null)
            DebugText.text = "Press Start to begin the Planet Walk.";

        // ensure NextPlanetButton is active if assigned
        if (NextPlanetButton != null)
            NextPlanetButton.SetActive(true);

        // Show initial planet data (index 0 = Sun) if data present
        if (planetDataArray != null && planetDataArray.Length > 0)
        {
            ShowPlanetData(0);
        }
        else
        {
            ClearPlanetDataUI();
        }
    }

    // START WALK (auto / threshold flow)
    public void StartWalk()
    {
        if (StartButton != null) StartButton.SetActive(false);
        if (DebugText != null) DebugText.text = "Move camera to detect placement planes.";
        waitingForPlanes = true;
        if (arPlaneManager != null) arPlaneManager.enabled = true;
    }

    // PLANE DETECTION
    void PlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (waitingForPlanes)
        {
            Activate();
        }
    }

    private void Activate()
    {
        if (DebugText != null)
            DebugText.text = "Press Place Object to place " + celestialObjectsNames[walkThresholdIndex] + ".";
        waitingForPlanes = false;
        if (arPlaneManager != null) arPlaneManager.enabled = false;
        if (PlacementButton != null) PlacementButton.SetActive(true);
    }

    // ------- AUTO-PLACEMENT (threshold/plane based) -------
    // Keep this if you want the previous auto placement behavior.
    [System.Obsolete]
    public void AddCelestialObject()
    {
        if (arCamera == null)
        {
            Debug.LogError("arCamera is null. Assign your AR Camera in the inspector.");
            if (DebugText != null) DebugText.text = "AR Camera missing.";
            return;
        }

        // If terrestrial group: place multiple objects but all at the same placementDistance,
        // arranged around the camera center so they don't overlap.
        if (walkThresholdIndex == 1) // add terrestrials
        {
            int[] indices = { 1, 2, 3, 4 };
            for (int i = 0; i < indices.Length; i++)
            {
                Vector3 pos = GetPositionAtFixedDistance(i, indices.Length);
                AddCelestialObjectAtPos(pos, indices[i]);
            }
            celestialObjectIndex = 5;
        }
        else
        {
            Vector3 centerPosition = GetPositionAtFixedDistance(0, 1); // center
            AddCelestialObjectAtPos(centerPosition, celestialObjectIndex);
            celestialObjectIndex++;
        }

        if (walkThresholdIndex == 0) // place the sun
        {
            if (locationManager != null)
                locationManager.SetStartLocation();
        }

        if (DebugText != null)
            DebugText.text = "Explore  " + celestialObjectsNames[walkThresholdIndex] + " or keep walking.";

        if (PlacementButton != null) PlacementButton.SetActive(false);
    }

    // Returns a world position at exactly 'placementDistance' meters from the camera.
    private Vector3 GetPositionAtFixedDistance(int index, int totalItems)
    {
        Vector3 forward = arCamera.transform.forward.normalized;
        Vector3 up = arCamera.transform.up.normalized;

        float angleRange = 30f; // degrees total spread
        float angle = 0f;
        if (totalItems > 1)
        {
            float step = angleRange / (totalItems - 1);
            angle = -angleRange / 2f + index * step;
        }

        Vector3 rotatedDir = Quaternion.AngleAxis(angle, up) * forward;
        Vector3 worldPos = arCamera.transform.position + rotatedDir * placementDistance;
        return worldPos;
    }

    [System.Obsolete]
    void AddCelestialObjectAtPos(Vector3 position, int celestialIdx)
    {
        if (CelestialObjectsToPlace == null || celestialIdx < 0 || celestialIdx >= CelestialObjectsToPlace.Length)
        {
            Debug.LogWarning($"Invalid celestial index {celestialIdx} or prefab array not assigned.");
            return;
        }

        GameObject prefab = CelestialObjectsToPlace[celestialIdx];
        if (prefab == null)
        {
            Debug.LogWarning($"Prefab at index {celestialIdx} is null.");
            return;
        }

        Pose pose = new Pose(position, Quaternion.LookRotation(arCamera.transform.forward, arCamera.transform.up));

        ARAnchor createdAnchor = null;
        GameObject spawned = null;

        if (anchorManager != null)
        {
            createdAnchor = anchorManager.AddAnchor(pose);
            if (createdAnchor != null)
            {
                spawned = Instantiate(prefab, createdAnchor.transform);
                spawned.transform.localPosition = Vector3.zero;
                spawned.transform.localRotation = Quaternion.identity;
            }
            else
            {
                spawned = Instantiate(prefab, pose.position, pose.rotation);
            }
        }
        else
        {
            spawned = Instantiate(prefab, pose.position, pose.rotation);
        }

        float scale = 1f;
        if (celestialIdx == 0) // Sun special scale
            scale = 1000f;

        if (spawned != null)
            spawned.transform.localScale = new Vector3(.00025f * scale, .00025f * scale, .00025f * scale);

        placedObjects.Add(spawned);
    }

    // ------- MANUAL PLACEMENT (Next Planet button) -------
    // Hook your UI Button -> OnClick to this method
    [System.Obsolete]
    public void OnNextPlanetButton()
    {
        // Place the next planet in AR
        PlaceNextPlanet();

        // After attempting to place, update the planet data UI:
        // manualPlanetIndex is incremented inside PlaceNextPlanet if placement was successful.
        if (planetDataArray != null && planetDataArray.Length > 0)
        {
            if (manualPlanetIndex < planetDataArray.Length)
            {
                ShowPlanetData(manualPlanetIndex); // shows the data for the next planet (matching current manualPlanetIndex)
            }
            else
            {
                if (planetNameText != null) planetNameText.text = "No more planets";
                if (planetDescriptionText != null) planetDescriptionText.text = "All planet data shown.";
                if (planetImageUI != null) planetImageUI.sprite = null;
                if (planetImageUI != null) planetImageUI.enabled = false;
            }
        }
    }

    // Places the next prefab in the CelestialObjectsToPlace array at placementDistance in front of camera,
    // and disables the previously-placed manual planet (keeps it for possible re-enable).
    [System.Obsolete]
    public void PlaceNextPlanet()
    {
        if (arCamera == null)
        {
            Debug.LogError("arCamera is not assigned.");
            if (DebugText != null) DebugText.text = "AR Camera missing.";
            return;
        }

        if (CelestialObjectsToPlace == null || CelestialObjectsToPlace.Length == 0)
        {
            Debug.LogWarning("No CelestialObjectsToPlace assigned.");
            if (DebugText != null) DebugText.text = "No planet prefabs assigned.";
            return;
        }

        if (manualPlanetIndex >= CelestialObjectsToPlace.Length)
        {
            if (DebugText != null) DebugText.text = "All planets placed.";
            return;
        }

        // disable previous manual spawned object if requested
        if (deactivateOldOnNext && lastManualSpawned != null)
        {
            lastManualSpawned.SetActive(false);
        }

        // compute pose
        Vector3 worldPos = arCamera.transform.position + arCamera.transform.forward.normalized * placementDistance;
        Pose pose = new Pose(worldPos, Quaternion.LookRotation(arCamera.transform.forward, arCamera.transform.up));

        GameObject prefab = CelestialObjectsToPlace[manualPlanetIndex];
        GameObject spawned = null;

        if (anchorManager != null)
        {
            ARAnchor anchor = anchorManager.AddAnchor(pose);
            if (anchor != null)
            {
                spawned = Instantiate(prefab, anchor.transform);
                spawned.transform.localPosition = Vector3.zero;
                spawned.transform.localRotation = Quaternion.identity;
            }
            else
            {
                spawned = Instantiate(prefab, pose.position, pose.rotation);
            }
        }
        else
        {
            spawned = Instantiate(prefab, pose.position, pose.rotation);
        }

        // store manual state
        lastManualSpawned = spawned;
        if (spawned != null)
        {
            placedObjects.Add(spawned);
            // preserve original scaling behavior if desired:
            float scale = (manualPlanetIndex == 0) ? 1000f : 1f;
            spawned.transform.localScale = new Vector3(.00025f * scale, .00025f * scale, .00025f * scale);
        }

        // update indices and UI
        manualPlanetIndex++;
        if (DebugText != null)
        {
            DebugText.text = (manualPlanetIndex < CelestialObjectsToPlace.Length)
                ? "Placed. Press Next Planet to place " + CelestialObjectsToPlace[manualPlanetIndex].name + "."
                : "Placed. No more planets.";
        }

        if (StartButton != null) StartButton.SetActive(false);
    }

    // Show planet data from ScriptableObjects
    private void ShowPlanetData(int index)
    {
        if (planetDataArray == null || planetDataArray.Length == 0)
        {
            ClearPlanetDataUI();
            return;
        }

        index = Mathf.Clamp(index, 0, planetDataArray.Length - 1);
        PlanetData data = planetDataArray[index];
        if (data == null)
        {
            ClearPlanetDataUI();
            return;
        }

        if (planetNameText != null) planetNameText.text = data.planetName;
        if (planetDescriptionText != null) planetDescriptionText.text = data.description;

        if (planetImageUI != null)
        {
            planetImageUI.sprite = data.planetImage;
            planetImageUI.enabled = data.planetImage != null;
        }

        // optional: the DebugText can also show a short hint
        if (DebugText != null)
            DebugText.text = $"Showing data for: {data.planetName}";
    }

    // Clear UI helper
    private void ClearPlanetDataUI()
    {
        if (planetNameText != null) planetNameText.text = "";
        if (planetDescriptionText != null) planetDescriptionText.text = "";
        if (planetImageUI != null)
        {
            planetImageUI.sprite = null;
            planetImageUI.enabled = false;
        }
    }

    // PASSED A NEW CELESTIAL OBJECT (keeps auto flow)
    public void NewCelestialObjectThresholdPassed(int pIndex)
    {
        walkThresholdIndex = pIndex;
        if (BeginPlacementButton != null) BeginPlacementButton.SetActive(true);
        if (DebugText != null) DebugText.text = "Press Begin Placement to add " + celestialObjectsNames[walkThresholdIndex] + ".";
    }

    // BEGIN PLACEMENT OF NEW OBJECT (auto flow)
    public void BeginPlacement()
    {
        if (BeginPlacementButton != null) BeginPlacementButton.SetActive(false);
        if (DebugText != null) DebugText.text = "Move camera to detect placement planes.";
        waitingForPlanes = true;
        if (arPlaneManager != null) arPlaneManager.enabled = true;
    }

    // Optional: reset manual placement (reenable or destroy placed objects)
    public void ResetPlanets(bool destroyInsteadOfDisable = false)
    {
        for (int i = 0; i < placedObjects.Count; i++)
        {
            if (placedObjects[i] == null) continue;
            if (destroyInsteadOfDisable) Destroy(placedObjects[i]);
            else placedObjects[i].SetActive(true);
        }
        placedObjects.Clear();
        lastManualSpawned = null;
        manualPlanetIndex = 0;

        // reset displayed data to index 0 if available
        if (planetDataArray != null && planetDataArray.Length > 0)
            ShowPlanetData(0);
        else
            ClearPlanetDataUI();

        if (DebugText != null) DebugText.text = "Placement reset. Press Next Planet.";
    }
}
