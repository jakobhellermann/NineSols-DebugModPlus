using System.Collections.Generic;
using System.Linq;
using InControl;
using TMPro;

namespace DebugMod;

public class ToastManager {
    public const float MaxToastAge = 5;

    private record struct ToastMessage(float StartTime, string Text);

    private bool toastsDirty = false;
    private List<ToastMessage> toasts = [];

    private TMP_Text toastText;

    public static void Toast(object message) {
        Plugin.Instance.ToastManager.MakeToast(message);
    }


    private void MakeToast(object message) {
        toasts.Add(new ToastMessage(InputManager.CurrentTime, message.ToString()));
        toastsDirty = true;
    }


    public void Initialize(TMP_Text text) {
        toastText = text;
    }


    public void Update() {
        var now = InputManager.CurrentTime;
        toastsDirty |= toasts.RemoveAll(toast => now - toast.StartTime > MaxToastAge) > 0;

        if (toastsDirty) toastText.text = string.Join('\n', toasts.Select(toast => toast.Text));
    }
}