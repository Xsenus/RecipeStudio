using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace RecipeStudio.Desktop.Views.Dialogs;

public sealed partial class ColorPickerDialog : Window
{
    private bool _isUpdating;

    public ColorPickerDialog(string initialHex)
    {
        InitializeComponent();

        if (!TryParseHex(initialHex, out var color))
            color = Color.FromRgb(34, 197, 94);

        SetFromColor(color);

        HexBox.LostFocus += (_, _) => ApplyHexFromText();
        RSlider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) UpdateFromSliders(); };
        GSlider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) UpdateFromSliders(); };
        BSlider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) UpdateFromSliders(); };
    }

    public string SelectedHex { get; private set; } = "#22C55E";

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        ApplyHexFromText();
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
        => Close(false);

    private void SetFromColor(Color color)
    {
        _isUpdating = true;
        try
        {
            RSlider.Value = color.R;
            GSlider.Value = color.G;
            BSlider.Value = color.B;
            SelectedHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            HexBox.Text = SelectedHex;
            PreviewBorder.Background = new SolidColorBrush(color);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateFromSliders()
    {
        if (_isUpdating) return;

        var color = Color.FromRgb((byte)RSlider.Value, (byte)GSlider.Value, (byte)BSlider.Value);
        SelectedHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        HexBox.Text = SelectedHex;
        PreviewBorder.Background = new SolidColorBrush(color);
    }

    private void ApplyHexFromText()
    {
        var text = HexBox.Text?.Trim() ?? string.Empty;
        if (!TryParseHex(text, out var color))
            return;

        SetFromColor(color);
    }

    private static bool TryParseHex(string value, out Color color)
    {
        if (!value.StartsWith("#"))
            value = "#" + value;

        if (Color.TryParse(value, out color))
        {
            color = Color.FromRgb(color.R, color.G, color.B);
            return true;
        }

        color = default;
        return false;
    }
}
