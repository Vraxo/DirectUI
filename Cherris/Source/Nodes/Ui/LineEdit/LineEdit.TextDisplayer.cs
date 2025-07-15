namespace Cherris;

public partial class LineEdit
{
    private class TextDisplayer : BaseText
    {
        public TextDisplayer(LineEdit parent) : base(parent)
        {
        }

        protected override string GetTextToDisplay()
        {
            if (string.IsNullOrEmpty(parentLineEdit.Text))
            {
                return "";
            }

            string textToDisplay = parentLineEdit.Text;

            if (parentLineEdit.Secret)
            {
                textToDisplay = new string(parentLineEdit.SecretCharacter, textToDisplay.Length);
            }
            int startIndex = Math.Clamp(parentLineEdit.TextStartIndex, 0, textToDisplay.Length);
            int availableLengthFromStartIndex = textToDisplay.Length - startIndex;
            int count = Math.Min(parentLineEdit.GetDisplayableCharactersCount(), availableLengthFromStartIndex);

            if (count <= 0) return "";
            return textToDisplay.Substring(startIndex, count);
        }

        protected override bool ShouldSkipDrawing()
        {
            return string.IsNullOrEmpty(parentLineEdit.Text);
        }
    }
}