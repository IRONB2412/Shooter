using UnityEngine;

/// <summary>
/// One-time runtime setup for mobile (Android) before the first scene loads:
///  - 60 FPS target (Android defaults to 30, which feels laggy), vsync off so the
///    target is respected. Low-end devices simply run as fast as they can.
///  - Screen never sleeps during play.
///  - Landscape only (twin-stick layout), auto-rotating between left/right.
/// </summary>
public static class PlatformSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // Match the display (90/120 Hz phones) but cap at 120 to save battery; never below 60.
        int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
        Application.targetFrameRate = Mathf.Clamp(hz, 60, 120);
        QualitySettings.vSyncCount = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }
}
