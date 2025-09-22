using System.Text;
using UnityEngine;
#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif
using TMPro; // ✅ Import TextMeshPro namespace

public class NativeGPSUI : MonoBehaviour
{
    public TextMeshProUGUI text;  // ✅ Changed from UnityEngine.UI.Text to TMP

    bool locationIsReady = false;
    bool locationGrantedAndroid = false;
    GameObject dialog = null;

    private void Start() 
    {
        #if PLATFORM_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
            dialog = new GameObject();
        }
        else
        {
            locationGrantedAndroid = true;
            locationIsReady = NativeGPSPlugin.StartLocation();
        }

        #elif PLATFORM_IOS
        locationIsReady = NativeGPSPlugin.StartLocation();
        #endif
    }

    private void Update() 
    {
        if (locationIsReady && text != null)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Longitude: " + NativeGPSPlugin.GetLongitude());
            sb.AppendLine("Latitude: " + NativeGPSPlugin.GetLatitude());
            sb.AppendLine("Accuracy: " + NativeGPSPlugin.GetAccuracy());
            sb.AppendLine("Altitude: " + NativeGPSPlugin.GetAltitude());
            sb.AppendLine("Speed: " + NativeGPSPlugin.GetSpeed());
            sb.AppendLine("Speed Accuracy (m/s): " + NativeGPSPlugin.GetSpeedAccuracyMetersPerSecond());
            sb.AppendLine("Vertical Accuracy (m): " + NativeGPSPlugin.GetVerticalAccuracyMeters());

            text.text = sb.ToString();
        }
    }

    void OnGUI()
    {
        #if PLATFORM_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            // The user denied permission to use the fineLocation.
            // Show rationale dialog
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
            }

            Destroy(dialog);
        }
        #endif
    }
}
