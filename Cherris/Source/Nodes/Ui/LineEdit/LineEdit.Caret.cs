using System.Globalization;using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace Cherris;

public partial class LineEdit
{
    protected class Caret : VisualItem
    {
        public float MaxTime { get; set; } = 0.5f;
        private const byte MinAlphaByte = 0;
        private const byte MaxAlphaByte = 255;
        private float _timer = 0;
        private float _alpha = 1.0f;        private LineEdit _parentLineEdit;

        private float _arrowKeyTimer = 0f;
        private const float ArrowKeyDelay = 0.4f;
        private const float ArrowKeySpeed = 0.04f;
        private bool _movingRight = false;
        private int _caretDisplayPositionX;        public int CaretDisplayPositionX
        {
            get => _caretDisplayPositionX;
            set
            {
                var maxVisibleChars = Math.Max(0, Math.Min(_parentLineEdit.GetDisplayableCharactersCount(), _parentLineEdit.Text.Length - _parentLineEdit.TextStartIndex));
                _caretDisplayPositionX = Math.Clamp(value, 0, maxVisibleChars);
                _alpha = 1.0f;                _timer = 0f;            }
        }

        public Caret(LineEdit parent)
        {
            _parentLineEdit = parent;
            Visible = false;        }

        public void UpdateLogic()        {
            if (!_parentLineEdit.Selected || !_parentLineEdit.Editable) return;

            HandleKeyboardInput();
            HandleMouseInput();
            UpdateAlpha();
        }

        public override void Draw(DrawingContext context)
        {
            if (!_parentLineEdit.Selected || !_parentLineEdit.Editable || !Visible || _alpha <= 0.01f)
            {
                return;
            }

            Rect layoutRect = GetCaretLayoutRect(context);
            if (layoutRect.Width <= 0 || layoutRect.Height <= 0) return;

            ButtonStyle caretStyle = new ButtonStyle
            {
                FontName = _parentLineEdit.Styles.Current.FontName,
                FontSize = _parentLineEdit.Styles.Current.FontSize,
                FontWeight = _parentLineEdit.Styles.Current.FontWeight,
                FontStyle = _parentLineEdit.Styles.Current.FontStyle,
                FontStretch = _parentLineEdit.Styles.Current.FontStretch,
                FontColor = new Color4(_parentLineEdit.Styles.Current.FontColor.R, _parentLineEdit.Styles.Current.FontColor.G, _parentLineEdit.Styles.Current.FontColor.B, _alpha),
                WordWrapping = WordWrapping.NoWrap
            };

            _parentLineEdit.DrawFormattedText(
                context,
                "|",
                layoutRect,
                caretStyle,
                HAlignment.Left,
                VAlignment.Center);
        }

        private void HandleKeyboardInput()
        {
            bool rightPressed = Input.IsKeyPressed(KeyCode.RightArrow);
            bool leftPressed = Input.IsKeyPressed(KeyCode.LeftArrow);

            if (rightPressed || leftPressed)
            {
                _movingRight = rightPressed;
                _arrowKeyTimer = 0f;
                MoveCaret(_movingRight ? 1 : -1);
            }
            else if (Input.IsKeyDown(KeyCode.RightArrow) || Input.IsKeyDown(KeyCode.LeftArrow))
            {
                if (Input.IsKeyDown(KeyCode.RightArrow)) _movingRight = true;
                else if (Input.IsKeyDown(KeyCode.LeftArrow)) _movingRight = false;

                _arrowKeyTimer += Time.Delta;
                if (_arrowKeyTimer >= ArrowKeyDelay)
                {
                    if ((_arrowKeyTimer - ArrowKeyDelay) % ArrowKeySpeed < Time.Delta)                    {
                        MoveCaret(_movingRight ? 1 : -1);
                    }
                }
            }
            else            {
                _arrowKeyTimer = 0f;
            }
        }

        private void HandleMouseInput()
        {
            if (Input.IsMouseButtonPressed(MouseButtonCode.Left))
            {
                Vector2 localMousePos = _parentLineEdit.GetLocalMousePosition();                Vector2 lineEditVisualTopLeft = _parentLineEdit.GlobalPosition - _parentLineEdit.Origin;
                Rect lineEditBounds = new Rect(
                    lineEditVisualTopLeft.X, lineEditVisualTopLeft.Y,
                    _parentLineEdit.Size.X, _parentLineEdit.Size.Y);

                if (lineEditBounds.Contains(localMousePos.X, localMousePos.Y))
                {
                    MoveCaretToMousePosition(localMousePos);
                }
            }
        }


        private void MoveCaret(int direction)
        {

            int newLogicalPos = _parentLineEdit.CaretLogicalPosition + direction;
            _parentLineEdit.CaretLogicalPosition = Math.Clamp(newLogicalPos, 0, _parentLineEdit.Text.Length);
            _parentLineEdit.UpdateCaretDisplayPositionAndStartIndex();
        }

        public void MoveCaretToMousePosition(Vector2 localMousePos)        {
            if (_parentLineEdit.Text.Length == 0)
            {
                _parentLineEdit.CaretLogicalPosition = 0;
                _parentLineEdit.UpdateCaretDisplayPositionAndStartIndex();
                return;
            }

            Direct2DAppWindow? owningWindow = _parentLineEdit.GetOwningWindow() as Direct2DAppWindow;
            if (owningWindow == null || owningWindow.DWriteFactory == null) return;            IDWriteFactory dwriteFactory = owningWindow.DWriteFactory;
            Vector2 lineEditVisualTopLeft = _parentLineEdit.GlobalPosition - _parentLineEdit.Origin;
            Vector2 textRenderAreaVisualTopLeft = lineEditVisualTopLeft + _parentLineEdit.TextOrigin;
            float mouseXInTextRenderArea = localMousePos.X - textRenderAreaVisualTopLeft.X;


            string visibleText = _parentLineEdit.Text.Substring(
                _parentLineEdit.TextStartIndex,
                Math.Min(_parentLineEdit.GetDisplayableCharactersCount(), _parentLineEdit.Text.Length - _parentLineEdit.TextStartIndex)
            );

            if (string.IsNullOrEmpty(visibleText))
            {
                _parentLineEdit.CaretLogicalPosition = (mouseXInTextRenderArea < 0 && _parentLineEdit.TextStartIndex > 0) ? _parentLineEdit.TextStartIndex : _parentLineEdit.TextStartIndex;
                _parentLineEdit.UpdateCaretDisplayPositionAndStartIndex();
                return;
            }

            IDWriteTextFormat? textFormat = owningWindow.GetOrCreateTextFormat(_parentLineEdit.Styles.Current);
            if (textFormat == null) return;

            using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(
                visibleText,
                textFormat,
                _parentLineEdit.Size.X,
                _parentLineEdit.Size.Y
            );

            textLayout.WordWrapping = WordWrapping.NoWrap;

            textLayout.HitTestPoint(mouseXInTextRenderArea, 0, out var isTrailingHit, out var isInside, out var metrics);

            int newCaretIndexInVisibleText = (int)metrics.TextPosition;
            if (isTrailingHit) newCaretIndexInVisibleText = (int)metrics.TextPosition + (int)metrics.Length;


            _parentLineEdit.CaretLogicalPosition = _parentLineEdit.TextStartIndex + Math.Clamp(newCaretIndexInVisibleText, 0, visibleText.Length);
            _parentLineEdit.UpdateCaretDisplayPositionAndStartIndex();
        }


        private Rect GetCaretLayoutRect(DrawingContext context)
        {
            Vector2 lineEditVisualTopLeft = _parentLineEdit.GlobalPosition - _parentLineEdit.Origin;
            Vector2 textRenderAreaVisualTopLeft = lineEditVisualTopLeft + _parentLineEdit.TextOrigin;

            float caretXOffset = 0;

            if (CaretDisplayPositionX > 0 && _parentLineEdit.Text.Length > 0)
            {
                int lengthOfTextBeforeCaretInVisiblePortion = Math.Min(CaretDisplayPositionX, _parentLineEdit.Text.Length - _parentLineEdit.TextStartIndex);

                if (lengthOfTextBeforeCaretInVisiblePortion > 0)                {
                    string textBeforeCaret = _parentLineEdit.Text.Substring(
                        _parentLineEdit.TextStartIndex,
                        lengthOfTextBeforeCaretInVisiblePortion
                    );

                    if (!string.IsNullOrEmpty(textBeforeCaret))                    {
                        var dwriteFactory = context.DWriteFactory;
                        var owningWindow = context.OwnerWindow;
                        IDWriteTextFormat? textFormat = owningWindow?.GetOrCreateTextFormat(_parentLineEdit.Styles.Current);

                        if (textFormat != null)
                        {
                            textFormat.WordWrapping = WordWrapping.NoWrap;
                            using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(
                                textBeforeCaret,
                                textFormat,
                                float.MaxValue,
                                _parentLineEdit.Size.Y);
                            caretXOffset = textLayout.Metrics.WidthIncludingTrailingWhitespace;                        }
                    }
                }
            }

            float caretWidth = _parentLineEdit.MeasureSingleCharWidth(context, "|", _parentLineEdit.Styles.Current);
            if (caretWidth <= 0) caretWidth = 2;

            float caretRectX = textRenderAreaVisualTopLeft.X + caretXOffset - caretWidth / 2f;
            float caretRectY = textRenderAreaVisualTopLeft.Y;
            float caretRectHeight = _parentLineEdit.Size.Y - _parentLineEdit.TextOrigin.Y * 2;


            return new Rect(
                caretRectX,
                caretRectY,
                caretWidth,
                Math.Max(0, caretRectHeight)
            );
        }


        private void UpdateAlpha()
        {
            _timer += Time.Delta;
            if (_timer > MaxTime)
            {
                _alpha = (_alpha == 1.0f) ? 0.0f : 1.0f;
                _timer = 0;
            }
        }
    }
}