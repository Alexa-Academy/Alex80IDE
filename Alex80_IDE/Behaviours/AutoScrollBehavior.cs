using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class AutoScrollBehavior : Behavior<DataGrid>
{
    private ScrollBar? _sb;
    private IEnumerable<object>? _items;
    private bool _controlled;
    
    /// <summary>
    /// Dependency property to enable or disable auto-scroll.
    /// </summary>
    public static readonly StyledProperty<bool> IsAutoScrollEnabledProperty =
        AvaloniaProperty.Register<AutoScrollBehavior, bool>(nameof(IsAutoScrollEnabled), true);

    public bool IsAutoScrollEnabled
    {
        get => GetValue(IsAutoScrollEnabledProperty);
        set => SetValue(IsAutoScrollEnabledProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject is not null)
        {
            AssociatedObject.TemplateApplied += AssociatedObjectOnTemplateApplied;
        }
    }

    private void AssociatedObjectOnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _sb = e.NameScope.Find<ScrollBar>("PART_VerticalScrollbar");

        if (_sb is not null && AssociatedObject is not null)
        {
            _items = AssociatedObject.ItemsSource.Cast<object>();

            AssociatedObject.LayoutUpdated += AssociatedObject_LayoutUpdated;

            _sb.PointerEntered += Sb_PointerEntered;
            _sb.PointerExited += Sb_PointerExited;
        }
        else
        {
            Debug.WriteLine("Failed to bind autoscroll");
        }
    }

    private void Sb_PointerExited(object? sender, PointerEventArgs? e)
    {
        _controlled = false;
    }

    private void Sb_PointerEntered(object? sender, PointerEventArgs? e)
    {
        _controlled = true;
    }

    private bool CanScroll
    {
        get
        {
            return _sb is not null && _sb.Maximum - _sb.Value > 2;
        }
    }

    private void AssociatedObject_LayoutUpdated(object? sender, EventArgs? e)
    {
        if (!_controlled && IsAutoScrollEnabled)
        {
            ScrollToEnd();
        }
    }

    private void ScrollToEnd()
    {
        if (CanScroll && _items is not null && _items.Any())
        {
            AssociatedObject?.ScrollIntoView(_items.Last(), null);
        }
    }
}
