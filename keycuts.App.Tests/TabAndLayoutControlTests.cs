using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using keycuts.GUI.Controls;
using keycuts.GUI.Themes;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// The custom controls of Phase 3.3b: the folder tabs (FolderTabShape, RailTabControl and their themes), TornCorner, StarCell,
    /// MenuButton and MenuPlacement. WPF's TornCornerTests, MenuPlacementTests and TabbedWindowTests' tab shape tests, ported.
    /// </summary>
    public class TabAndLayoutControlTests
    {
        #region Helpers

        static Window Host(Control content, double width = 480, double height = 320)
        {
            ThemeManager.Apply(Palette.Light);
            var window = new Window { Width = width, Height = height, Content = content };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            return window;
        }

        static ControlTheme Theme(string key)
        {
            var app = Application.Current!;
            Assert.True(app.TryGetResource(key, app.ActualThemeVariant, out var value), $"No resource {key}");
            return Assert.IsType<ControlTheme>(value);
        }

        static Color PaletteColor(string key)
        {
            var app = Application.Current!;
            Assert.True(app.TryGetResource(key, app.ActualThemeVariant, out var value), $"No resource {key}");
            return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
        }

        static T Part<T>(Visual root, string name) where T : Control
        {
            return root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
        }

        #endregion

        #region The tab shape

        [Fact]
        public void FolderTabShape_HasASquareLeftSide_ACutCorner_AFlatTop_AndASlantedRightSide()
        {
            Assert.Equal(
                new[] { new Point(0, 40), new Point(0, 8), new Point(8, 0), new Point(84, 0), new Point(100, 40) },
                FolderTabShape.GetPoints(100, 40, 8, 16, 0));
        }

        [Fact]
        public void FolderTabShape_DrawsItsStrokeInsideItsBounds()
        {
            var points = FolderTabShape.GetPoints(100, 40, 8, 16, 1);

            Assert.Equal(0.5, points[0].X);
            Assert.Equal(0.5, points[2].Y);
            Assert.Equal(99.5, points[4].X);
            Assert.Equal(40, points[0].Y);
            Assert.Equal(40, points[4].Y);
        }

        [Fact]
        public void FolderTabShape_SurvivesATabNarrowerThanItsSlant()
        {
            var points = FolderTabShape.GetPoints(10, 40, 8, 16, 1);

            Assert.All(points, p => Assert.True(p.X >= 0 && p.X <= 10 && p.Y >= 0 && p.Y <= 40, p.ToString()));
            Assert.True(points[3].X >= points[2].X, "the top never runs backwards");
        }

        [AvaloniaFact]
        public void AFolderTabShape_FillsWhatItIsGiven_InItsOwnCoordinates()
        {
            var shape = new FolderTabShape { Width = 100, Height = 40, Fill = Brushes.Black, Stroke = Brushes.Black, StrokeThickness = 1, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Host(shape);
            try
            {
                Assert.Equal(new Size(100, 40), shape.Bounds.Size);
                var bounds = shape.RenderedGeometry!.Bounds;
                Assert.Equal(0.5, bounds.Left, 3);
                Assert.Equal(0.5, bounds.Top, 3);
                Assert.Equal(99.5, bounds.Right, 3);
                Assert.Equal(40, bounds.Bottom, 3);
                // The cut corner is outside the tab, the middle inside
                Assert.False(shape.RenderedGeometry.FillContains(new Point(1.5, 1.5)));
                Assert.True(shape.RenderedGeometry.FillContains(new Point(50, 20)));
            }
            finally
            {
                window.Close();
            }
        }

        #endregion

        #region The tab list

        static (RailTabControl Rail, TabItem[] Tabs) RailWithTabs()
        {
            var tabs = new[] { "creator", "manager", "settings" }.Select(name => new TabItem { Header = name, Name = name }).ToArray();
            var rail = new RailTabControl { Theme = Theme("RailTabControl") };
            foreach (var tab in tabs)
            {
                rail.Items.Add(tab);
            }

            rail.SelectedIndex = 0;
            return (rail, tabs);
        }

        [AvaloniaFact]
        public void TheSelectedTab_IsThePageColor_CastsItsShadowRight_AndStandsInFront()
        {
            var (rail, tabs) = RailWithTabs();
            var window = Host(rail);
            try
            {
                Assert.All(tabs, tab => Assert.Equal(40, tab.Bounds.Height));
                Assert.True(tabs[1].Bounds.Left > tabs[0].Bounds.Right, "the tabs stand side by side");
                Assert.Equal(4, tabs[1].Bounds.Left - tabs[0].Bounds.Right, 3);

                var selected = tabs[0];
                Assert.Equal(PaletteColor("SurfaceBrush"), Assert.IsAssignableFrom<ISolidColorBrush>(Part<FolderTabShape>(selected, "Body").Fill).Color);
                Assert.True(Part<FolderTabShape>(selected, "Shadow").IsVisible);
                Assert.False(Part<FolderTabShape>(selected, "Texture").IsVisible);
                Assert.Equal(5, selected.ZIndex);
                Assert.Equal(PaletteColor("TextBrush"), Assert.IsAssignableFrom<ISolidColorBrush>(selected.Foreground).Color);

                // Direction 0: to the right only
                var shadow = Assert.IsType<DropShadowDirectionEffect>(Part<FolderTabShape>(selected, "Shadow").Effect);
                Assert.Equal(3, shadow.OffsetX, 3);
                Assert.Equal(0, shadow.OffsetY, 3);

                var inactive = tabs[1];
                Assert.False(Part<FolderTabShape>(inactive, "Shadow").IsVisible);
                Assert.True(Part<FolderTabShape>(inactive, "Texture").IsVisible);
                Assert.Equal(PaletteColor("TabTextBrush"), Assert.IsAssignableFrom<ISolidColorBrush>(inactive.Foreground).Color);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void TheArrowKeys_MoveAlongTheTabs_AndWrap_ButCtrlTabDoesNot()
        {
            var (rail, tabs) = RailWithTabs();
            var window = Host(rail);
            try
            {
                tabs[0].Focus(NavigationMethod.Tab);
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, rail.SelectedIndex);

                window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
                window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, rail.SelectedIndex);

                window.KeyPress(Key.Tab, RawInputModifiers.Control, PhysicalKey.Tab, null);
                window.KeyPress(Key.PageDown, RawInputModifiers.Control, PhysicalKey.PageDown, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, rail.SelectedIndex);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void HoldsKeyboardFocus_OnlyAfterAKeyOnATab_NotAfterAClick()
        {
            var (rail, tabs) = RailWithTabs();
            var window = Host(rail);
            try
            {
                Assert.False(RailTabControl.HoldsKeyboardFocus(null));

                tabs[0].Focus(NavigationMethod.Tab);
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(RailTabControl.HoldsKeyboardFocus(window.FocusManager!.GetFocusedElement()));

                var point = tabs[0].TranslatePoint(new Point(20, 20), window)!.Value;
                window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.False(RailTabControl.HoldsKeyboardFocus(tabs[0]));
            }
            finally
            {
                window.Close();
            }
        }

        [Theory]
        [InlineData(Key.Tab, KeyModifiers.Control, true)]
        [InlineData(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift, true)]
        [InlineData(Key.PageUp, KeyModifiers.Control, true)]
        [InlineData(Key.PageDown, KeyModifiers.Control, true)]
        [InlineData(Key.Tab, KeyModifiers.None, false)]
        [InlineData(Key.Right, KeyModifiers.Control, false)]
        public void IsTabCycleKey_IsCtrlTabAndCtrlPage(Key key, KeyModifiers modifiers, bool expected)
        {
            Assert.Equal(expected, RailTabControl.IsTabCycleKey(key, modifiers));
        }

        #endregion

        #region The torn corner

        [Fact]
        public void TheRip_RunsFromTheBottomEdgeToTheRightEdge()
        {
            var edge = TornCornerShape.Default.Edge;

            Assert.Equal(new Point(-TornCornerShape.TearWidth, 0), edge.First());
            Assert.Equal(new Point(0, -TornCornerShape.TearHeight), edge.Last());
        }

        [Fact]
        public void TheRip_StaysInsideTheWindowAndNearTheCorner()
        {
            // The jitter may push a point a little past the tear's box inwards, never out of the window
            foreach (var point in TornCornerShape.Default.Edge)
            {
                Assert.InRange(point.X, -TornCornerShape.TearWidth - 10, 0);
                Assert.InRange(point.Y, -TornCornerShape.TearHeight - 10, 0);
            }
        }

        [Fact]
        public void TheRip_IsTheSameEveryTime()
        {
            // Seeded, like the mockup: every window tears the same way
            var shape = TornCornerShape.Default;
            Assert.Equal(27, shape.Edge.Count);
            Assert.Equal(18, shape.Threads.Count);
            Assert.All(shape.Threads, thread => Assert.InRange(thread.Opacity, .6, 1));
        }

        [AvaloniaFact]
        public void TheWindowClip_LeavesOutTheCornerAndKeepsTheRest()
        {
            var clip = TornCornerShape.Default.GetWindowClip(new Size(600, 300));

            Assert.False(clip.FillContains(new Point(599, 299)));
            Assert.False(clip.FillContains(new Point(590, 290)));
            Assert.True(clip.FillContains(new Point(1, 1)));
            Assert.True(clip.FillContains(new Point(599, 1)));
            Assert.True(clip.FillContains(new Point(1, 299)));
            // Well above the rip on the right edge, and well left of it on the bottom
            Assert.True(clip.FillContains(new Point(599, 300 - TornCornerShape.TearHeight - 12)));
            Assert.True(clip.FillContains(new Point(600 - TornCornerShape.TearWidth - 12, 299)));
        }

        [AvaloniaFact]
        public void TheTornCorner_TakesThePointerOnlyAlongTheRip_AndOnlyAsAResizeGrip()
        {
            var corner = new TornCorner { Stroke = Brushes.Gray };
            var window = Host(new Panel { Children = { corner } });
            try
            {
                Assert.Equal(new Size(TornCornerShape.TearWidth + TornCorner.Room, TornCornerShape.TearHeight + TornCorner.Room), corner.Bounds.Size);
                var onTheRip = TornCornerShape.Offset(TornCornerShape.Default.Edge[13], new Point(corner.Bounds.Width, corner.Bounds.Height));

                Assert.False(corner.HitTest(onTheRip));
                Assert.Null(corner.Cursor);

                corner.IsResizeGrip = true;
                Assert.True(corner.HitTest(onTheRip));
                Assert.False(corner.HitTest(new Point(2, 2)));
                Assert.NotNull(corner.Cursor);
            }
            finally
            {
                window.Close();
            }
        }

        #endregion

        #region StarCell

        [AvaloniaFact]
        public void StarCells_ShareTheRowByRatio_WhateverTheTextBoxesHold()
        {
            var target = new TextBox { Text = "C:" };
            var name = new TextBox { Text = "npp" };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,12,1.25*"), Width = 401 };
            grid.Children.Add(new StarCell { Child = target });
            var nameCell = new StarCell { Child = name };
            Grid.SetColumn(nameCell, 2);
            grid.Children.Add(nameCell);
            var window = Host(grid, 600);
            try
            {
                var shortTarget = target.Bounds;
                var shortName = name.Bounds;

                target.Text = @"C:\Program Files\Notepad++\a very long folder name that runs well past the end of the box\notepad++.exe";
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(shortTarget, target.Bounds);
                Assert.Equal(shortName, name.Bounds);
                Assert.Equal(2 / 3.25 * (401 - 12), target.Bounds.Width, 0);
            }
            finally
            {
                window.Close();
            }
        }

        #endregion

        #region MenuButton and MenuPlacement

        [AvaloniaFact]
        public void AMenuButton_IsAnExpandCollapseControl_ThatOpensItsMenu()
        {
            var button = new MenuButton { Content = "Browse", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, ContextMenu = new ContextMenu { Items = { new MenuItem { Header = "File…" } } } };
            var window = Host(button);
            try
            {
                var peer = ControlAutomationPeer.CreatePeerForElement(button);
                var provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer);
                Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);
                Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());

                provider.Expand();
                Dispatcher.UIThread.RunJobs();
                Assert.True(button.IsMenuOpen);
                Assert.Equal(ExpandCollapseState.Expanded, provider.ExpandCollapseState);

                provider.Collapse();
                Dispatcher.UIThread.RunJobs();
                Assert.False(button.IsMenuOpen);

                // It looks like any button
                Assert.Equal(36, button.Bounds.Height);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void UnderRightEdge_OpensTheMenuUnderTheButton_TheirRightEdgesLinedUp()
        {
            var menu = new ContextMenu { Items = { new MenuItem { Header = "Folder…" }, new MenuItem { Header = "File…" } } };
            var button = new Button { Content = "B", Width = 36, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, ContextMenu = menu };
            MenuPlacement.UnderRightEdge(menu, 6);
            var window = Host(button);
            try
            {
                menu.Open(button);
                Dispatcher.UIThread.RunJobs();

                var buttonRight = button.PointToScreen(new Point(button.Bounds.Width, button.Bounds.Height));
                var menuRight = menu.PointToScreen(new Point(menu.Bounds.Width, 0));
                Assert.InRange(menuRight.X, buttonRight.X - 1, buttonRight.X + 1);
                Assert.InRange(menuRight.Y, buttonRight.Y + 5, buttonRight.Y + 7);
                Assert.True(menu.Bounds.Width > button.Bounds.Width);
            }
            finally
            {
                menu.Close();
                window.Close();
            }
        }

        #endregion
    }
}
