using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Input = UnityEngine.Input;

namespace DebugMod.Source;

internal class KeyBind {
    public KeyCode[] Keys;
    public Action Action;
}

public class KeybindManager {
    private List<KeyBind> keybindings = [];

    public int Count => keybindings.Count;

    public void Add(Action action, params KeyCode[] keys) {
        if (keys.Length == 0) throw new Exception("zero keys");
        keybindings.Add(new KeyBind() { Action = action, Keys = keys });
    }

    public void Update() {
        foreach (var keybind in keybindings) {
            var pressed = true;
            for (var i = 0; i < keybind.Keys.Length; i++) {
                var key = keybind.Keys[i];
                var last = i == keybind.Keys.Length - 1;

                pressed &= last ? Input.GetKeyDown(key) : Input.GetKey(key);
            }

            if (pressed) keybind.Action.Invoke();
        }
    }
}