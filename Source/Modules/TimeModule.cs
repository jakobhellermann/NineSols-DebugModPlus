using System.Collections;

namespace DebugModPlus.Modules;

public class TimeModule {
    [BindableMethod(Name = "Toggle Fastforward")]
    private static void OnFastForwardChange() {
        RCGTime.GlobalSimulationSpeed = RCGTime.GlobalSimulationSpeed == 1 ? 2 : 1;
    }

    [BindableMethod(Name = "Play/Pause")]
    private static void PlayPause() {
        RCGTime.GlobalSimulationSpeed = RCGTime.GlobalSimulationSpeed == 0 ? 1 : 0;
    }

    [BindableMethod(Name = "Advance Frame")]
    private static void FrameAdvance() {
        RCGTime.GlobalSimulationSpeed = 0;

        DebugModPlus.Instance.StartCoroutine(AdvanceFrameCoro());
    }

    private static IEnumerator AdvanceFrameCoro() {
        RCGTime.GlobalSimulationSpeed = 1;
        yield return null;
        RCGTime.GlobalSimulationSpeed = 0;
    }
}