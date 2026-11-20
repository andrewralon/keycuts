using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using keycuts.Common;
using keycuts.GUI;
using keycuts.GUI.Mvvm;
using keycuts.GUI.ViewModels;
using Xunit;

namespace keycuts.Tests
{
    /// <summary>
    /// What a drop or the clipboard gives the target (WPF's CreatorInputTests, ported onto <see cref="TargetData"/>): files first,
    /// then a virtual shell item, then a browser link, then text. The shell's parsing-name lookup is faked.
    /// </summary>
    public class TargetDataTests
    {
        const string RecycleBinKey = "::{645FF040-5081-101B-9F08-00AA002F954E}";

        static readonly byte[] RecycleBinCida = ShellIdListTests.Cida(ShellIdListTests.Desktop, ShellIdListTests.RecycleBin);

        static byte[] Url(string url, Encoding encoding)
        {
            return encoding.GetBytes(url + "\0");
        }

        static string? Pick(TargetData.Contents contents, string? shellName = RecycleBinKey)
        {
            return TargetData.Pick(contents, _ => shellName);
        }

        [Fact]
        public void Files_WinOverEverything()
        {
            Assert.Equal(@"C:\a.txt", Pick(new(@"C:\a.txt", RecycleBinCida, Url("https://x.test/", Encoding.Unicode), null, "some text")));
        }

        [Fact]
        public void BrowserLink_WinsOverText_AndAnsiIsUsedWhenUnicodeIsMissing()
        {
            Assert.Equal("https://x.test/page", Pick(new(null, null, Url("https://x.test/page", Encoding.Unicode), null, "Page title")));
            Assert.Equal("https://ansi.test/", Pick(new(null, null, null, Url("https://ansi.test/", Encoding.UTF8), null)));
        }

        [Fact]
        public void Text_IsUsedLast_AndBlankIsRejected()
        {
            Assert.Equal("amazon.com", Pick(new(null, null, null, null, "amazon.com")));
            Assert.Null(Pick(new(null, null, null, null, "   ")));
            Assert.Null(Pick(new(null, null, null, null, null)));
        }

        // #9: a dragged virtual item, as its CLSID key; it loses to files and wins over links and text
        [Fact]
        public void ShellItem_IsItsClsidKey_AfterFiles_BeforeLinks()
        {
            Assert.Equal(RecycleBinKey, Pick(new(null, RecycleBinCida, Url("https://x.test/", Encoding.Unicode), null, "Recycle Bin")));
            Assert.Equal(@"C:\a.txt", Pick(new(@"C:\a.txt", RecycleBinCida, null, null, null)));
        }

        [Theory]
        [InlineData(null)]                                     // the shell can't name it
        [InlineData(@"C:\Users\me\Documents")]                 // a real path
        [InlineData("::{031E4825-7B94-4DC3-B131-E946B44C8DD5}\\Documents.library-ms")] // a library, not a bare key
        [InlineData("::{not-a-guid}")]
        [InlineData("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}\\::{645FF040-5081-101B-9F08-00AA002F954E}")] // nested
        public void ShellItem_ThatIsNotABareClsidKey_FallsBackToTheLinkOrText(string? name)
        {
            Assert.Equal("https://x.test/", Pick(new(null, RecycleBinCida, Url("https://x.test/", Encoding.Unicode), null, null), name));
            Assert.Equal("amazon.com", Pick(new(null, RecycleBinCida, null, null, "amazon.com"), name));
            Assert.Null(Pick(new(null, RecycleBinCida, null, null, null), name));
        }

        [Fact]
        public void ShellItem_WithBadData_FallsBackToText_WithoutAskingTheShell()
        {
            var asked = false;
            var picked = TargetData.Pick(new(null, new byte[] { 1, 0, 0, 0 }, null, null, "amazon.com"), _ =>
            {
                asked = true;
                return RecycleBinKey;
            });

            Assert.Equal("amazon.com", picked);
            Assert.False(asked);
        }

        [Fact]
        public void DecodeUrlData_StopsAtTheFirstNull()
        {
            Assert.Equal("https://a.test/", TargetData.DecodeUrlData(Url("https://a.test/", Encoding.Unicode), Encoding.Unicode));
            Assert.Equal("https://a.test/", TargetData.DecodeUrlData(Encoding.UTF8.GetBytes("https://a.test/\0junk"), Encoding.UTF8));
        }

        // Avalonia's own data transfer, as a drop delivers it
        [AvaloniaFact]
        public void ADrop_IsReadThroughTheDataTransfer()
        {
            var text = new DataTransfer();
            text.Add(DataTransferItem.CreateText("amazon.com"));
            var link = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(TargetData.UrlFormatUnicode, Url("https://x.test/", Encoding.Unicode));
            item.SetText("Page title");
            link.Add(item);

            Assert.True(TargetData.TryGetTarget(text, out var fromText));
            Assert.Equal("amazon.com", fromText);
            Assert.True(TargetData.TryGetTarget(link, out var fromLink));
            Assert.Equal("https://x.test/", fromLink);
            Assert.False(TargetData.HasSupportedData(new DataTransfer()));
            Assert.False(TargetData.HasSupportedData(null));
        }

        #region The view model's half (WPF's CreatorInputTests)

        static CreatorViewModel CreateViewModel()
        {
            return new CreatorViewModel(new RelayCommand(() => { }), new RelayCommand(() => { }));
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

        #endregion
    }
}
