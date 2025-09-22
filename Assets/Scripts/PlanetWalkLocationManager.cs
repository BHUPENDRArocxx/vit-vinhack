using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif
using TMPro;

public class PlanetWalkLocationManager : MonoBehaviour
{
    PlanetWalkARManager arManager;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI LocationText = null;
    [SerializeField] private TextMeshProUGUI DistanceText = null;
    [SerializeField] private TextMeshProUGUI StatusText = null;

    GameObject dialog = null;
    bool locationIsReady = false;
    bool locationGrantedAndroid = false;

    double startLat = 0;
    double startLong = 0;
    double currLat = 0;
    double currLong = 0;
    double currAcc = 0;

    [Header("Thresholds (meters)")]
    [Tooltip("Distance thresholds (in meters) that trigger placement progression.")]
    [SerializeField] private double[] thresholds = { 3, 8.5, 13.976, 25.63, 51.553, 80.822, 106.203 };

    [SerializeField, Tooltip("Maximum acceptable accuracy (in meters) for using the current GPS fix.")]
    private float maxAcceptableAccuracy = 5f;

    int currentThreshold = 0;
    bool startLocationMarked = false;

    // Start is called before the first frame update
    void Start()
    {
#if PLATFORM_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
            dialog = new GameObject();
            UpdateStatusText("Requesting location permission...");
        }
        else
        {
            locationGrantedAndroid = true;
            locationIsReady = NativeGPSPlugin.StartLocation();
            UpdateStatusText(locationIsReady ? "Location service started." : "Failed to start location service.");
        }

#elif PLATFORM_IOS
        locationIsReady = NativeGPSPlugin.StartLocation();
        UpdateStatusText(locationIsReady ? "Location service started." : "Failed to start location service.");
#endif
        arManager = this.GetComponent<PlanetWalkARManager>();
        if (arManager == null)
        {
            Debug.LogWarning("PlanetWalkLocationManager: PlanetWalkARManager component not found on the same GameObject.");
        }

        // initial UI
        if (LocationText != null) LocationText.text = "LOC: -";
        if (DistanceText != null) DistanceText.text = "D: 0.00 m";
    }

    public void Reset()
    {
        currentThreshold = 0;
        startLocationMarked = false;
        startLat = 0;
        startLong = 0;
        UpdateStatusText("Start location reset.");
    }

    private void Update()
    {
        if (!locationIsReady) return;

        // retrieve current device location
        double lat = NativeGPSPlugin.GetLatitude();
        double lon = NativeGPSPlugin.GetLongitude();
        double acc = NativeGPSPlugin.GetAccuracy();

        // if accuracy is acceptable, use the fix
        if (acc <= maxAcceptableAccuracy && lat != 0 && lon != 0)
        {
            currLat = lat;
            currLong = lon;
            currAcc = acc;

            if (LocationText != null) LocationText.text = $"LOC: {currLat:F6}, {currLong:F6} (acc {currAcc:F1} m)";

            // update distance display if start was set
            if (startLat != 0 || startLong != 0)
            {
                ComputeDistance();
            }
        }
        else
        {
            // still show the raw values but mark accuracy issue
            if (LocationText != null)
            {
                LocationText.text = $"LOC: {lat:F6}, {lon:F6} (acc {acc:F1} m) - waiting for better accuracy";
            }
        }

        // threshold progression logic
        if (startLocationMarked && currentThreshold < thresholds.Length)
        {
            double dist = DistanceBetweenPointsInMeters(startLat, startLong, currLat, currLong);
            // only consider threshold if we have a reasonable fix
            if (dist >= 0 && dist > thresholds[currentThreshold])
            {
                currentThreshold++;
                UpdateStatusText("Passed threshold " + currentThreshold);
                if (arManager != null)
                {
                    arManager.NewCelestialObjectThresholdPassed(currentThreshold);
                }
            }
        }
    }

    void OnGUI()
    {
#if PLATFORM_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            // The user denied permission to use the fineLocation.
            // Display a message explaining why you need it with Yes/No buttons.
            if (dialog != null && dialog.GetComponent<PermissionsRationaleDialog>() == null)
            {
                dialog.AddComponent<PermissionsRationaleDialog>();
            }
            return;
        }
        else if (dialog != null)
        {
            if (!locationGrantedAndroid)
            {
                locationGrantedAndroid = true;
                locationIsReady = NativeGPSPlugin.StartLocation();
                UpdateStatusText(locationIsReady ? "Location service started." : "Failed to start location service.");
            }

            Destroy(dialog);
            dialog = null;
        }
#endif
    }

    /// <summary>
    /// Marks the current GPS fix as the start location — only if the current fix is valid.
    /// </summary>
    public bool SetStartLocation()
    {
        // require a valid current fix
        if (currLat == 0 && currLong == 0)
        {
            UpdateStatusText("Cannot set start location: no valid GPS fix yet.");
            return false;
        }
        if (currAcc > maxAcceptableAccuracy)
        {
            UpdateStatusText($"Cannot set start location: accuracy {currAcc:F1} m is worse than acceptable {maxAcceptableAccuracy} m.");
            return false;
        }

        startLat = currLat;
        startLong = currLong;
        currentThreshold = 0;
        startLocationMarked = true;
        UpdateStatusText("Start location set.");
        return true;
    }

    public void ComputeDistance()
    {
        double dist = DistanceBetweenPointsInMeters(startLat, startLong, currLat, currLong);
        if (DistanceText != null)
        {
            DistanceText.text = $"D: {dist:F2} m";
        }
    }

    /// <summary>
    /// Haversine-like formula used previously — returns meters.
    /// </summary>
    public static double DistanceBetweenPointsInMeters(double lat1, double lon1, double lat2, double lon2)
    {
        // basic sanity
        if ((lat1 == 0 && lon1 == 0) || (lat2 == 0 && lon2 == 0)) return 0;

        double rlat1 = System.Math.PI * lat1 / 180.0;
        double rlat2 = System.Math.PI * lat2 / 180.0;
        double theta = lon1 - lon2;
        double rtheta = System.Math.PI * theta / 180.0;
        double dist =
            System.Math.Sin(rlat1) * System.Math.Sin(rlat2) + System.Math.Cos(rlat1) *
            System.Math.Cos(rlat2) * System.Math.Cos(rtheta);

        // guard numerical issues
        dist = System.Math.Clamp(dist, -1.0, 1.0);
        dist = System.Math.Acos(dist);
        dist = dist * 180.0 / System.Math.PI;

        // convert degrees to miles, then to meters
        double distInMiles = dist * 60.0 * 1.1515;
        double distInMeters = distInMiles * 1.609344 * 1000.0;
        return distInMeters;
    }

    private void UpdateStatusText(string message)
    {
        if (StatusText != null) StatusText.text = message;
        Debug.Log("[PlanetWalkLocationManager] " + message);
    }
}
