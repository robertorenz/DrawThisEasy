using System;
using System.Threading;
using System.Windows;
using DrawThisEasy.Services;

namespace DrawThisEasy.Dialogs;

/// Shows a found update, downloads it with progress, and hands the file back to the caller.
public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _info;
    private CancellationTokenSource? _cts;
    private string? _downloaded;

    private UpdateWindow(UpdateInfo info)
    {
        InitializeComponent();
        _info = info;

        TitleText.Text = L10n.T("update.title");
        SummaryText.Text = string.Format(L10n.T("update.summary"), info.Version.ToString(3), Updater.CurrentVersion.ToString(3))
            + "\n" + L10n.T(info.IsInstaller ? "update.mode.installer" : "update.mode.portable");
        LblNotes.Text = L10n.T("update.notes").ToUpperInvariant();
        NotesBox.Text = string.IsNullOrWhiteSpace(info.Notes) ? L10n.T("update.notes.none") : info.Notes.Trim();
        BtnSkip.Content = L10n.T("update.skip");
        BtnLater.Content = L10n.T("update.later");
        BtnInstall.Content = L10n.T("update.install");
    }

    /// Returns the downloaded file path if the user chose to install, otherwise null.
    public static string? Show(Window owner, UpdateInfo info)
    {
        var w = new UpdateWindow(info) { Owner = owner };
        w.ShowDialog();
        return w._downloaded;
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        var s = AppSettings.Current;
        s.SkippedUpdateVersion = _info.Version.ToString(3);
        s.Save();
        Close();
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        BtnInstall.IsEnabled = BtnSkip.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = L10n.T("update.downloading");
        _cts = new CancellationTokenSource();
        var progress = new Progress<double>(p =>
        {
            Progress.Value = p;
            ProgressText.Text = $"{L10n.T("update.downloading")} {p:P0}";
        });
        try
        {
            _downloaded = await Updater.DownloadAsync(_info, progress, _cts.Token);
            Close();
        }
        catch (OperationCanceledException) { /* closed via Later / ✕ */ }
        catch (Exception ex)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            ErrorText.Text = string.Format(L10n.T("update.error.download"), ex.Message);
            ErrorText.Visibility = Visibility.Visible;
            BtnInstall.IsEnabled = BtnSkip.IsEnabled = true;
        }
    }
}
