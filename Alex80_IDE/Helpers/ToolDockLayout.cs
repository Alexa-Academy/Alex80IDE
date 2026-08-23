using System;
using System.ComponentModel;
using Alex80_IDE.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace Alex80_IDE.Helpers;

/// <summary>
/// Tiene allineata la riga o la colonna che ospita un dock con lo stato del relativo
/// <see cref="ToolDockViewModel"/>: chiuso occupa zero pixel, aperto riprende la misura
/// memorizzata, e il trascinamento del GridSplitter torna nel view model.
/// <para>
/// Il collegamento non passa da un binding perche' le definizioni di riga e colonna non
/// fanno parte dell'albero logico e non ereditano quindi il DataContext.
/// </para>
/// </summary>
public sealed class ToolDockLayout : IDisposable
{
    /// <summary>Sotto questa misura il pannello non e' piu' utilizzabile: il splitter si ferma qui.</summary>
    private const double MinOpenSize = 140;

    private readonly ToolDockViewModel _dock;
    private readonly AvaloniaObject _definition;
    private readonly AvaloniaProperty _lengthProperty;
    private readonly Action<double> _writeMinSize;
    private bool _applying;
    private bool _capturing;

    private ToolDockLayout(
        ToolDockViewModel dock,
        AvaloniaObject definition,
        AvaloniaProperty lengthProperty,
        Action<double> writeMinSize)
    {
        _dock = dock;
        _definition = definition;
        _lengthProperty = lengthProperty;
        _writeMinSize = writeMinSize;

        _definition.PropertyChanged += OnDefinitionPropertyChanged;
        _dock.PropertyChanged += OnDockPropertyChanged;

        Apply();
    }

    public static ToolDockLayout ForColumn(ToolDockViewModel dock, ColumnDefinition column) =>
        new(dock, column, ColumnDefinition.WidthProperty, value => column.MinWidth = value);

    public static ToolDockLayout ForRow(ToolDockViewModel dock, RowDefinition row) =>
        new(dock, row, RowDefinition.HeightProperty, value => row.MinHeight = value);

    public void Dispose()
    {
        _definition.PropertyChanged -= OnDefinitionPropertyChanged;
        _dock.PropertyChanged -= OnDockPropertyChanged;
    }

    private void OnDockPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_capturing)
        {
            return;
        }

        if (e.PropertyName is nameof(ToolDockViewModel.IsPanelVisible) or nameof(ToolDockViewModel.PanelSize))
        {
            Apply();
        }
    }

    private void OnDefinitionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == _lengthProperty)
        {
            CaptureSize();
        }
    }

    /// <summary>Porta nella griglia la misura che lo stato corrente del dock richiede.</summary>
    private void Apply()
    {
        var size = _dock.IsPanelVisible ? Math.Max(_dock.PanelSize, MinOpenSize) : 0;

        _applying = true;
        try
        {
            // Il minimo va tolto prima di chiudere, altrimenti la riga resterebbe aperta.
            _writeMinSize(_dock.IsPanelVisible ? MinOpenSize : 0);
            _definition.SetValue(_lengthProperty, new GridLength(size, GridUnitType.Pixel));
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>Registra nel dock la misura scelta trascinando il GridSplitter.</summary>
    private void CaptureSize()
    {
        if (_applying || !_dock.IsPanelVisible)
        {
            return;
        }

        if (_definition.GetValue(_lengthProperty) is not GridLength length ||
            !length.IsAbsolute ||
            length.Value < MinOpenSize)
        {
            return;
        }

        // Il valore torna nel view model, ma non deve rimbalzare nella griglia: la misura
        // giusta e' gia' quella che il GridSplitter ha appena scritto.
        _capturing = true;
        try
        {
            _dock.PanelSize = length.Value;
        }
        finally
        {
            _capturing = false;
        }
    }
}
