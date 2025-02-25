using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using InputExtension;
using NineSolsAPI;
using UnityEngine;

namespace DebugModPlus;

internal class DebugActionToggle {
    public bool Value;
    public required Action<bool> OnChange;
}

internal class DebugAction {
    public required Action OnChange;
}

public class DebugUI : MonoBehaviour {
    public bool settingsOpen = false;

    private GUIStyle? styleButton;
    private GUIStyle? styleToggle;

    private Dictionary<string, DebugActionToggle> toggles = new();
    private Dictionary<string, DebugAction> actions = new();

    private void Awake() {
        toggles.Clear();
    }

    public void AddBindableMethods(ConfigFile config, Type ty) {
        foreach (var method in ty.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            if (method.GetCustomAttribute<BindableMethod>() is { } attr) {
                var actionName = attr.Name ?? method.Name;
                var action = (Action)Delegate.CreateDelegate(typeof(Action), method);


                var shortcutName = new string(Array.FindAll(actionName.ToCharArray(), char.IsLetterOrDigit));
                var keyboardShortcut =
                    config.Bind("Shortcuts",
                        shortcutName,
                        attr.DefaultKeybind != null
                            ? new KeyboardShortcut(attr.DefaultKeybind[^1], attr.DefaultKeybind[..^1])
                            : new KeyboardShortcut());

                actions.Add(actionName, new DebugAction { OnChange = action });

                KeybindManager.Add(this, action, () => keyboardShortcut.Value);
            }
    }

    public void AddToggle(string actionName, Action<bool> onChange, bool defaultValue = false) {
        toggles.Add(actionName,
            new DebugActionToggle {
                Value = defaultValue,
                OnChange = onChange,
            });
    }

    private void OnGUI() {
        if (!settingsOpen) return;

        RCGInput.SetCursorVisible(true);

        const int padding = 20;
        styleButton ??= new GUIStyle(GUI.skin.box) {
            alignment = TextAnchor.MiddleRight,
            padding = new RectOffset(padding, padding, padding, padding),
            fontSize = 20,
        };

        styleToggle ??= new GUIStyle(GUI.skin.toggle) {
            // styleToggle.alignment = TextAnchor.MiddleLeft;
            // styleToggle.padding = new RectOffset(padding, padding, padding, padding);
            fontSize = 20,
        };

        GUILayout.BeginArea(new Rect(padding, padding, Screen.width - padding * 2, Screen.height - padding * 2));

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();


        GUILayout.BeginVertical();

        foreach (var (name, toggle) in toggles)
            if (GUILayout.Button($"{name}: {toggle.Value}", styleButton)) {
                toggle.Value = !toggle.Value;
                toggle.OnChange(toggle.Value);
                ToastManager.Toast($"change {name} to {toggle.Value}");
            }

        foreach (var (name, toggle) in actions)
            if (GUILayout.Button($"{name}", styleButton))
                toggle.OnChange();

        GUILayout.EndVertical();


        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }
}