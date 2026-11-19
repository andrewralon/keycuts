using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(HeadlessProbe.TestApp))]

namespace HeadlessProbe;

public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new System.Uri("avares://HeadlessProbe/")) { Source = new System.Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") });
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public class HeadlessTests
{
    [AvaloniaFact]
    public void Typing_and_Enter_reach_the_text_box()
    {
        var box = new TextBox();
        var entered = false;
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) entered = true; };
        var window = new Window { Content = box, CanMaximize = false };
        window.Show();
        box.Focus();

        window.KeyTextInput("gmail");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.Equal("gmail", box.Text);
        Assert.True(entered);
        Assert.False(window.CanMaximize);
    }

    [AvaloniaFact]
    public void Ctrl_or_Cmd_shortcut_arrives_with_its_modifier()
    {
        var window = new Window();
        KeyModifiers seen = default;
        window.KeyDown += (_, e) => seen = e.KeyModifiers;
        window.Show();

        window.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.Meta);

        Assert.Equal(KeyModifiers.Meta, seen);
    }

    [AvaloniaFact]
    public void DataGrid_star_columns_share_the_width_and_fixed_ones_stay()
    {
        var grid = new DataGrid
        {
            ItemsSource = new[] { "a", "b" },
            Columns =
            {
                new DataGridTextColumn { Header = "Name", Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
                new DataGridTextColumn { Header = "Target", Width = new DataGridLength(3, DataGridLengthUnitType.Star) },
                new DataGridTextColumn { Header = "Type", Width = new DataGridLength(80), CanUserResize = false },
            },
        };
        var window = new Window { Width = 480, Height = 300, Content = grid };
        window.Show();
        window.UpdateLayout();

        var name = grid.Columns[0].ActualWidth;
        var target = grid.Columns[1].ActualWidth;
        Assert.Equal(80, grid.Columns[2].ActualWidth);
        Assert.InRange(target / name, 2.9, 3.1);

        window.Width = 780;
        window.UpdateLayout();
        Assert.Equal(80, grid.Columns[2].ActualWidth);
        Assert.True(grid.Columns[1].ActualWidth > target);
    }
}
