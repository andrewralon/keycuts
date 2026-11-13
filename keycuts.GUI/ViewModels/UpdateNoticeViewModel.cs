using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using keycuts.Common;
using keycuts.GUI.Mvvm;

namespace keycuts.GUI.ViewModels
{
    /// <summary>
    /// The quiet line in the footer of both screens when a newer release is out (#171, #205): "keycuts 1.2.0 is available · Download".
    /// Download opens the release page; dismissing hides it until a newer release than that one.
    /// </summary>
    public class UpdateNoticeViewModel : ObservableObject
    {
        public const string AvailableFormat = "keycuts {0} is available";

        private readonly Action<string> openUrl;

        private UpdateCheckResult? notice;

        /// <param name="openUrl">Opens the release page in the browser; tests pass a fake.</param>
        public UpdateNoticeViewModel(UpdateChecker checker, Action<string>? openUrl = null)
        {
            Checker = checker ?? throw new ArgumentNullException(nameof(checker));
            this.openUrl = openUrl ?? OpenInBrowser;

            DownloadCommand = new RelayCommand(Download, () => notice != null);
            DismissCommand = new RelayCommand(Dismiss, () => notice != null);
        }

        /// <summary>Shared with Settings, whose Check now stores what it finds here too.</summary>
        public UpdateChecker Checker { get; }

        public RelayCommand DownloadCommand { get; }

        public RelayCommand DismissCommand { get; }

        public bool IsVisible
        {
            get { return notice != null; }
        }

        public string Message
        {
            get { return notice == null ? string.Empty : FormatAvailable(notice.Latest); }
        }

        /// <summary>What a screen reader calls the Download link: "Download keycuts 1.2.0".</summary>
        public string DownloadName
        {
            get { return notice == null ? string.Empty : "Download " + Runner.AppName + " " + notice.Latest.ToString(3); }
        }

        public static string FormatAvailable(Version latest)
        {
            return string.Format(AvailableFormat, latest.ToString(3));
        }

        /// <summary>
        /// Shows what an earlier check found, then checks GitHub if a day has passed (off the UI thread) and shows that.
        /// Never throws for a network or registry problem; the line just doesn't appear.
        /// </summary>
        public async Task CheckOnStartupAsync(CancellationToken cancellationToken = default)
        {
            Refresh();

            if (await Task.Run(() => Checker.CheckOnStartupAsync(cancellationToken), cancellationToken))
            {
                Refresh();
            }
        }

        /// <summary>Reads the stored state again: after Settings (the toggle, or a Check now) and after a check.</summary>
        public void Refresh()
        {
            SetNotice(Checker.GetNotice());
        }

        private void Download()
        {
            if (notice != null)
            {
                openUrl(notice.ReleaseUrl);
            }
        }

        private void Dismiss()
        {
            if (notice != null)
            {
                Checker.Dismiss(notice.Latest);
                SetNotice(null);
            }
        }

        private void SetNotice(UpdateCheckResult? value)
        {
            if (notice?.Latest == value?.Latest && notice?.ReleaseUrl == value?.ReleaseUrl)
            {
                return;
            }

            notice = value;
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(DownloadName));
            DownloadCommand.RaiseCanExecuteChanged();
            DismissCommand.RaiseCanExecuteChanged();
        }

        /// <summary>Opens the page in the default browser. A browser that won't start is ignored: there's nothing else to offer.</summary>
        public static void OpenInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception e) when (e is Win32Exception || e is InvalidOperationException)
            {
                Debug.WriteLine("keycuts: couldn't open " + url + ": " + e.Message);
            }
        }
    }
}
