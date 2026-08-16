using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Alex80_IDE.Converters;

namespace Alex80_IDE.Controls;

/// <summary>
/// Dump esadecimale della memoria. Le colonne dei byte sono generate una volta sola:
/// <see cref="ByteColumns"/> decide quante restano visibili, così le tre viste
/// (16 o 8 byte per riga) condividono la stessa griglia e le stesse larghezze.
/// </summary>
public partial class MemoryView : UserControl
{
    private const int MaxByteColumns = 16;
    private const double ByteColumnWidth = 52;

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MemoryView, string>(nameof(Title), "Memoria");

    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<MemoryView, string?>(nameof(Caption));

    public static readonly StyledProperty<int> ByteColumnsProperty =
        AvaloniaProperty.Register<MemoryView, int>(nameof(ByteColumns), MaxByteColumns);

    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<MemoryView, bool>(nameof(ShowToolbar));

    private readonly List<DataGridColumn> _byteColumns = new();

    public MemoryView()
    {
        InitializeComponent();
        BuildColumns();
        ApplyByteColumns();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Testo secondario a destra del titolo. Nascosto se vuoto.</summary>
    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>Byte per riga effettivamente mostrati (1..16).</summary>
    public int ByteColumns
    {
        get => GetValue(ByteColumnsProperty);
        set => SetValue(ByteColumnsProperty, value);
    }

    public bool ShowToolbar
    {
        get => GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ByteColumnsProperty)
        {
            ApplyByteColumns();
        }
    }

    private void BuildColumns()
    {
        MemoryGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "ADDR",
            Width = new DataGridLength(90),
            Binding = new Binding(nameof(Models.RowElement.Address))
            {
                Mode = BindingMode.OneWay,
                Converter = new HexadecimalConverter()
            }
        });

        var byteConverter = new ByteToHexConverter();

        for (var i = 0; i < MaxByteColumns; i++)
        {
            var column = new DataGridTextColumn
            {
                Header = i.ToString("X2"),
                Width = new DataGridLength(ByteColumnWidth),
                Binding = new Binding($"{nameof(Models.RowElement.DataArray)}[{i}]")
                {
                    Mode = BindingMode.OneWay,
                    Converter = byteConverter
                }
            };

            _byteColumns.Add(column);
            MemoryGrid.Columns.Add(column);
        }

        MemoryGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "ASCII",
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            Binding = new Binding(nameof(Models.RowElement.Ascii)) { Mode = BindingMode.OneWay }
        });
    }

    private void ApplyByteColumns()
    {
        var visible = ByteColumns;

        for (var i = 0; i < _byteColumns.Count; i++)
        {
            _byteColumns[i].IsVisible = i < visible;
        }
    }
}
