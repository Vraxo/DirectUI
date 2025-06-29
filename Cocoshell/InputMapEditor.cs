using System;
using System.Collections.Generic;
using System.Numerics;
using Cocoshell.Input;
using Vortice.Mathematics;

namespace DirectUI;

/// <summary>
/// Encapsulates the state and drawing logic for the Input Map Editor tab.
/// </summary>
public class InputMapEditor
{
    // State
    private Dictionary<string, List<InputBinding>> _inputMap;
    private readonly string _inputMapPath;
    private bool _inputMapDirty = false;
    private int _newActionCounter = 1;

    // Caches
    private readonly List<string> _actionNamesCache = new();
    private static readonly Dictionary<BindingType, string> s_bindingTypeStringCache = new();

    static InputMapEditor()
    {
        // Pre-cache enum strings for performance.
        foreach (BindingType val in Enum.GetValues(typeof(BindingType)))
        {
            s_bindingTypeStringCache[val] = val.ToString();
        }
    }

    public InputMapEditor(string inputMapPath)
    {
        _inputMapPath = inputMapPath;
        _inputMap = LoadMap();
        UpdateActionNamesCache();
    }

    public bool IsDirty() => _inputMapDirty;

    public void RevertChanges()
    {
        _inputMap = LoadMap();
        UpdateActionNamesCache();
        _inputMapDirty = false;
    }

    public void SaveChanges()
    {
        InputMapManager.Save(_inputMapPath, _inputMap);
        _inputMapDirty = false;
    }

    public void Draw(UIContext context, Rect contentArea)
    {
        var paddedContentRect = new Rect(
            contentArea.X + 10, contentArea.Y + 10,
            Math.Max(0, contentArea.Width - 20),
            Math.Max(0, contentArea.Height - 20)
        );

        string? actionToRemove = null;
        var scrollableSize = new Vector2(paddedContentRect.Width, paddedContentRect.Height - 40);

        // Outer VBox to hold the scroll region and the buttons below it
        UI.BeginVBoxContainer("input_map_vbox", paddedContentRect.TopLeft, 10);
        {
            // --- Scrollable list of actions and bindings ---
            UI.BeginScrollableRegion("input_map_scroll", scrollableSize);
            // FIX: Pass the current layout position to the inner VBox so it inherits the scroll offset.
            // Previously, this was Vector2.Zero, which caused the content to ignore the scroll container.
            UI.BeginVBoxContainer("input_map_scroll_content", UI.Context.Layout.GetCurrentPosition(), 8); // Inner VBox for item spacing
            {
                // Use a standard for-loop for safe removal from the cache during iteration
                for (int i = 0; i < _actionNamesCache.Count; i++)
                {
                    string actionName = _actionNamesCache[i];
                    if (!_inputMap.TryGetValue(actionName, out var bindings)) continue;

                    string? removedAction = DrawInputMapAction(actionName, bindings);
                    if (removedAction != null)
                    {
                        actionToRemove = removedAction;
                    }
                }
            }
            UI.EndVBoxContainer();
            UI.EndScrollableRegion();

            if (actionToRemove != null)
            {
                _inputMap.Remove(actionToRemove);
                UpdateActionNamesCache();
                _inputMapDirty = true;
            }

            // --- Action Buttons (Add, Apply, Revert) ---
            DrawActionButtons();
        }
        UI.EndVBoxContainer();
    }

    private string? DrawInputMapAction(string actionName, List<InputBinding> bindings)
    {
        string? actionToRemove = null;
        int bindingToRemove = -1;

        // --- Action Header (Name + Remove Button) ---
        UI.BeginHBoxContainer($"action_hbox_{actionName}", UI.Context.Layout.GetCurrentPosition(), 5);
        {
            UI.PushStyleColor(StyleColor.Button, Colors.Transparent);
            UI.PushStyleColor(StyleColor.TextDisabled, DefaultTheme.Text);
            UI.Button($"action_label_{actionName}", actionName, disabled: true, autoWidth: true);
            UI.PopStyleColor(2);

            UI.PushStyleVar(StyleVar.FrameRounding, 0.5f);
            UI.PushStyleColor(StyleColor.Button, new Color4(0.5f, 0.2f, 0.2f, 1f));
            if (UI.Button($"action_remove_{actionName}", "x", size: new Vector2(20, 20))) actionToRemove = actionName;
            UI.PopStyleColor();
            UI.PopStyleVar();
        }
        UI.EndHBoxContainer();

        // --- Indented VBox for bindings ---
        UI.BeginHBoxContainer($"bindings_outer_hbox_{actionName}", UI.Context.Layout.GetCurrentPosition(), 0);
        {
            UI.Button($"indent_spacer_{actionName}", "", size: new Vector2(20, 0), disabled: true); // Indent
            UI.BeginVBoxContainer($"bindings_vbox_{actionName}", UI.Context.Layout.GetCurrentPosition(), 5);
            {
                for (int j = 0; j < bindings.Count; j++)
                {
                    var binding = bindings[j];
                    UI.BeginHBoxContainer($"binding_hbox_{actionName}_{j}", UI.Context.Layout.GetCurrentPosition(), 5);
                    {
                        // Binding Type Button
                        if (UI.Button($"binding_type_{actionName}_{j}", s_bindingTypeStringCache[binding.Type], size: new Vector2(100, 24)))
                        {
                            binding.Type = (BindingType)(((int)binding.Type + 1) % Enum.GetValues(typeof(BindingType)).Length);
                            _inputMapDirty = true;
                        }

                        // Key/Button Text Edit
                        string tempKey = binding.KeyOrButton;
                        if (UI.LineEdit($"binding_key_{actionName}_{j}", ref tempKey, new Vector2(120, 24)))
                        {
                            binding.KeyOrButton = tempKey;
                            _inputMapDirty = true;
                        }

                        // Remove Binding Button
                        UI.PushStyleVar(StyleVar.FrameRounding, 0.5f);
                        UI.PushStyleColor(StyleColor.Button, new Color4(0.5f, 0.2f, 0.2f, 1f));
                        if (UI.Button($"binding_remove_{actionName}_{j}", "x", size: new Vector2(24, 24))) bindingToRemove = j;
                        UI.PopStyleColor();
                        UI.PopStyleVar();
                    }
                    UI.EndHBoxContainer();
                }

                if (bindingToRemove != -1)
                {
                    bindings.RemoveAt(bindingToRemove);
                    _inputMapDirty = true;
                }

                // "Add Binding" button for this action
                if (UI.Button($"add_binding_{actionName}", "Add Binding", size: new Vector2(100, 24)))
                {
                    bindings.Add(new InputBinding { Type = BindingType.Keyboard, KeyOrButton = "None" });
                    _inputMapDirty = true;
                }
            }
            UI.EndVBoxContainer();
        }
        UI.EndHBoxContainer();

        return actionToRemove;
    }

    private void DrawActionButtons()
    {
        UI.BeginHBoxContainer("input_map_actions", UI.Context.Layout.GetCurrentPosition(), 10);
        {
            UI.PushStyleVar(StyleVar.FrameRounding, 0.2f);
            UI.PushStyleColor(StyleColor.Button, DefaultTheme.NormalFill);
            UI.PushStyleColor(StyleColor.ButtonDisabled, DefaultTheme.DisabledFill);
            UI.PushStyleColor(StyleColor.TextDisabled, DefaultTheme.DisabledText);

            if (UI.Button("add_new_action", "Add New Action", autoWidth: true, textMargin: new Vector2(10, 5)))
            {
                string newActionName;

                do
                {
                    newActionName = $"NewAction_{_newActionCounter++}";
                }
                while (_inputMap.ContainsKey(newActionName));

                _inputMap[newActionName] = [];
                UpdateActionNamesCache();
                _inputMapDirty = true;
            }

            if (UI.Button("apply_changes", "Apply Changes", disabled: !_inputMapDirty, autoWidth: true, textMargin: new Vector2(10, 5)))
            {
                SaveChanges();
            }

            if (UI.Button("revert_changes", "Revert", autoWidth: true, textMargin: new Vector2(10, 5)))
            {
                RevertChanges();
            }

            UI.PopStyleColor(3);
            UI.PopStyleVar();
        }
        UI.EndHBoxContainer();
    }

    private Dictionary<string, List<InputBinding>> LoadMap()
    {
        try
        {
            return InputMapManager.Load(_inputMapPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load input map: {ex.Message}");
            return [];
        }
    }

    private void UpdateActionNamesCache()
    {
        _actionNamesCache.Clear();

        if (_inputMap is null)
        {
            return;
        }

        _actionNamesCache.AddRange(_inputMap.Keys);
    }
}