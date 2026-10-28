using System;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace Alex80_IDE.Controls;

/// <summary>Evidenzia una riga senza spostare il cursore o modificare il documento.</summary>
public class ExecutionListingEditor : TextEditor
{
    protected override Type StyleKeyOverride => typeof(TextEditor);

    public static readonly StyledProperty<int> ExecutionLineProperty =
        AvaloniaProperty.Register<ExecutionListingEditor, int>(nameof(ExecutionLine));
    public static readonly StyledProperty<IBrush> ExecutionBrushProperty =
        AvaloniaProperty.Register<ExecutionListingEditor, IBrush>(nameof(ExecutionBrush), Brushes.Goldenrod);

    public int ExecutionLine
    {
        get => GetValue(ExecutionLineProperty);
        set => SetValue(ExecutionLineProperty, value);
    }

    public IBrush ExecutionBrush
    {
        get => GetValue(ExecutionBrushProperty);
        set => SetValue(ExecutionBrushProperty, value);
    }

    public ExecutionListingEditor() => TextArea.TextView.BackgroundRenderers.Add(new ExecutionRenderer(this));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ExecutionLineProperty || change.Property == ExecutionBrushProperty)
        {
            TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            if (Document is { } document && ExecutionLine > 0 && ExecutionLine <= document.LineCount)
                ScrollToLine(ExecutionLine);
        }
    }

    private sealed class ExecutionRenderer(ExecutionListingEditor editor) : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (!textView.VisualLinesValid || textView.Document is not { } document ||
                editor.ExecutionLine <= 0 || editor.ExecutionLine > document.LineCount) return;

            var line = document.GetLineByNumber(editor.ExecutionLine);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, line))
            {
                using (drawingContext.PushOpacity(0.22))
                    drawingContext.FillRectangle(editor.ExecutionBrush,
                        new Rect(0, rect.Y, textView.Bounds.Width, rect.Height));
                drawingContext.FillRectangle(editor.ExecutionBrush, new Rect(0, rect.Y, 3, rect.Height));
            }
        }
    }
}
