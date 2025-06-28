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
    private readonly BoxStyle _modalPanelStyle;


    // Styles for the editor UI
    private readonly ButtonStylePack _labelStyle;
    private readonly ButtonStylePack _editorButtonStyle;
    private readonly ButtonStylePack _removeButtonStyle;
    private readonly ButtonStylePack _utilityButtonStyle;
    private readonly LineEditDefinition _lineEditDef;

    private readonly ButtonDefinition _reusableButtonDef = new();


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

        // --- NEW: Load input map ---
        _inputMap = InputMapManager.Load(_inputMapPath);
        UpdateActionNamesCache();

        // --- NEW: Initialize styles for editor ---
        _labelStyle = new ButtonStylePack { BorderLength = 0, Roundness = 0 };
        _labelStyle.Normal.FillColor = Colors.Transparent;
        _labelStyle.Disabled.FillColor = Colors.Transparent; // Important for disabled button as label
        _labelStyle.Disabled.FontColor = DefaultTheme.Text;

        _editorButtonStyle = new ButtonStylePack { FontSize = 12, Roundness = 0.2f };
        _editorButtonStyle.Normal.FillColor = new Color4(0.25f, 0.25f, 0.3f, 1.0f);
        _editorButtonStyle.Hover.FillColor = new Color4(0.35f, 0.35f, 0.4f, 1.0f);
        _editorButtonStyle.Pressed.FillColor = DefaultTheme.Accent;
        _editorButtonStyle.Disabled.FillColor = DefaultTheme.DisabledFill;
        _editorButtonStyle.Disabled.FontColor = DefaultTheme.DisabledText;


        _removeButtonStyle = new ButtonStylePack { FontSize = 12, Roundness = 0.5f, FontName = "Segoe UI Symbol" };
        _removeButtonStyle.Normal.FillColor = new Color4(0.5f, 0.2f, 0.2f, 1f);
        _removeButtonStyle.Hover.FillColor = new Color4(0.7f, 0.3f, 0.3f, 1f);
        _removeButtonStyle.Pressed.FillColor = new Color4(0.9f, 0.4f, 0.4f, 1f);

        _utilityButtonStyle = new ButtonStylePack { FontSize = 14, Roundness = 0.2f };
        _utilityButtonStyle.Normal.FillColor = DefaultTheme.NormalFill;
        _utilityButtonStyle.Hover.FillColor = DefaultTheme.HoverFill;
        _utilityButtonStyle.Pressed.FillColor = DefaultTheme.Accent;
        _utilityButtonStyle.Disabled.FillColor = DefaultTheme.DisabledFill;
        _utilityButtonStyle.Disabled.FontColor = DefaultTheme.DisabledText;

        // --- NEW: Define a style for the LineEdit ---
        var lineEditTheme = new ButtonStylePack { FontSize = 12, Roundness = 0.2f };
        lineEditTheme.Normal.FillColor = new Color4(0.2f, 0.2f, 0.25f, 1.0f);
        lineEditTheme.Normal.BorderColor = new Color4(0.1f, 0.1f, 0.1f, 1.0f);
        lineEditTheme.Hover.FillColor = new Color4(0.22f, 0.22f, 0.27f, 1.0f);
        lineEditTheme.Focused.FillColor = new Color4(0.22f, 0.22f, 0.27f, 1.0f);
        lineEditTheme.Focused.BorderColor = DefaultTheme.Accent;
        _lineEditDef = new LineEditDefinition
        {
            Size = new Vector2(120, 24),
            Theme = lineEditTheme
        };

        _modalPanelStyle = new BoxStyle
        {
            FillColor = new(37 / 255f, 37 / 255f, 38 / 255f, 1.0f),
            BorderColor = DefaultTheme.HoverBorder,
            BorderLengthTop = 1f,
            Roundness = 0f
        };
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
            // ID hash not found, generate string, hash it, and cache the hash.
            string idStr = bindingIndex < 0
                ? $"{elementKey}_{actionName}"
                : $"{elementKey}_{actionName}_{bindingIndex}";
            idHash = idStr.GetHashCode();
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

            var menuButtonTheme = new ButtonStylePack
            {
                Roundness = 0f,
                BorderLength = 0,
                FontName = "Segoe UI",
                FontSize = 14
            };
            menuButtonTheme.Normal.FillColor = Colors.Transparent;
            menuButtonTheme.Normal.FontColor = new Color4(204 / 255f, 204 / 255f, 204 / 255f, 1f);
            menuButtonTheme.Hover.FillColor = new Color4(63 / 255f, 63 / 255f, 70 / 255f, 1f);
            menuButtonTheme.Pressed.FillColor = DefaultTheme.Accent;

            var menuButtonDef = new ButtonDefinition
            {
                Theme = menuButtonTheme,
                AutoWidth = true,
                TextMargin = new Vector2(10, 0),
                Size = new Vector2(0, menuBarHeight),
                TextAlignment = new Alignment(HAlignment.Center, VAlignment.Center)
            };

            UI.BeginHBoxContainer("menu_bar".GetHashCode(), new Vector2(5, 0), 0);
            menuButtonDef.Text = "File";
            if (UI.Button("file_button".GetHashCode(), menuButtonDef)) { Console.WriteLine("File clicked"); }

            menuButtonDef.Text = "Project";
            if (UI.Button("project_button".GetHashCode(), menuButtonDef))
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

            menuButtonDef.Text = "Edit";
            if (UI.Button("edit_button".GetHashCode(), menuButtonDef)) { Console.WriteLine("Edit clicked"); }
            menuButtonDef.Text = "View";
            if (UI.Button("view_button".GetHashCode(), menuButtonDef)) { Console.WriteLine("View clicked"); }
            menuButtonDef.Text = "Help";
            if (UI.Button("help_button".GetHashCode(), menuButtonDef)) { Console.WriteLine("Help clicked"); }
            UI.EndHBoxContainer();
        }

        // --- Define shared styles ---
        var buttonTheme = new ButtonStylePack
        {
            Roundness = 0.2f,
            BorderLength = 1, // Thinner border
            FontName = "Segoe UI",
            FontSize = 16,
        };

        var panelStyle = new BoxStyle
        {
            BorderLength = 1,
            Roundness = 0f
        };

        var vPanelDef = new ResizablePanelDefinition
        {
            MinWidth = 150,
            MaxWidth = 400,
            Padding = new Vector2(10, 10),
            Gap = 10,
            PanelStyle = panelStyle
        };

        var hPanelDef = new ResizableHPanelDefinition
        {
            MinHeight = 50,
            MaxHeight = 300,
            Padding = new Vector2(10, 10),
            Gap = 10,
            PanelStyle = panelStyle
        };


        // --- Left Panel ---
        {
            UI.BeginResizableVPanel("left_panel".GetHashCode(), ref leftPanelWidth, vPanelDef, HAlignment.Left, menuBarHeight);

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
            UI.BeginResizableVPanel("right_panel".GetHashCode(), ref rightPanelWidth, vPanelDef, HAlignment.Right, menuBarHeight);

            if (UI.Button("right_button_1".GetHashCode(), new ButtonDefinition { Text = "Right Panel", Theme = buttonTheme }))
            {
                Console.WriteLine("Right panel button 1 clicked!");
            }
            if (UI.Button("right_button_2".GetHashCode(), new ButtonDefinition { Text = "Another Button", Theme = buttonTheme }))
            {
                Console.WriteLine("Right panel button 2 clicked!");
            }
            sliderValue = UI.HSlider("my_slider".GetHashCode(), sliderValue, new SliderDefinition { Size = new Vector2(200, 20) });

            UI.EndResizableVPanel();
        }

        // --- Bottom Panel ---
        {
            UI.BeginResizableHPanel("bottom_panel".GetHashCode(), ref bottomPanelHeight, hPanelDef, leftPanelWidth, rightPanelWidth, menuBarHeight);

            if (UI.Button("bottom_button".GetHashCode(), new ButtonDefinition { Text = "Bottom Panel Button", Theme = buttonTheme }))
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
        UI.Resources.DrawBoxStyleHelper(rt, new Vector2(contentArea.X, contentArea.Y), new Vector2(contentArea.Width, contentArea.Height), _modalPanelStyle);

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
            if (UI.Button("modal_button_1".GetHashCode(), new ButtonDefinition { Text = "A button in a modal" }))
            {
                Console.WriteLine("Modal button clicked!");
            }
            if (UI.Button("modal_button_close".GetHashCode(), new ButtonDefinition { Text = "Close Me" }))
            {
                _isProjectWindowOpen = false;
            }
            UI.EndVBoxContainer();
        }
        else if (_projectWindowActiveTab == 1 && _inputMap != null) // Input Map Tab
        {
            var buttonDef = _reusableButtonDef;

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
                buttonDef.Text = actionName;
                buttonDef.Theme = _labelStyle;
                buttonDef.Disabled = true;
                buttonDef.AutoWidth = true;
                buttonDef.TextMargin = null;
                buttonDef.Size = default;
                UI.Button(GetIdHash(actionName, -1, "action_label"), buttonDef);

                buttonDef.Text = "x";
                buttonDef.Theme = _removeButtonStyle;
                buttonDef.Size = new Vector2(20, 20);
                buttonDef.Disabled = false;
                buttonDef.AutoWidth = false;
                if (UI.Button(GetIdHash(actionName, -1, "remove_action"), buttonDef))
                {
                    actionToRemove = actionName; // Defer removal
                }
                UI.EndHBoxContainer();

                UI.BeginHBoxContainer(GetIdHash(actionName, -1, "bindings_outer_hbox"), UI.Context.Layout.GetCurrentPosition(), 0);
                buttonDef.Size = new Vector2(20, 0);
                buttonDef.Theme = _labelStyle;
                buttonDef.Disabled = true;
                buttonDef.Text = "";
                UI.Button(GetIdHash(actionName, -1, "indent_spacer"), buttonDef);

                UI.BeginVBoxContainer(GetIdHash(actionName, -1, "bindings_vbox"), UI.Context.Layout.GetCurrentPosition(), 5);
                for (int j = 0; j < bindings.Count; j++)
                {
                    var binding = bindings[j];
                    UI.BeginHBoxContainer(GetIdHash(actionName, j, "binding_row"), UI.Context.Layout.GetCurrentPosition(), 5);

                    buttonDef.Text = binding.Type.ToString();
                    buttonDef.Theme = _editorButtonStyle;
                    buttonDef.Size = new Vector2(100, 24);
                    buttonDef.Disabled = false;
                    if (UI.Button(GetIdHash(actionName, j, "binding_type"), buttonDef))
                    {
                        binding.Type = (BindingType)(((int)binding.Type + 1) % Enum.GetValues(typeof(BindingType)).Length);
                        _inputMapDirty = true;
                    }

                    string tempKeyOrButton = binding.KeyOrButton;
                    if (UI.LineEdit(GetIdHash(actionName, j, "binding_key"), ref tempKeyOrButton, _lineEditDef))
                    {
                        binding.KeyOrButton = tempKeyOrButton;
                        _inputMapDirty = true;
                    }

                    buttonDef.Text = "x";
                    buttonDef.Theme = _removeButtonStyle;
                    buttonDef.Size = new Vector2(24, 24);
                    if (UI.Button(GetIdHash(actionName, j, "remove_binding"), buttonDef))
                    {
                        bindingToRemove = j; // Defer removal
                    }
                    UI.EndHBoxContainer();
                }

                if (bindingToRemove != -1)
                {
                    bindings.RemoveAt(bindingToRemove);
                    InvalidateIdCacheForAction(actionName); // Clean cache for this action
                    _inputMapDirty = true;
                }

                buttonDef.Text = "Add Binding";
                buttonDef.Theme = _editorButtonStyle;
                buttonDef.Size = new Vector2(100, 24);
                if (UI.Button(GetIdHash(actionName, -1, "add_binding"), buttonDef))
                {
                    bindings.Add(new InputBinding { Type = BindingType.Keyboard, KeyOrButton = "None" });
                    _inputMapDirty = true;
                }
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
            buttonDef.Text = "Add New Action";
            buttonDef.Theme = _utilityButtonStyle;
            buttonDef.AutoWidth = true;
            buttonDef.TextMargin = new Vector2(10, 5);
            buttonDef.Size = default;
            buttonDef.Disabled = false;
            if (UI.Button("add_action".GetHashCode(), buttonDef))
            {
                string newActionName;
                do { newActionName = $"NewAction_{_newActionCounter++}"; } while (_inputMap.ContainsKey(newActionName));
                _inputMap[newActionName] = new List<InputBinding>();
                _actionNamesCache.Add(newActionName);
                _inputMapDirty = true;
            }

            buttonDef.Text = "Apply Changes";
            buttonDef.Theme = _utilityButtonStyle;
            buttonDef.Disabled = !_inputMapDirty;
            if (UI.Button("apply_changes".GetHashCode(), buttonDef))
            {
                InputMapManager.Save(_inputMapPath, _inputMap);
                _inputMapDirty = false;
            }

            buttonDef.Text = "Revert";
            buttonDef.Disabled = false;
            if (UI.Button("revert_changes".GetHashCode(), buttonDef))
            {
                _inputMap = InputMapManager.Load(_inputMapPath);
                _idCache.Clear();
                UpdateActionNamesCache();
                _inputMapDirty = false;
            }

            buttonDef.AutoWidth = false;
            buttonDef.TextMargin = null;

            UI.EndHBoxContainer();

            UI.EndVBoxContainer();
        }

        rt.PopAxisAlignedClip();
    }
}