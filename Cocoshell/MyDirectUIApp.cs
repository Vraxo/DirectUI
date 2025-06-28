// MyDirectUIApp.cs
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Cocoshell.Input;
using Vortice.Mathematics;

namespace DirectUI;

public class MyDirectUIApp : Direct2DAppWindow
{
    // Application state remains here, where it belongs.
    private float sliderValue = 0.5f;
    private float leftPanelWidth = 250f;
    private float rightPanelWidth = 250f;
    private float bottomPanelHeight = 150f;

    // Window management state
    private ModalWindow? _projectWindow;
    private bool _isProjectWindowOpen = false;
    private int _projectWindowActiveTab = 0;

    // --- NEW STATE for Input Map Editor ---
    private Dictionary<string, List<InputBinding>>? _inputMap;
    private readonly string _inputMapPath = @"D:\Parsa Stuff\Visual Studio\Cosmocrush\Cherris\Res\Cherris\InputMap.yaml";
    private bool _inputMapDirty = false;
    private int _newActionCounter = 1;

    // --- Caching for performance ---
    private readonly List<string> _actionNamesCache = new();
    private readonly Dictionary<(string ActionName, int BindingIndex, string ElementKey), int> _idCache = new();
    private static readonly string[] ProjectWindowTabLabels = { "General", "Input Map" };


    private readonly TreeNode<string> _fileRoot;
    private readonly TreeStyle _treeStyle = new();

    public MyDirectUIApp(string title, int width, int height)
        : base(title, width, height)
    {
        // Data initialization remains here.
        try
        {
            string scenePath = @"D:\Parsa Stuff\Visual Studio\Cosmocrush\Cosmocrush\Res\Scenes\Player.yaml";
            if (File.Exists(scenePath))
            {
                _fileRoot = SceneParser.Parse(scenePath);
            }
            else
            {
                Console.WriteLine($"Warning: Scene file not found at '{scenePath}'. Loading default tree.");
                _fileRoot = CreateDefaultTree();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing scene file. Loading default tree. Error: {ex.Message}");
            _fileRoot = CreateDefaultTree();
        }

        // Load input map
        _inputMap = InputMapManager.Load(_inputMapPath);
        UpdateActionNamesCache();
    }

    private void UpdateActionNamesCache()
    {
        _actionNamesCache.Clear();
        if (_inputMap != null)
        {
            _actionNamesCache.AddRange(_inputMap.Keys);
        }
    }

    private void InvalidateIdCacheForAction(string actionName)
    {
        var keysToRemove = _idCache.Keys.Where(k => k.ActionName == actionName).ToList();
        foreach (var key in keysToRemove)
        {
            _idCache.Remove(key);
        }
    }

    private int GetIdHash(string actionName, int bindingIndex, string elementKey)
    {
        var key = (actionName, bindingIndex, elementKey);
        if (!_idCache.TryGetValue(key, out var idHash))
        {
            // OPTIMIZATION: Use HashCode.Combine to avoid string allocations and hashing overhead.
            idHash = bindingIndex < 0
                ? HashCode.Combine(elementKey, actionName)
                : HashCode.Combine(elementKey, actionName, bindingIndex);
            _idCache[key] = idHash;
        }
        return idHash;
    }


    protected override void OnKeyDown(Keys key)
    {
        if (key == Keys.F3)
        {
            if (_appHost != null)
            {
                _appHost.ShowFpsCounter = !_appHost.ShowFpsCounter;
            }
        }

        // Call the base implementation to handle input registration (AddKeyPressed)
        // and default behavior (like ESC to close).
        base.OnKeyDown(key);
    }

    private void ManageWindows()
    {
        // This logic is now called from DrawUI to ensure it runs in the correct context.

        // Case 1: We have a window instance we are tracking.
        if (_projectWindow != null)
        {
            // If the OS window has been closed (by user or OS), clean up our state.
            if (_projectWindow.Handle == IntPtr.Zero)
            {
                _projectWindow.Dispose(); // Ensure cleanup
                _projectWindow = null;
                _isProjectWindowOpen = false;
                _inputMapDirty = false; // Discard changes on close
            }
            // Else, if our app logic wants to close it (e.g. from a button click)
            else if (!_isProjectWindowOpen)
            {
                _projectWindow.Close(); // Asks the OS to close the window
            }
        }
    }


    // The factory method implementation creates the AppHost, passing the drawing logic.
    protected override AppHost CreateAppHost()
    {
        var backgroundColor = new Color4(21 / 255f, 21 / 255f, 21 / 255f, 1.0f); // #151515
        return new AppHost(DrawUI, backgroundColor);
    }

    private TreeNode<string> CreateDefaultTree()
    {
        var root = new TreeNode<string>("Error", "Could not load scene", true);
        root.AddChild("Please check file path and format.", "");
        root.AddChild(@"Path: D:\Parsa Stuff\Visual Studio\Cosmocrush\Cosmocrush\Res\Scenes\Player.yaml", "");
        return root;
    }

    // The actual drawing logic for the main window.
    private void DrawUI(UIContext context)
    {
        // Handle window state management at the beginning of the frame.
        ManageWindows();

        // Note: UI.BeginFrame and UI.EndFrame are now called by the AppHost.
        // We just need to define the UI content for the frame.

        float menuBarHeight = 30f;

        // --- Menu Bar ---
        {
            var rt = context.RenderTarget;
            var menuBarBackgroundBrush = UI.Resources.GetOrCreateBrush(rt, new Color4(37 / 255f, 37 / 255f, 38 / 255f, 1f));
            var menuBarBorderBrush = UI.Resources.GetOrCreateBrush(rt, DefaultTheme.NormalBorder);

            if (menuBarBackgroundBrush != null)
            {
                rt.FillRectangle(new Rect(0, 0, rt.Size.Width, menuBarHeight), menuBarBackgroundBrush);
            }
            if (menuBarBorderBrush != null)
            {
                rt.DrawLine(new Vector2(0, menuBarHeight - 1), new Vector2(rt.Size.Width, menuBarHeight - 1), menuBarBorderBrush, 1f);
            }

            var menuButtonSize = new Vector2(0, menuBarHeight);
            var menuButtonAlignment = new Alignment(HAlignment.Center, VAlignment.Center);
            var menuButtonTextMargin = new Vector2(10, 0);

            // Use the style stack to define the look for all menu buttons
            UI.PushStyleColor(StyleColor.Button, Colors.Transparent);
            UI.PushStyleColor(StyleColor.ButtonHovered, new Color4(63 / 255f, 63 / 255f, 70 / 255f, 1f));
            UI.PushStyleColor(StyleColor.ButtonPressed, DefaultTheme.Accent);
            UI.PushStyleColor(StyleColor.Text, new Color4(204 / 255f, 204 / 255f, 204 / 255f, 1f));


            UI.BeginHBoxContainer("menu_bar".GetHashCode(), new Vector2(5, 0), 0);
            if (UI.Button("file_button".GetHashCode(), "File", menuButtonSize, autoWidth: true, textMargin: menuButtonTextMargin, textAlignment: menuButtonAlignment)) { Console.WriteLine("File clicked"); }

            if (UI.Button("project_button".GetHashCode(), "Project", menuButtonSize, autoWidth: true, textMargin: menuButtonTextMargin, textAlignment: menuButtonAlignment))
            {
                // Create the window immediately upon click if it's not already open.
                if (!_isProjectWindowOpen)
                {
                    _projectWindow = new ModalWindow(this, "Project Settings", 600, 400, DrawProjectWindowUI);
                    if (_projectWindow.CreateAsModal())
                    {
                        _isProjectWindowOpen = true;
                    }
                    else
                    {
                        Console.WriteLine("Failed to create modal window.");
                        _projectWindow.Dispose();
                        _projectWindow = null;
                    }
                }
            }

            if (UI.Button("edit_button".GetHashCode(), "Edit", menuButtonSize, autoWidth: true, textMargin: menuButtonTextMargin, textAlignment: menuButtonAlignment)) { Console.WriteLine("Edit clicked"); }
            if (UI.Button("view_button".GetHashCode(), "View", menuButtonSize, autoWidth: true, textMargin: menuButtonTextMargin, textAlignment: menuButtonAlignment)) { Console.WriteLine("View clicked"); }
            if (UI.Button("help_button".GetHashCode(), "Help", menuButtonSize, autoWidth: true, textMargin: menuButtonTextMargin, textAlignment: menuButtonAlignment)) { Console.WriteLine("Help clicked"); }
            UI.EndHBoxContainer();

            UI.PopStyleColor(4); // Pop the 4 colors we pushed
        }

        var panelStyle = new BoxStyle
        {
            BorderLength = 1,
            Roundness = 0f
        };


        // --- Left Panel ---
        {
            UI.BeginResizableVPanel(
                "left_panel".GetHashCode(),
                ref leftPanelWidth,
                HAlignment.Left,
                menuBarHeight,
                minWidth: 150,
                maxWidth: 400,
                padding: new Vector2(10, 10),
                gap: 10,
                panelStyle: panelStyle);

            // Wrap tree in a VBox with 0 gap to ensure lines connect correctly
            UI.BeginVBoxContainer("tree_vbox".GetHashCode(), UI.Context.Layout.GetCurrentPosition(), 0);
            UI.Tree("file_tree".GetHashCode(), _fileRoot, out var clickedNode, _treeStyle);
            if (clickedNode is not null)
            {
                Console.WriteLine($"Tree Node Clicked: '{clickedNode.Text}', Path: {clickedNode.UserData}");
            }
            UI.EndVBoxContainer();

            UI.EndResizableVPanel();
        }

        // --- Right Panel ---
        {
            UI.BeginResizableVPanel(
                "right_panel".GetHashCode(),
                ref rightPanelWidth,
                HAlignment.Right,
                menuBarHeight,
                minWidth: 150,
                maxWidth: 400,
                padding: new Vector2(10, 10),
                gap: 10,
                panelStyle: panelStyle);

            if (UI.Button("right_button_1".GetHashCode(), "Right Panel"))
            {
                Console.WriteLine("Right panel button 1 clicked!");
            }
            if (UI.Button("right_button_2".GetHashCode(), "Another Button"))
            {
                Console.WriteLine("Right panel button 2 clicked!");
            }
            sliderValue = UI.HSlider("my_slider".GetHashCode(), sliderValue, 0f, 1f, new Vector2(200, 20));

            UI.EndResizableVPanel();
        }

        // --- Bottom Panel ---
        {
            UI.BeginResizableHPanel(
                "bottom_panel".GetHashCode(),
                ref bottomPanelHeight,
                leftPanelWidth,
                rightPanelWidth,
                menuBarHeight,
                minHeight: 50,
                maxHeight: 300,
                padding: new Vector2(10, 10),
                gap: 10,
                panelStyle: panelStyle);


            if (UI.Button("bottom_button".GetHashCode(), "Bottom Panel Button"))
            {
                Console.WriteLine("Bottom button clicked!");
            }

            UI.EndResizableHPanel();
        }
    }

    // Drawing logic for the modal window.
    private void DrawProjectWindowUI(UIContext context)
    {
        var rt = context.RenderTarget;
        float windowWidth = rt.Size.Width;
        float windowHeight = rt.Size.Height;
        float tabBarHeight = 30f;
        var contentArea = new Rect(0, tabBarHeight, windowWidth, windowHeight - tabBarHeight);

        // --- Draw Tab Bar ---
        UI.TabBar("project_tabs".GetHashCode(), ProjectWindowTabLabels, ref _projectWindowActiveTab);

        // --- Draw Content Panel and Content ---
        var modalPanelStyle = new BoxStyle
        {
            FillColor = new(37 / 255f, 37 / 255f, 38 / 255f, 1.0f),
            BorderColor = DefaultTheme.HoverBorder,
            BorderLengthTop = 1f,
            Roundness = 0f
        };
        UI.Resources.DrawBoxStyleHelper(rt, new Vector2(contentArea.X, contentArea.Y), new Vector2(contentArea.Width, contentArea.Height), modalPanelStyle);

        var contentPadding = new Vector2(10, 10);
        var paddedContentRect = new Rect(
            contentArea.X + contentPadding.X, contentArea.Y + contentPadding.Y,
            Math.Max(0, contentArea.Width - contentPadding.X * 2),
            Math.Max(0, contentArea.Height - contentPadding.Y * 2)
        );

        rt.PushAxisAlignedClip(paddedContentRect, Vortice.Direct2D1.AntialiasMode.Aliased);

        if (_projectWindowActiveTab == 0) // General Tab
        {
            UI.BeginVBoxContainer("tab_content_vbox_general".GetHashCode(), new Vector2(paddedContentRect.X, paddedContentRect.Y), 10);
            if (UI.Button("modal_button_1".GetHashCode(), "A button in a modal"))
            {
                Console.WriteLine("Modal button clicked!");
            }
            if (UI.Button("modal_button_close".GetHashCode(), "Close Me"))
            {
                _isProjectWindowOpen = false;
            }
            UI.EndVBoxContainer();
        }
        else if (_projectWindowActiveTab == 1 && _inputMap != null) // Input Map Tab
        {
            // --- DEFERRED MODIFICATION VARS ---
            string? actionToRemove = null;

            UI.BeginVBoxContainer("input_editor_main_vbox".GetHashCode(), new Vector2(paddedContentRect.X, paddedContentRect.Y), 15);

            UI.BeginVBoxContainer("actions_list_vbox".GetHashCode(), UI.Context.Layout.GetCurrentPosition(), 8);
            for (int i = 0; i < _actionNamesCache.Count; i++)
            {
                string actionName = _actionNamesCache[i];
                if (!_inputMap.ContainsKey(actionName)) continue;
                var bindings = _inputMap[actionName];
                int bindingToRemove = -1; // Deferred removal for bindings

                UI.BeginHBoxContainer(GetIdHash(actionName, -1, "action_header"), UI.Context.Layout.GetCurrentPosition(), 5);
                // Action Label (use a disabled button)
                UI.PushStyleColor(StyleColor.Button, Colors.Transparent);
                UI.PushStyleColor(StyleColor.TextDisabled, DefaultTheme.Text);
                UI.Button(GetIdHash(actionName, -1, "action_label"), actionName, disabled: true, autoWidth: true);
                UI.PopStyleColor(2);

                // Remove Action Button
                UI.PushStyleColor(StyleColor.Button, new Color4(0.5f, 0.2f, 0.2f, 1f));
                UI.PushStyleColor(StyleColor.ButtonHovered, new Color4(0.7f, 0.3f, 0.3f, 1f));
                UI.PushStyleColor(StyleColor.ButtonPressed, new Color4(0.9f, 0.4f, 0.4f, 1f));
                if (UI.Button(GetIdHash(actionName, -1, "remove_action"), "x", size: new Vector2(20, 20)))
                {
                    actionToRemove = actionName; // Defer removal
                }
                UI.PopStyleColor(3);
                UI.EndHBoxContainer();

                UI.BeginHBoxContainer(GetIdHash(actionName, -1, "bindings_outer_hbox"), UI.Context.Layout.GetCurrentPosition(), 0);
                UI.Button(GetIdHash(actionName, -1, "indent_spacer"), "", size: new Vector2(20, 0), disabled: true);

                UI.BeginVBoxContainer(GetIdHash(actionName, -1, "bindings_vbox"), UI.Context.Layout.GetCurrentPosition(), 5);
                for (int j = 0; j < bindings.Count; j++)
                {
                    var binding = bindings[j];
                    UI.BeginHBoxContainer(GetIdHash(actionName, j, "binding_row"), UI.Context.Layout.GetCurrentPosition(), 5);

                    // Style for Editor buttons
                    UI.PushStyleColor(StyleColor.Button, new Color4(0.25f, 0.25f, 0.3f, 1.0f));
                    UI.PushStyleColor(StyleColor.ButtonHovered, new Color4(0.35f, 0.35f, 0.4f, 1.0f));
                    UI.PushStyleColor(StyleColor.ButtonPressed, DefaultTheme.Accent);

                    if (UI.Button(GetIdHash(actionName, j, "binding_type"), binding.Type.ToString(), size: new Vector2(100, 24)))
                    {
                        binding.Type = (BindingType)(((int)binding.Type + 1) % Enum.GetValues(typeof(BindingType)).Length);
                        _inputMapDirty = true;
                    }
                    UI.PopStyleColor(3);

                    // Style for LineEdit
                    UI.PushStyleColor(StyleColor.Button, new Color4(0.2f, 0.2f, 0.25f, 1.0f)); // Normal background
                    UI.PushStyleColor(StyleColor.ButtonHovered, new Color4(0.22f, 0.22f, 0.27f, 1.0f)); // Hover/Focus background
                    UI.PushStyleColor(StyleColor.Border, new Color4(0.1f, 0.1f, 0.1f, 1.0f)); // Normal border
                    UI.PushStyleColor(StyleColor.BorderFocused, DefaultTheme.Accent); // Focus border

                    string tempKeyOrButton = binding.KeyOrButton;
                    var lineEditSize = new Vector2(120, 24);
                    if (UI.LineEdit(GetIdHash(actionName, j, "binding_key"), ref tempKeyOrButton, lineEditSize))
                    {
                        binding.KeyOrButton = tempKeyOrButton;
                        _inputMapDirty = true;
                    }
                    UI.PopStyleColor(4);

                    // Style for Remove Binding Button
                    UI.PushStyleColor(StyleColor.Button, new Color4(0.5f, 0.2f, 0.2f, 1f));
                    UI.PushStyleColor(StyleColor.ButtonHovered, new Color4(0.7f, 0.3f, 0.3f, 1f));
                    UI.PushStyleColor(StyleColor.ButtonPressed, new Color4(0.9f, 0.4f, 0.4f, 1f));
                    if (UI.Button(GetIdHash(actionName, j, "remove_binding"), "x", size: new Vector2(24, 24)))
                    {
                        bindingToRemove = j; // Defer removal
                    }
                    UI.PopStyleColor(3);

                    UI.EndHBoxContainer();
                }

                if (bindingToRemove != -1)
                {
                    bindings.RemoveAt(bindingToRemove);
                    InvalidateIdCacheForAction(actionName); // Clean cache for this action
                    _inputMapDirty = true;
                }

                // Style for "Add Binding" button
                UI.PushStyleColor(StyleColor.Button, new Color4(0.25f, 0.25f, 0.3f, 1.0f));
                UI.PushStyleColor(StyleColor.ButtonHovered, new Color4(0.35f, 0.35f, 0.4f, 1.0f));
                UI.PushStyleColor(StyleColor.ButtonPressed, DefaultTheme.Accent);
                if (UI.Button(GetIdHash(actionName, -1, "add_binding"), "Add Binding", size: new Vector2(100, 24)))
                {
                    bindings.Add(new InputBinding { Type = BindingType.Keyboard, KeyOrButton = "None" });
                    _inputMapDirty = true;
                }
                UI.PopStyleColor(3);

                UI.EndVBoxContainer();
                UI.EndHBoxContainer();
            }
            UI.EndVBoxContainer();

            // --- PERFORM DEFERRED ACTION REMOVAL ---
            if (actionToRemove != null)
            {
                _inputMap.Remove(actionToRemove);
                InvalidateIdCacheForAction(actionToRemove);
                UpdateActionNamesCache();
                _inputMapDirty = true;
            }

            UI.BeginHBoxContainer("input_editor_utils_hbox".GetHashCode(), UI.Context.Layout.GetCurrentPosition(), 10);

            // Style for bottom utility buttons
            UI.PushStyleColor(StyleColor.Button, DefaultTheme.NormalFill);
            UI.PushStyleColor(StyleColor.ButtonHovered, DefaultTheme.HoverFill);
            UI.PushStyleColor(StyleColor.ButtonPressed, DefaultTheme.Accent);
            UI.PushStyleColor(StyleColor.ButtonDisabled, DefaultTheme.DisabledFill);
            UI.PushStyleColor(StyleColor.TextDisabled, DefaultTheme.DisabledText);

            if (UI.Button("add_action".GetHashCode(), "Add New Action", autoWidth: true, textMargin: new Vector2(10, 5)))
            {
                string newActionName;
                do { newActionName = $"NewAction_{_newActionCounter++}"; } while (_inputMap.ContainsKey(newActionName));
                _inputMap[newActionName] = new List<InputBinding>();
                _actionNamesCache.Add(newActionName);
                _inputMapDirty = true;
            }

            if (UI.Button("apply_changes".GetHashCode(), "Apply Changes", disabled: !_inputMapDirty, autoWidth: true, textMargin: new Vector2(10, 5)))
            {
                InputMapManager.Save(_inputMapPath, _inputMap);
                _inputMapDirty = false;
            }

            if (UI.Button("revert_changes".GetHashCode(), "Revert", autoWidth: true, textMargin: new Vector2(10, 5)))
            {
                _inputMap = InputMapManager.Load(_inputMapPath);
                _idCache.Clear();
                UpdateActionNamesCache();
                _inputMapDirty = false;
            }

            UI.PopStyleColor(5);

            UI.EndHBoxContainer();

            UI.EndVBoxContainer();
        }

        rt.PopAxisAlignedClip();
    }
}