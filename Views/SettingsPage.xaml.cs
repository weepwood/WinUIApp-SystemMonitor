using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIApp.Services;

namespace WinUIApp.Views;

public sealed partial class SettingsPage : Page
{
    public event Action<MonitorPreferences>? PreferencesChanged;

    public SettingsPage()
    {
        InitializeComponent();
    }

    public void Initialize(MonitorPreferences preferences)
    {
        ThemeSelector.SelectedIndex = preferences.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };
        IntervalSelector.SelectedIndex = preferences.IntervalSeconds switch { 2 => 1, 5 => 2, _ => 0 };
        PinSwitch.IsOn = preferences.AlwaysOnTop;
    }

    private void OnPinToggled(object sender, RoutedEventArgs args) => ApplyChange();
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs args) => ApplyChange();

    private void ApplyChange()
    {
        if (PreferencesChanged is null || ThemeSelector.SelectedItem is not ComboBoxItem theme ||
            IntervalSelector.SelectedItem is not ComboBoxItem interval) return;
        var preferences = new MonitorPreferences
        {
            Theme = (string)theme.Tag,
            IntervalSeconds = int.Parse((string)interval.Tag),
            AlwaysOnTop = PinSwitch.IsOn
        };
        PreferencesChanged(preferences);
        SaveWarning.IsOpen = !preferences.Save();
    }
}
