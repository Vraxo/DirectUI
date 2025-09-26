using System.Numerics;
using Cocoshell.Input;
using Vortice.Mathematics;
using DirectUI;
using DirectUI.Core;

namespace Cocoshell;

public class InputMapEditor
{
    private Dictionary<string, List<InputBinding>> _inputMap;
    private readonly string _inputMapPath;
    private bool _inputMapDirty = false;
    private int _newActionCounter = 1;
    private (string ActionName, int BindingIndex)? _listeningForBinding;
    private bool _ignoreInputForOneFrame;
    private readonly List<string> _actionNamesCache = new();
    private static readonly string[] s_bindingTypeNames = Enum.GetNames(typeof(BindingType));

    public InputMapEditor(string inputMapPath)
    {
        _inputMapPath = inputMapPath;
        _inputMap = LoadMap();
        UpdateActionNamesCache();
    }

    public bool IsDirty()
    {
        return _inputMapDirty;
    }

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
        // --- Input Listening Logic ---
        HandleInputListening(context);

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
            UI.BeginScrollableRegion("input_map_scroll", scrollableSize, out _);
            UI.BeginVBoxContainer("input_map_scroll_content", UI.Context.Layout.GetCurrentPosition(), 8);
            {
                for (int i = 0; i < _actionNamesCache.Count; i++)
                {
                    string actionName = _actionNamesCache[i];
                    if (!_inputMap.TryGetValue(actionName, out var bindings)) continue;

                    string? removedAction = DrawInputMapAction(actionName, bindings);
                    if (removedAction is not null)
                    {
                        actionToRemove = removedAction;
                    }
                }
            }
            UI.EndVBoxContainer();
            UI.EndScrollableRegion();

            if (actionToRemove is not null)
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

    private void HandleInputListening(UIContext context)
    {
        if (_ignoreInputForOneFrame)
        {
            _ignoreInputForOneFrame = false;
            return;
        }

        if (!_listeningForBinding.HasValue) return;

        var (actionName, bindingIndex) = _listeningForBinding.Value;
        var input = context.InputState;

        // Ensure the binding still exists before trying to modify it
        if (!_inputMap.TryGetValue(actionName, out var bindings) || bindingIndex >= bindings.Count)
        {
            _listeningForBinding = null;
            return;
        }

        InputBinding targetBinding = bindings[bindingIndex];
        bool inputWasBound = false;

        // Check for Keyboard Input
        if (input.PressedKeys.Count > 0)
        {
            targetBinding.Type = BindingType.Keyboard;
            targetBinding.KeyOrButton = input.PressedKeys[0].ToString();
            inputWasBound = true;
        }
        // Check for Mouse Input
        else if (input.PressedMouseButtons.Count > 0)
        {
            targetBinding.Type = BindingType.MouseButton;
            targetBinding.KeyOrButton = input.PressedMouseButtons[0].ToString();
            inputWasBound = true;
        }
        // Check if user clicked somewhere else to cancel
        else if (input.WasLeftMousePressedThisFrame || input.WasRightMousePressedThisFrame)
        {
            // This click didn't start a new binding listen, so it must be a cancel click.
            _listeningForBinding = null;
        }

        if (inputWasBound)
        {
            _inputMapDirty = true;
            _listeningForBinding = null; // Stop listening
        }
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
                        // Binding Type Combobox
                        int selectedIndex = (int)binding.Type;
                        if (UI.Combobox($"binding_type_{actionName}_{j}", ref selectedIndex, s_bindingTypeNames, new Vector2(100, 24)))
                        {
                            binding.Type = (BindingType)selectedIndex;
                            _inputMapDirty = true;
                        }

                        // Button to display binding and enter listening mode
                        string buttonText = binding.KeyOrButton;
                        bool isThisOneListening = _listeningForBinding.HasValue &&
                                                  _listeningForBinding.Value.ActionName == actionName &&
                                                  _listeningForBinding.Value.BindingIndex == j;

                        if (isThisOneListening)
                        {
                            buttonText = "Press a key...";
                        }

                        if (UI.Button($"binding_key_{actionName}_{j}", buttonText, new Vector2(150, 24)))
                        {
                            if (!isThisOneListening)
                            {
                                // Start listening for this binding
                                _listeningForBinding = (actionName, j);
                                _ignoreInputForOneFrame = true;
                            }
                            else
                            {
                                // If already listening, clicking again cancels
                                _listeningForBinding = null;
                            }
                        }

                        // Remove Binding Button
                        UI.PushStyleVar(StyleVar.FrameRounding, 0.5f);
                        UI.PushStyleColor(StyleColor.Button, new(0.5f, 0.2f, 0.2f, 1f));

                        if (UI.Button($"binding_remove_{actionName}_{j}", "x", size: new(24, 24)))
                        {
                            bindingToRemove = j;
                        }

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
                if (UI.Button($"add_binding_{actionName}", "Add Binding", size: new(100, 24)))
                {
                    bindings.Add(new()
                    {
                        Type = BindingType.Keyboard,
                        KeyOrButton = "None"
                    });

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

            if (UI.Button("apply_changes", "Apply Changes", disabled: !_inputMapDirty, autoWidth: true, textMargin: new(10, 5)))
            {
                SaveChanges();
            }

            if (UI.Button("revert_changes", "Revert", autoWidth: true, textMargin: new(10, 5)))
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