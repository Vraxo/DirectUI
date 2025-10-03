using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using DirectUI.Core;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace DirectUI.Backends.SkiaSharp;

internal record TextRun(string Text, SKTypeface Typeface, Vector2 Size, SKFontMetrics FontMetrics);

internal class SilkNetTextLayout : ITextLayout
{
    private readonly List<TextRun> _runs = new();
    public IReadOnlyList<TextRun> Runs => _runs;

    public Vector2 Size { get; }
    public string Text { get; }

    public SilkNetTextLayout(string text, ButtonStyle style, SilkNetTextService textService)
    {
        Text = text;
        BuildRuns(text, style, textService);

        float totalWidth = 0;
        float maxHeight = 0;
        foreach (var run in _runs)
        {
            totalWidth += run.Size.X;
            if (run.Size.Y > maxHeight)
            {
                maxHeight = run.Size.Y;
            }
        }
        Size = new Vector2(totalWidth, maxHeight);
    }

    private void BuildRuns(string text, ButtonStyle style, SilkNetTextService textService)
    {
        if (string.IsNullOrEmpty(text)) return;

        var fontManager = SKFontManager.Default;
        var primaryTypeface = textService.GetOrCreateTypeface(style);

        var currentRunText = new StringBuilder();
        var currentTypeface = primaryTypeface;

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var grapheme = enumerator.GetTextElement();
            var firstRune = grapheme.EnumerateRunes().First();

            if (currentTypeface.ContainsGlyph(firstRune.Value))
            {
                currentRunText.Append(grapheme);
            }
            else
            {
                if (currentRunText.Length > 0)
                {
                    AddRun(currentRunText.ToString(), currentTypeface, style.FontSize);
                    currentRunText.Clear();
                }

                // --- THE FIX ---
                // 1. First, specifically request a font that supports the character as an EMOJI.
                var emojiFallback = fontManager.MatchCharacter(null, SKFontStyle.Normal, new[] { "und-Zsye" }, firstRune.Value);

                // 2. If that fails, fall back to the generic search (which might find symbols, etc.).
                var genericFallback = fontManager.MatchCharacter(firstRune.Value);

                // 3. Use the best available option, defaulting to the primary typeface.
                currentTypeface = emojiFallback ?? genericFallback ?? primaryTypeface;
                // --- END FIX ---

                currentRunText.Append(grapheme);
            }
        }

        if (currentRunText.Length > 0)
        {
            AddRun(currentRunText.ToString(), currentTypeface, style.FontSize);
        }
    }

    private void AddRun(string text, SKTypeface typeface, float fontSize)
    {
        using var font = new SKFont(typeface, fontSize);
        using var paint = new SKPaint(font);
        using var shaper = new SKShaper(typeface);

        var shapeResult = shaper.Shape(text, paint);
        var width = shapeResult.Width;
        var fontMetrics = paint.FontMetrics;
        var height = fontMetrics.Descent - fontMetrics.Ascent;

        _runs.Add(new TextRun(text, typeface, new Vector2(width, height), fontMetrics));
    }


    public TextHitTestMetrics HitTestTextPosition(int textPosition, bool isTrailingHit)
    {
        textPosition = Math.Clamp(textPosition, 0, Text.Length);
        float totalX = 0;
        int charCount = 0;

        foreach (var run in _runs)
        {
            if (textPosition <= charCount + run.Text.Length)
            {
                int positionInRun = textPosition - charCount;
                string sub = run.Text[..positionInRun];
                using var font = new SKFont(run.Typeface, run.FontMetrics.XHeight * 2); // Size doesn't matter much for this
                using var paint = new SKPaint(font);
                using var shaper = new SKShaper(run.Typeface);

                float xInRun = shaper.Shape(sub, paint).Width;

                float graphemeWidth = 0;
                if (positionInRun < run.Text.Length)
                {
                    var enumerator = StringInfo.GetTextElementEnumerator(run.Text, positionInRun);
                    if (enumerator.MoveNext())
                    {
                        graphemeWidth = shaper.Shape(enumerator.GetTextElement(), paint).Width;
                    }
                }
                return new TextHitTestMetrics(new Vector2(totalX + xInRun, 0), new Vector2(graphemeWidth, Size.Y));
            }
            totalX += run.Size.X;
            charCount += run.Text.Length;
        }

        return new TextHitTestMetrics(new Vector2(Size.X, 0), new Vector2(1, Size.Y));
    }

    public TextHitTestResult HitTestPoint(Vector2 point)
    {
        if (string.IsNullOrEmpty(Text))
            return new TextHitTestResult(0, false, false, new TextHitTestMetrics(Vector2.Zero, Vector2.Zero));

        bool isInside = point.X >= 0 && point.X <= Size.X && point.Y >= 0 && point.Y <= Size.Y;
        float currentRunX = 0;
        int overallCharIndex = 0;

        foreach (var run in _runs)
        {
            if (point.X < currentRunX + run.Size.X || run == _runs.Last())
            {
                float xInRun = point.X - currentRunX;
                using var font = new SKFont(run.Typeface, run.FontMetrics.XHeight * 2);
                using var paint = new SKPaint(font);
                using var shaper = new SKShaper(run.Typeface);

                var enumerator = StringInfo.GetTextElementEnumerator(run.Text);
                float currentGraphemeX = 0;

                while (enumerator.MoveNext())
                {
                    string grapheme = enumerator.GetTextElement();
                    float graphemeWidth = shaper.Shape(grapheme, paint).Width;
                    if (xInRun < currentGraphemeX + graphemeWidth)
                    {
                        bool isTrailing = xInRun > currentGraphemeX + graphemeWidth / 2f;
                        int hitCharIndex = overallCharIndex + enumerator.ElementIndex + (isTrailing ? grapheme.Length : 0);
                        var metrics = new TextHitTestMetrics(new Vector2(currentRunX + currentGraphemeX, 0), new Vector2(graphemeWidth, run.Size.Y));
                        return new TextHitTestResult(hitCharIndex, isTrailing, isInside, metrics);
                    }
                    currentGraphemeX += graphemeWidth;
                }
            }

            currentRunX += run.Size.X;
            overallCharIndex += run.Text.Length;
        }

        return new TextHitTestResult(Text.Length, false, isInside, new TextHitTestMetrics(new Vector2(Size.X, 0), Vector2.Zero));
    }

    public void Dispose() { }
}