using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using keycuts.GUI.Themes;

namespace keycuts.GUI.Views
{
    /// <summary>
    /// An icon choice as a picture, 52 (WPF's IconPreview template in SettingsView). One that follows the theme shows both drawings:
    /// the light-mode one at the top left, and the dark-mode one over it, smaller, at the bottom right, in a ring of whatever it
    /// sits on. While the icon is set to transparent, it shows the drawings the taskbar and the shortcuts will.
    /// </summary>
    public sealed class IconPreview : Panel
    {
        public static readonly StyledProperty<AppIconOption?> OptionProperty =
            AvaloniaProperty.Register<IconPreview, AppIconOption?>(nameof(Option));

        private readonly Image single = new();

        private readonly Image back = new() { Width = 38, Height = 38, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };

        private readonly Image frontImage = new();

        private readonly Border front;

        public IconPreview()
        {
            Width = 52;
            Height = 52;
            RenderOptions.SetBitmapInterpolationMode(this, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
            front = new Border
            {
                Width = 33,
                Height = 33,
                Padding = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = frontImage
            };
            Children.Add(single);
            Children.Add(back);
            Children.Add(front);
        }

        public AppIconOption? Option
        {
            get => GetValue(OptionProperty);
            set => SetValue(OptionProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == OptionProperty)
            {
                Update();
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            AppIconManager.Changed += AppIconManager_Changed;
            // The ring is the color of what the preview sits on: the card or the button
            if (this.FindAncestorOfType<Border>() is { } host)
            {
                front.Bind(Border.BackgroundProperty, host.GetObservable(Border.BackgroundProperty));
            }

            if (this.FindResource("RadiusMd") is CornerRadius radius)
            {
                front.CornerRadius = radius;
            }

            Update();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            AppIconManager.Changed -= AppIconManager_Changed;
            base.OnDetachedFromVisualTree(e);
        }

        private void AppIconManager_Changed(object? sender, EventArgs e)
        {
            Update();
        }

        private void Update()
        {
            var option = Option;
            if (option == null)
            {
                return;
            }

            var transparent = AppIconManager.Transparent;
            var light = AppIcons.GetPreview(transparent ? AppIcons.GetTransparent(option.LightArt) : option.LightArt);
            var dark = AppIcons.GetPreview(transparent ? AppIcons.GetTransparent(option.DarkArt) : option.DarkArt);
            single.Source = light;
            back.Source = light;
            frontImage.Source = dark;
            single.IsVisible = !option.FollowsTheme;
            back.IsVisible = option.FollowsTheme;
            front.IsVisible = option.FollowsTheme;
        }
    }
}
