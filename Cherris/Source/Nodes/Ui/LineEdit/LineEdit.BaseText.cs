using Vortice.Mathematics;

namespace Cherris;

public partial class LineEdit
{
    private abstract class BaseText : VisualItem
    {
        protected LineEdit parentLineEdit;
        private Vector2 _textOffset = Vector2.Zero;
        public BaseText(LineEdit parent)
        {
            parentLineEdit = parent;
            Visible = false;
        }

        public Vector2 TextOffset
        {
            get => _textOffset;
            set => _textOffset = value;
        }

        public override void Draw(DrawingContext context)
        {
            if (!parentLineEdit.Visible || ShouldSkipDrawing() || string.IsNullOrEmpty(GetTextToDisplay()))
            {
                return;
            }

            Rect layoutRect = GetLayoutRect();

            parentLineEdit.DrawFormattedText(
                context,
                GetTextToDisplay(),
                layoutRect,
                parentLineEdit.Styles.Current,                HAlignment.Left,                VAlignment.Center);        }

        protected Rect GetLayoutRect()
        {
            Vector2 lineEditVisualTopLeft = parentLineEdit.GlobalPosition - parentLineEdit.Origin;
            Vector2 lineEditSize = parentLineEdit.Size;
            float textRenderAreaX = lineEditVisualTopLeft.X + parentLineEdit.TextOrigin.X + TextOffset.X;
            float textRenderAreaY = lineEditVisualTopLeft.Y + parentLineEdit.TextOrigin.Y + TextOffset.Y;
            float textRenderAreaWidth = lineEditSize.X - parentLineEdit.TextOrigin.X * 2;            float textRenderAreaHeight = lineEditSize.Y - parentLineEdit.TextOrigin.Y * 2;

            return new Rect(textRenderAreaX, textRenderAreaY, Math.Max(0, textRenderAreaWidth), Math.Max(0, textRenderAreaHeight));
        }

        protected abstract string GetTextToDisplay();
        protected abstract bool ShouldSkipDrawing();
    }
}