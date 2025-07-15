using System.Globalization;
using SharpGen.Runtime;
using Vortice.DirectWrite;

namespace Cherris;

public partial class LineEdit : Button
{
    public static readonly Vector2 DefaultLineEditSize = new(200, 28);

    public new string Text
    {
        get;
        set
        {
            if (field == value) return;

            string oldText = field;
            field = value ?? "";

            if (field.Length > MaxCharacters)
            {
                field = field.Substring(0, MaxCharacters);
            }

            UpdateCaretDisplayPositionAndStartIndex();
            TextChanged?.Invoke(this, field);

            if (oldText.Length == 0 && field.Length > 0)
            {
                FirstCharacterEntered?.Invoke(this, EventArgs.Empty);
            }

            if (oldText.Length > 0 && field.Length == 0)
            {
                Cleared?.Invoke(this, EventArgs.Empty);
            }
        }
    } = "";

    public string PlaceholderText { get; set; } = "";
    public Vector2 TextOrigin { get; set; } = new(6, 0);
    public int MaxCharacters { get; set; } = int.MaxValue;
    public List<char> ValidCharacters { get; set; } = [];

    public bool Selected
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _caret.Visible = field && Editable;
            
            if (!field)
            {
                _caret.CaretDisplayPositionX = 0;
            }
        }
    } = false;

    public bool Editable { get; set; } = true;
    public bool ExpandWidthToText { get; set; } = false;
    public bool Secret { get; set; } = false;
    public char SecretCharacter { get; set; } = '*';
    public bool AutoScrollToShowFullText { get; set; } = true;

    public int TextStartIndex { get; internal set; } = 0;

    internal int CaretLogicalPosition { get; set; } = 0;

    public event EventHandler? FirstCharacterEntered;
    public event EventHandler? Cleared;
    public event EventHandler<string>? TextChanged;
    public event EventHandler<string>? Confirmed;

    private readonly Caret _caret;
    private readonly TextDisplayer TextDisplayerSub;
    private readonly PlaceholderTextDisplayer _placeholderTextDisplayer;

    private const float BackspaceDelay = 0.5f;
    private const float BackspaceSpeed = 0.05f;
    private const float UndoDelay = 0.5f;
    private const float UndoSpeed = 0.05f;

    private float _backspaceTimer = 0f;
    private bool _backspaceHeld = false;
    private float _undoTimer = 0f;
    private bool _undoHeld = false;
    private bool _backspaceCtrlHeld = false;

    private readonly Stack<LineEditState> _undoStack = new();
    private readonly Stack<LineEditState> _redoStack = new();
    private const int HistoryLimit = 50;

    private char? _pendingCharInput = null;

    public LineEdit()
    {
        _caret = new Caret(this);
        TextDisplayerSub = new TextDisplayer(this);
        _placeholderTextDisplayer = new PlaceholderTextDisplayer(this);

        Visible = true;
        Size = DefaultLineEditSize;
        TextHAlignment = HAlignment.Left;
        TextVAlignment = VAlignment.Center;

        Text = "Type here...";

        Styles.Normal.BorderLength = 1;
        Styles.Focused.BorderLength = 1;
        Styles.Focused.BorderColor = DefaultTheme.FocusBorder;
        Styles.WordWrapping = WordWrapping.NoWrap;
        FocusChanged += OnFocusChangedHandler;
        LeftClicked += OnLeftClickedHandler;
        ClickedOutside += OnClickedOutsideHandler;
        LayerChanged += OnLayerChangedHandler;
        SizeChanged += OnSizeChangedHandler;
    }

    public override void Process()
    {
        base.Process();
        CaptureCharInput();

        if (Editable && Selected)
        {
            HandleCharacterInput();
            HandleBackspace();
            HandleDelete();
            HandleHomeEndKeys();
            HandleClipboardPaste();
            HandleUndoRedo();
            ConfirmOnEnter();
        }
        _caret.UpdateLogic();
        UpdateSizeToFitTextIfEnabled();
    }

    public override void Draw(DrawingContext context)
    {
        base.Draw(context);
        _placeholderTextDisplayer.Draw(context);
        TextDisplayerSub.Draw(context);

        if (Selected && Editable)
        {
            _caret.Visible = true;
            _caret.Draw(context);
        }
        else
        {
            _caret.Visible = false;
        }
    }

    private void CaptureCharInput()
    {
        if (_pendingCharInput == null)
        {
            _pendingCharInput = Input.ConsumeNextTypedChar();
        }
    }

    protected override void OnEnterPressed()
    {
        if (Editable)
        {
            ConfirmAction();
        }
    }

    private void OnFocusChangedHandler(Control control)
    {
        Selected = control.Focused;
    }

    private void OnLeftClickedHandler()
    {
        if (Editable)
        {
            Selected = true;
        }
    }

    private void OnClickedOutsideHandler(Control control)
    {
        if (Selected)
        {
            Selected = false;
        }
    }

    private void OnLayerChangedHandler(VisualItem sender, int layer)
    {
    }

    private void OnSizeChangedHandler(object? sender, Vector2 newSize)
    {
        TextStartIndex = 0;
        UpdateCaretDisplayPositionAndStartIndex();
    }

    private void UpdateSizeToFitTextIfEnabled()
    {
        if (!ExpandWidthToText || !Visible) return;

        var owningWindow = GetOwningWindow() as Direct2DAppWindow;
        if (owningWindow == null || owningWindow.DWriteFactory == null) return;
        IDWriteFactory dwriteFactory = owningWindow.DWriteFactory;

        string textToMeasure = string.IsNullOrEmpty(Text) ? PlaceholderText : Text;
        if (string.IsNullOrEmpty(textToMeasure))
        {
            Size = new Vector2(TextOrigin.X * 2 + 20, Size.Y);
            return;
        }

        float measuredWidth = MeasureTextWidth(dwriteFactory, textToMeasure, Styles.Current);
        Size = new Vector2(measuredWidth + TextOrigin.X * 2, Size.Y);
    }

    internal float MeasureTextWidth(IDWriteFactory dwriteFactory, string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text)) return 0f;

        using IDWriteTextFormat textFormat = dwriteFactory.CreateTextFormat(
            style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, CultureInfo.CurrentCulture.Name);
        textFormat.WordWrapping = WordWrapping.NoWrap;

        using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(text, textFormat, float.MaxValue, float.MaxValue);
        return textLayout.Metrics.WidthIncludingTrailingWhitespace;
    }

    internal float MeasureSingleCharWidth(DrawingContext context, string character, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(character)) return 0f;
        IDWriteFactory dwriteFactory = context.DWriteFactory;
        if (dwriteFactory == null) return 0f;

        using IDWriteTextFormat textFormat = dwriteFactory.CreateTextFormat(
            style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, CultureInfo.CurrentCulture.Name);
        textFormat.WordWrapping = WordWrapping.NoWrap;

        using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(character, textFormat, float.MaxValue, float.MaxValue);
        return textLayout.Metrics.Width;
    }

    public void Insert(string textToInsert)
    {
        if (!Editable || string.IsNullOrEmpty(textToInsert)) return;

        PushStateForUndo();
        foreach (char c in textToInsert)
        {
            InsertCharacterLogic(c);
        }
    }

    private void HandleCharacterInput()
    {
        if (!_pendingCharInput.HasValue)
        {
            return;
        }

        char typedChar = _pendingCharInput.Value;
        _pendingCharInput = null;

        if (Text.Length >= MaxCharacters)
        {
            return;
        }

        if (ValidCharacters.Count != 0 && !ValidCharacters.Contains(typedChar))
        {
            return;
        }

        PushStateForUndo();
        InsertCharacterLogic(typedChar);
    }

    private void InsertCharacterLogic(char c)
    {
        if (Text.Length >= MaxCharacters) return;

        Text = Text.Insert(CaretLogicalPosition, c.ToString());
        CaretLogicalPosition++;
        UpdateCaretDisplayPositionAndStartIndex();
    }

    private void HandleBackspace()
    {
        bool ctrlHeld = Input.IsKeyDown(KeyCode.LeftControl) || Input.IsKeyDown(KeyCode.RightControl);

        if (Input.IsKeyPressed(KeyCode.Backspace))
        {
            _backspaceHeld = true;
            _backspaceTimer = 0f;
            _backspaceCtrlHeld = ctrlHeld;
            PerformBackspaceAction(_backspaceCtrlHeld);
        }
        else if (Input.IsKeyDown(KeyCode.Backspace) && _backspaceHeld)
        {
            _backspaceTimer += Time.Delta;
            if (_backspaceTimer >= BackspaceDelay)
            {
                if ((_backspaceTimer - BackspaceDelay) % BackspaceSpeed < Time.Delta)
                {
                    PerformBackspaceAction(_backspaceCtrlHeld);
                }
            }
        }
        else if (Input.IsKeyReleased(KeyCode.Backspace))
        {
            _backspaceHeld = false;
            _backspaceTimer = 0f;
        }
    }

    private void PerformBackspaceAction(bool isCtrlHeld)
    {
        if (Text.Length == 0 || CaretLogicalPosition == 0) return;

        PushStateForUndo();
        if (isCtrlHeld)
        {
            int originalCaretPos = CaretLogicalPosition;
            int wordStart = FindPreviousWordStart(Text, originalCaretPos);
            Text = Text.Remove(wordStart, originalCaretPos - wordStart);
            CaretLogicalPosition = wordStart;
        }
        else
        {
            Text = Text.Remove(CaretLogicalPosition - 1, 1);
            CaretLogicalPosition--;
        }
        UpdateCaretDisplayPositionAndStartIndex();
    }

    private int FindPreviousWordStart(string text, int currentPos)
    {
        if (currentPos == 0) return 0;
        int pos = currentPos - 1;
        while (pos > 0 && char.IsWhiteSpace(text[pos])) pos--;
        while (pos > 0 && !char.IsWhiteSpace(text[pos - 1])) pos--;
        return pos;
    }

    private void HandleDelete()
    {
        if (Input.IsKeyPressed(KeyCode.Delete))
        {
            if (CaretLogicalPosition < Text.Length)
            {
                PushStateForUndo();
                Text = Text.Remove(CaretLogicalPosition, 1);
                UpdateCaretDisplayPositionAndStartIndex();
            }
        }
    }

    private void HandleHomeEndKeys()
    {
        if (Input.IsKeyPressed(KeyCode.Home))
        {
            CaretLogicalPosition = 0;
            UpdateCaretDisplayPositionAndStartIndex();
        }
        else if (Input.IsKeyPressed(KeyCode.End))
        {
            CaretLogicalPosition = Text.Length;
            UpdateCaretDisplayPositionAndStartIndex();
        }
    }

    private void HandleClipboardPaste()
    {
        if ((Input.IsKeyDown(KeyCode.LeftControl) || Input.IsKeyDown(KeyCode.RightControl)) && Input.IsKeyPressed(KeyCode.V))
        {
            try
            {
                string clipboardText = "";
                Log.Warning("Clipboard.GetText() is currently disabled. Project setup required for System.Windows.Forms.");

                if (!string.IsNullOrEmpty(clipboardText))
                {
                    clipboardText = clipboardText.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                    Insert(clipboardText);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error pasting from clipboard: {ex.Message}");
            }
        }
    }

    private void HandleUndoRedo()
    {
        bool ctrlHeld = Input.IsKeyDown(KeyCode.LeftControl) || Input.IsKeyDown(KeyCode.RightControl);

        if (ctrlHeld && Input.IsKeyPressed(KeyCode.Z))
        {
            _undoHeld = true;
            _undoTimer = 0f;
            Undo();
        }
        else if (ctrlHeld && Input.IsKeyDown(KeyCode.Z) && _undoHeld)
        {
            _undoTimer += Time.Delta;
            if (_undoTimer >= UndoDelay)
            {
                if ((_undoTimer - UndoDelay) % UndoSpeed < Time.Delta)
                {
                    Undo();
                }
            }
        }
        else if (Input.IsKeyReleased(KeyCode.Z))
        {
            _undoHeld = false;
            _undoTimer = 0f;
        }

        if (ctrlHeld && Input.IsKeyPressed(KeyCode.Y))
        {
            Redo();
        }
    }

    private void ConfirmOnEnter()
    {
        if (Input.IsKeyPressed(KeyCode.Enter))
        {
            ConfirmAction();
        }
    }

    private void ConfirmAction()
    {
        Selected = false;
        Confirmed?.Invoke(this, Text);
    }

    internal IDWriteFactory? GetDWriteFactory()
    {
        var owningWindow = GetOwningWindow() as Direct2DAppWindow;
        return owningWindow?.DWriteFactory;
    }

    internal int GetDisplayableCharactersCount()
    {
        if (Size.X <= TextOrigin.X * 2) return 0;

        float availableWidth = Size.X - TextOrigin.X * 2;
        if (availableWidth <= 0) return 0;

        IDWriteFactory? dwriteFactory = GetDWriteFactory();
        if (dwriteFactory == null)
        {
            Log.Warning("LineEdit.GetDisplayableCharactersCount: DWriteFactory not available. Falling back to rough estimate.");
            return (int)(availableWidth / 8);
        }

        if (TextStartIndex >= Text.Length && Text.Length > 0)
        {
            return 0;
        }
        if (Text.Length == 0) return 0;

        string textToMeasure = Text.Substring(TextStartIndex);
        if (string.IsNullOrEmpty(textToMeasure)) return 0;

        IDWriteTextFormat? textFormat = (GetOwningWindow() as Direct2DAppWindow)?.GetOrCreateTextFormat(Styles.Current);
        if (textFormat == null)
        {
            Log.Warning("LineEdit.GetDisplayableCharactersCount: Could not get TextFormat. Falling back to rough estimate.");
            return (int)(availableWidth / 8);
        }
        textFormat.WordWrapping = WordWrapping.NoWrap;

        using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(
            textToMeasure,
            textFormat,
            float.MaxValue,
            Size.Y
        );

        ClusterMetrics[] clusterMetricsBuffer = new ClusterMetrics[textToMeasure.Length];
        Result result = textLayout.GetClusterMetrics(clusterMetricsBuffer, out uint actualClusterCount);

        if (result.Failure)
        {
            Log.Error($"LineEdit.GetDisplayableCharactersCount: GetClusterMetrics failed with HRESULT {result.Code}");
            return (int)(availableWidth / 8);
        }

        if (actualClusterCount == 0) return 0;

        float currentCumulativeWidth = 0;
        int displayableCharacterLengthInSubstring = 0;

        for (int i = 0; i < actualClusterCount; i++)
        {
            ClusterMetrics cluster = clusterMetricsBuffer[i];
            if (currentCumulativeWidth + cluster.Width <= availableWidth)
            {
                currentCumulativeWidth += cluster.Width;
                displayableCharacterLengthInSubstring += (int)cluster.Length;
            }
            else
            {
                break;
            }
        }
        return displayableCharacterLengthInSubstring;
    }

    internal void UpdateCaretDisplayPositionAndStartIndex()
    {
        if (Text.Length == 0)
        {
            TextStartIndex = 0;
            _caret.CaretDisplayPositionX = 0;
            CaretLogicalPosition = 0;
            return;
        }

        float availableWidth = Size.X - TextOrigin.X * 2;
        if (availableWidth <= 0)
        {
            TextStartIndex = 0;
            _caret.CaretDisplayPositionX = 0;
            return;
        }

        if (AutoScrollToShowFullText)
        {
            IDWriteFactory? dwriteFactory = GetDWriteFactory();
            if (dwriteFactory != null)
            {
                float fullTextWidth = MeasureTextWidth(dwriteFactory, Text, Styles.Current);
                if (fullTextWidth <= availableWidth)
                {
                    TextStartIndex = 0;
                    _caret.CaretDisplayPositionX = CaretLogicalPosition;
                    return;
                }
            }
        }

        int displayableChars = GetDisplayableCharactersCount();
        if (displayableChars <= 0 && Text.Length > 0)
        {
            TextStartIndex = CaretLogicalPosition;
            _caret.CaretDisplayPositionX = 0;
            TextStartIndex = Math.Clamp(TextStartIndex, 0, Text.Length);
            return;
        }

        if (AutoScrollToShowFullText)
        {
            int maxVisibleChars = GetDisplayableCharactersCount(0);
            if (maxVisibleChars > displayableChars && TextStartIndex > 0)
            {
                int charactersToShow = maxVisibleChars - displayableChars;
                TextStartIndex = Math.Max(0, TextStartIndex - charactersToShow);
                displayableChars = GetDisplayableCharactersCount();
            }
        }

        if (CaretLogicalPosition < TextStartIndex)
        {
            TextStartIndex = CaretLogicalPosition;
        }
        else if (CaretLogicalPosition >= TextStartIndex + displayableChars)
        {
            TextStartIndex = CaretLogicalPosition - displayableChars + 1;
        }

        TextStartIndex = Math.Max(0, TextStartIndex);
        if (TextStartIndex + displayableChars > Text.Length && displayableChars > 0)
        {
            TextStartIndex = Math.Max(0, Text.Length - displayableChars);
        }
        TextStartIndex = Math.Clamp(TextStartIndex, 0, Math.Max(0, Text.Length - 1));

        _caret.CaretDisplayPositionX = CaretLogicalPosition - TextStartIndex;
        _caret.CaretDisplayPositionX = Math.Clamp(_caret.CaretDisplayPositionX, 0, displayableChars > 0 ? displayableChars : 0);
    }

    private int GetDisplayableCharactersCount(int startIndex)
    {
        if (Size.X <= TextOrigin.X * 2) return 0;

        float availableWidth = Size.X - TextOrigin.X * 2;
        if (availableWidth <= 0) return 0;

        IDWriteFactory? dwriteFactory = GetDWriteFactory();
        if (dwriteFactory == null)
        {
            Log.Warning("LineEdit.GetDisplayableCharactersCount: DWriteFactory not available. Falling back to rough estimate.");
            return (int)(availableWidth / 8);
        }

        if (startIndex >= Text.Length && Text.Length > 0) return 0;
        if (Text.Length == 0) return 0;

        string textToMeasure = Text.Substring(startIndex);
        if (string.IsNullOrEmpty(textToMeasure)) return 0;

        IDWriteTextFormat? textFormat = (GetOwningWindow() as Direct2DAppWindow)?.GetOrCreateTextFormat(Styles.Current);
        if (textFormat == null)
        {
            Log.Warning("LineEdit.GetDisplayableCharactersCount: Could not get TextFormat. Falling back to rough estimate.");
            return (int)(availableWidth / 8);
        }
        textFormat.WordWrapping = WordWrapping.NoWrap;

        using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(
            textToMeasure,
            textFormat,
            float.MaxValue,
            Size.Y
        );

        ClusterMetrics[] clusterMetricsBuffer = new ClusterMetrics[textToMeasure.Length];
        Result result = textLayout.GetClusterMetrics(clusterMetricsBuffer, out uint actualClusterCount);

        if (result.Failure)
        {
            Log.Error($"LineEdit.GetDisplayableCharactersCount: GetClusterMetrics failed with HRESULT {result.Code}");
            return (int)(availableWidth / 8);
        }

        if (actualClusterCount == 0) return 0;

        float currentCumulativeWidth = 0;
        int displayableCharacterLengthInSubstring = 0;

        for (int i = 0; i < actualClusterCount; i++)
        {
            ClusterMetrics cluster = clusterMetricsBuffer[i];
            if (currentCumulativeWidth + cluster.Width <= availableWidth)
            {
                currentCumulativeWidth += cluster.Width;
                displayableCharacterLengthInSubstring += (int)cluster.Length;
            }
            else
            {
                break;
            }
        }
        return displayableCharacterLengthInSubstring;
    }

    private void PushStateForUndo()
    {
        if (_undoStack.Count > 0 && _undoStack.Peek().Text == Text && _undoStack.Peek().CaretPosition == CaretLogicalPosition)
        {
            return;
        }
        if (_undoStack.Count >= HistoryLimit)
        {
            var tempList = _undoStack.ToList();
            tempList.RemoveAt(0);
            _undoStack.Clear();
            foreach (var state in tempList.AsEnumerable().Reverse())
            {
                _undoStack.Push(state);
            }
        }
        _undoStack.Push(new LineEditState(Text, CaretLogicalPosition, TextStartIndex));
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.Count > 0)
        {
            LineEditState currentState = new LineEditState(Text, CaretLogicalPosition, TextStartIndex);
            _redoStack.Push(currentState);
            if (_redoStack.Count > HistoryLimit)
            {
                var tempList = _redoStack.ToList();
                tempList.RemoveAt(0);
                _redoStack.Clear();
                foreach (var state in tempList.AsEnumerable().Reverse())
                {
                    _redoStack.Push(state);
                }
            }

            LineEditState previousState = _undoStack.Pop();
            Text = previousState.Text;
            CaretLogicalPosition = previousState.CaretPosition;
            TextStartIndex = previousState.TextStartIndex;

            TextChanged?.Invoke(this, Text);
            UpdateCaretDisplayPositionAndStartIndex();
        }
    }

    public void Redo()
    {
        if (_redoStack.Count > 0)
        {
            LineEditState currentState = new LineEditState(Text, CaretLogicalPosition, TextStartIndex);
            _undoStack.Push(currentState);
            if (_undoStack.Count > HistoryLimit)
            {
                var tempList = _undoStack.ToList();
                tempList.RemoveAt(0);
                _undoStack.Clear();
                foreach (var state in tempList.AsEnumerable().Reverse())
                {
                    _undoStack.Push(state);
                }
            }

            LineEditState nextState = _redoStack.Pop();
            Text = nextState.Text;
            CaretLogicalPosition = nextState.CaretPosition;
            TextStartIndex = nextState.TextStartIndex;

            TextChanged?.Invoke(this, Text);
            UpdateCaretDisplayPositionAndStartIndex();
        }
    }

    protected record LineEditState(string Text, int CaretPosition, int TextStartIndex);

    protected Vector2 GetLocalMousePosition()
    {
        var owningWindowNode = GetOwningWindowNode();
        if (owningWindowNode != null)
        {
            return owningWindowNode.LocalMousePosition;
        }

        var mainAppWindow = ApplicationServer.Instance.GetMainAppWindow();
        if (mainAppWindow != null)
        {
            return mainAppWindow.GetLocalMousePosition();
        }

        Log.Warning($"LineEdit '{Name}': Could not determine owning window for local mouse position. Using global Input.MousePosition.");
        return Input.MousePosition;
    }
}