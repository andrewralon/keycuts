using System.IO;
using System.Text;
using System.Windows;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;
using DragDrop = keycuts.GUI.DragDrop;

namespace keycuts.Tests
{
    public class CreatorInputTests
    {
        static MemoryStream UrlStream(string url, Encoding encoding)
        {
            return new MemoryStream(encoding.GetBytes(url + "\0"));
        }

        static CreatorViewModel CreateViewModel()
        {
            return new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }));
        }

        [Fact]
        public void Files_WinOverEverything_AndOnlyTheFirstIsUsed()
        {
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { @"C:\a.txt", @"C:\b.txt" });
            data.SetData(DragDrop.UrlFormatUnicode, UrlStream("https://x.test/", Encoding.Unicode));
            data.SetText("some text");

            Assert.True(DragDrop.TryGetDestination(data, out var destination));
            Assert.Equal(@"C:\a.txt", destination);
        }

        [Fact]
        public void BrowserLink_WinsOverText()
        {
            var data = new DataObject();
            data.SetData(DragDrop.UrlFormatUnicode, UrlStream("https://x.test/page", Encoding.Unicode));
            data.SetText("Page title");

            Assert.True(DragDrop.TryGetDestination(data, out var destination));
            Assert.Equal("https://x.test/page", destination);
        }

        [Fact]
        public void AnsiLink_IsUsedWhenUnicodeIsMissing()
        {
            var data = new DataObject();
            data.SetData(DragDrop.UrlFormatAnsi, UrlStream("https://ansi.test/", Encoding.UTF8));

            Assert.True(DragDrop.TryGetDestination(data, out var destination));
            Assert.Equal("https://ansi.test/", destination);
        }

        [Fact]
        public void Text_IsUsedLast()
        {
            var data = new DataObject();
            data.SetText("amazon.com");

            Assert.True(DragDrop.TryGetDestination(data, out var destination));
            Assert.Equal("amazon.com", destination);
        }

        [Fact]
        public void UnsupportedOrBlankData_IsRejected()
        {
            var bitmapOnly = new DataObject();
            bitmapOnly.SetData("SomethingElse", new byte[] { 1, 2, 3 });
            var blank = new DataObject();
            blank.SetText("   ");

            Assert.False(DragDrop.HasSupportedData(bitmapOnly));
            Assert.False(DragDrop.HasSupportedData(blank));
            Assert.False(DragDrop.HasSupportedData(null));
        }

        [Fact]
        public void TryGetFirstFile_OnlyReadsFiles()
        {
            var files = new DataObject();
            files.SetData(DataFormats.FileDrop, new[] { @"C:\one.txt" });
            var text = new DataObject();
            text.SetText(@"C:\one.txt");

            Assert.True(DragDrop.TryGetFirstFile(files, out var path));
            Assert.Equal(@"C:\one.txt", path);
            Assert.False(DragDrop.TryGetFirstFile(text, out _));
        }

        [Fact]
        public void DecodeUrlData_HandlesStreamBytesStringAndUnknown()
        {
            Assert.Equal("https://a.test/", DragDrop.DecodeUrlData(UrlStream("https://a.test/", Encoding.Unicode), Encoding.Unicode));
            Assert.Equal("https://a.test/", DragDrop.DecodeUrlData(Encoding.UTF8.GetBytes("https://a.test/\0junk"), Encoding.UTF8));
            Assert.Equal("https://a.test/", DragDrop.DecodeUrlData("https://a.test/\0", Encoding.UTF8));
            Assert.Null(DragDrop.DecodeUrlData(42, Encoding.UTF8));
        }

        [Fact]
        public void ApplyResolved_Valid_ShowsNormalizedDestinationAndKind()
        {
            var creator = CreateViewModel();
            creator.Destination = "amazon.com";
            creator.DestinationError = DestinationError.NotFound;

            creator.ApplyResolved(DestinationResult.Valid(ShortcutType.Url, "https://amazon.com/"), showError: false);

            Assert.Equal("https://amazon.com/", creator.Destination);
            Assert.Equal(KeycutKind.Url, creator.DestinationKind);
            Assert.Equal(DestinationError.None, creator.DestinationError);
            Assert.Equal(CreatorState.DestinationSelected, creator.State);
        }

        [Fact]
        public void ApplyResolved_Invalid_HidesErrorWhileTyping_AndShowsItOtherwise()
        {
            var creator = CreateViewModel();
            creator.Destination = @"C:\missing";
            var invalid = DestinationResult.Invalid(DestinationError.NotFound, @"C:\missing");

            creator.ApplyResolved(invalid, showError: false);
            Assert.Equal(CreatorState.DestinationSelected, creator.State);

            creator.ApplyResolved(invalid, showError: true);
            Assert.Equal(CreatorState.InvalidDestination, creator.State);
            Assert.Equal(DestinationError.NotFound, creator.DestinationError);
            Assert.Equal(@"C:\missing", creator.Destination);
        }

        [Fact]
        public void ApplyResolved_EmptyError_IsNeverShown()
        {
            var creator = CreateViewModel();

            creator.ApplyResolved(DestinationResult.Invalid(DestinationError.Empty, string.Empty), showError: true);

            Assert.Equal(CreatorState.Empty, creator.State);
        }

        [Fact]
        public void EditingAfterAnError_ClearsIt()
        {
            var creator = CreateViewModel();
            creator.Destination = "x";
            creator.ApplyResolved(DestinationResult.Invalid(DestinationError.NotFound, "x"), showError: true);

            creator.Destination = "xy";

            Assert.Equal(DestinationError.None, creator.DestinationError);
        }

        [Fact]
        public void BrowseCommands_RaiseTheirRequests_AndAreOffWhileSaving()
        {
            var creator = CreateViewModel();
            var files = 0;
            var folders = 0;
            creator.BrowseFileRequested += (s, e) => files++;
            creator.BrowseFolderRequested += (s, e) => folders++;

            creator.BrowseFileCommand.Execute(null);
            creator.BrowseFolderCommand.Execute(null);
            creator.IsSaving = true;
            creator.BrowseFileCommand.Execute(null);

            Assert.Equal(1, files);
            Assert.Equal(1, folders);
            Assert.False(creator.BrowseFolderCommand.CanExecute(null));
        }
    }
}
