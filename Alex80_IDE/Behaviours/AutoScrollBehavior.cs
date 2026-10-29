using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections;
using System.Linq;
using System.Windows.Input;

public class AutoScrollBehavior : Behavior<DataGrid>
{
    private ScrollBar? _scrollBar;
    
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

    public static readonly StyledProperty<ICommand?> SuspendAutoScrollCommandProperty =
        AvaloniaProperty.Register<AutoScrollBehavior, ICommand?>(nameof(SuspendAutoScrollCommand));

    public ICommand? SuspendAutoScrollCommand
    {
        get => GetValue(SuspendAutoScrollCommandProperty);
        set => SetValue(SuspendAutoScrollCommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject is not { } grid) return;
        grid.TemplateApplied += OnTemplateApplied;
        grid.LayoutUpdated += OnLayoutUpdated;
        // Pause before DataGrid handles the gesture, without consuming it.
        grid.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel, true);
        grid.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, true);
        grid.AddHandler(Gestures.ScrollGestureEvent, OnScrollGesture, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
    }

    protected override void OnDetaching()
    {
        DetachScrollBar();
        if (AssociatedObject is { } grid)
        {
            grid.TemplateApplied -= OnTemplateApplied;
            grid.LayoutUpdated -= OnLayoutUpdated;
            grid.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            grid.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            grid.RemoveHandler(Gestures.ScrollGestureEvent, OnScrollGesture);
        }
        base.OnDetaching();
    }

    private void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        DetachScrollBar();
        _scrollBar = e.NameScope.Find<ScrollBar>("PART_VerticalScrollbar");
        _scrollBar?.AddHandler(InputElement.PointerPressedEvent, OnScrollBarPressed, RoutingStrategies.Tunnel, true);
    }

    private void DetachScrollBar()
    {
        _scrollBar?.RemoveHandler(InputElement.PointerPressedEvent, OnScrollBarPressed);
        _scrollBar = null;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y != 0) SuspendAutoScroll();
    }

    private void OnScrollGesture(object? sender, ScrollGestureEventArgs e)
    {
        if (e.Delta.Y != 0) SuspendAutoScroll();
    }

    private void OnScrollBarPressed(object? sender, PointerPressedEventArgs e) => SuspendAutoScroll();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
            SuspendAutoScroll();
    }

    private void SuspendAutoScroll()
    {
        if (IsAutoScrollEnabled && SuspendAutoScrollCommand is { } command && command.CanExecute(null))
            command.Execute(null);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!IsAutoScrollEnabled || _scrollBar is null || _scrollBar.Maximum - _scrollBar.Value <= 2 ||
            AssociatedObject is not { } grid) return;
        // Resolve the current source, including after a DataContext change.
        var last = grid.ItemsSource is IList list
            ? (list.Count > 0 ? list[list.Count - 1] : null)
            : grid.ItemsSource?.Cast<object>().LastOrDefault();
        if (last is not null) grid.ScrollIntoView(last, null);
    }
}
