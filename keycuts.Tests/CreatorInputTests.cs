#nullable enable
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

            Assert.True(DragDrop.TryGetTarget(data, out var target));
            Assert.Equal(@"C:\a.txt", target);
        }

        [Fact]
        public void BrowserLink_WinsOverText()
        {
            var data = new DataObject();
            data.SetData(DragDrop.UrlFormatUnicode, UrlStream("https://x.test/page", Encoding.Unicode));
            data.SetText("Page title");

            Assert.True(DragDrop.TryGetTarget(data, out var target));
            Assert.Equal("https://x.test/page", target);
        }

        [Fact]
        public void AnsiLink_IsUsedWhenUnicodeIsMissing()
        {
            var data = new DataObject();
            data.SetData(DragDrop.UrlFormatAnsi, UrlStream("https://ansi.test/", Encoding.UTF8));

            Assert.True(DragDrop.TryGetTarget(data, out var target));
            Assert.Equal("https://ansi.test/", target);
        }

        [Fact]
        public void Text_IsUsedLast()
        {
            var data = new DataObject();
            data.SetText("amazon.com");

            Assert.True(DragDrop.TryGetTarget(data, out var target));
            Assert.Equal("amazon.com", target);
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

        // #9: a dragged virtual item. The parsing-name lookup is faked, so these don't read this machine's shell namespace.
        const string RecycleBinKey = "::{645FF040-5081-101B-9F08-00AA002F954E}";

        static readonly byte[] RecycleBinCida = ShellIdListTests.Cida(ShellIdListTests.Desktop, ShellIdListTests.RecycleBin);

        static string? NameAs(string? name, byte[] _)
        {
            return name;
        }

        [Fact]
        public void ShellItem_IsReadFromAStreamOrBytes_AsItsClsidKey()
        {
            var stream = new DataObject();
            stream.SetData(ShellIdList.Format, new MemoryStream(RecycleBinCida));
            var bytes = new DataObject();
            bytes.SetData(ShellIdList.Format, RecycleBinCida);

            Assert.True(DragDrop.TryGetTarget(stream, out var fromStream, id => NameAs(RecycleBinKey, id)));
            Assert.Equal(RecycleBinKey, fromStream);
            Assert.True(DragDrop.TryGetTarget(bytes, out var fromBytes, id => NameAs(RecycleBinKey, id)));
            Assert.Equal(RecycleBinKey, fromBytes);
        }

        [Fact]
        public void ShellItem_LosesToFiles_AndWinsOverLinksAndText()
        {
            var files = new DataObject();
            files.SetData(DataFormats.FileDrop, new[] { @"C:\a.txt" });
            files.SetData(ShellIdList.Format, RecycleBinCida);
            var links = new DataObject();
            links.SetData(ShellIdList.Format, RecycleBinCida);
            links.SetData(DragDrop.UrlFormatUnicode, UrlStream("https://x.test/", Encoding.Unicode));
            links.SetText("Recycle Bin");

            Assert.True(DragDrop.TryGetTarget(files, out var file, id => NameAs(RecycleBinKey, id)));
            Assert.Equal(@"C:\a.txt", file);
            Assert.True(DragDrop.TryGetTarget(links, out var item, id => NameAs(RecycleBinKey, id)));
            Assert.Equal(RecycleBinKey, item);
        }

        [Theory]
        [InlineData(null)]                                     // the shell can't name it
        [InlineData(@"C:\Users\me\Documents")]                 // a real path
        [InlineData("::{031E4825-7B94-4DC3-B131-E946B44C8DD5}\\Documents.library-ms")] // a library, not a bare key
        [InlineData("::{not-a-guid}")]
        [InlineData("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}\\::{645FF040-5081-101B-9F08-00AA002F954E}")] // nested
        public void ShellItem_ThatIsNotABareClsidKey_FallsBackToTheLinkOrText(string? name)
        {
            var link = new DataObject();
            link.SetData(ShellIdList.Format, RecycleBinCida);
            link.SetData(DragDrop.UrlFormatUnicode, UrlStream("https://x.test/", Encoding.Unicode));
            var text = new DataObject();
            text.SetData(ShellIdList.Format, RecycleBinCida);
            text.SetText("amazon.com");
            var alone = new DataObject();
            alone.SetData(ShellIdList.Format, RecycleBinCida);

            Assert.True(DragDrop.TryGetTarget(link, out var fromLink, id => NameAs(name, id)));
            Assert.Equal("https://x.test/", fromLink);
            Assert.True(DragDrop.TryGetTarget(text, out var fromText, id => NameAs(name, id)));
            Assert.Equal("amazon.com", fromText);
            Assert.False(DragDrop.TryGetTarget(alone, out _, id => NameAs(name, id)));
        }

        [Fact]
        public void ShellItem_WithBadOrUnsupportedData_FallsBackToText_WithoutAskingTheShell()
        {
            var asked = false;
            string? Lookup(byte[] id)
            {
                asked = true;
                return RecycleBinKey;
            }

            var malformed = new DataObject();
            malformed.SetData(ShellIdList.Format, new byte[] { 1, 0, 0, 0 });
            malformed.SetText("amazon.com");
            var unsupported = new DataObject();
            unsupported.SetData(ShellIdList.Format, "not bytes");
            unsupported.SetText("amazon.com");

            Assert.True(DragDrop.TryGetTarget(malformed, out var fromMalformed, Lookup));
            Assert.Equal("amazon.com", fromMalformed);
            Assert.True(DragDrop.TryGetTarget(unsupported, out var fromUnsupported, Lookup));
            Assert.Equal("amazon.com", fromUnsupported);
            Assert.False(asked);
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
        public void ApplyResolved_Valid_ShowsNormalizedTargetAndKind()
        {
            var creator = CreateViewModel();
            creator.Target = "amazon.com";
            creator.TargetError = TargetError.NotFound;

            creator.ApplyResolved(TargetResult.Valid(ShortcutType.Url, "https://amazon.com/"), showError: false);

            Assert.Equal("https://amazon.com/", creator.Target);
            Assert.Equal(KeycutKind.Url, creator.TargetKind);
            Assert.Equal(TargetError.None, creator.TargetError);
            Assert.Equal(CreatorState.TargetSelected, creator.State);
        }

        [Fact]
        public void ApplyResolved_Invalid_HidesErrorWhileTyping_AndShowsItOtherwise()
        {
            var creator = CreateViewModel();
            creator.Target = @"C:\missing";
            var invalid = TargetResult.Invalid(TargetError.NotFound, @"C:\missing");

            creator.ApplyResolved(invalid, showError: false);
            Assert.Equal(CreatorState.TargetSelected, creator.State);

            creator.ApplyResolved(invalid, showError: true);
            Assert.Equal(CreatorState.InvalidTarget, creator.State);
            Assert.Equal(TargetError.NotFound, creator.TargetError);
            Assert.Equal(@"C:\missing", creator.Target);
        }

        [Fact]
        public void ApplyResolved_EmptyError_IsNeverShown()
        {
            var creator = CreateViewModel();

            creator.ApplyResolved(TargetResult.Invalid(TargetError.Empty, string.Empty), showError: true);

            Assert.Equal(CreatorState.Empty, creator.State);
        }

        [Fact]
        public void EditingAfterAnError_ClearsIt()
        {
            var creator = CreateViewModel();
            creator.Target = "x";
            creator.ApplyResolved(TargetResult.Invalid(TargetError.NotFound, "x"), showError: true);

            creator.Target = "xy";

            Assert.Equal(TargetError.None, creator.TargetError);
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
