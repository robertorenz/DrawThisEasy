using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DrawThisEasy.Services;
using DrawThisEasy.Services.Ai;
using Microsoft.Win32;

namespace DrawThisEasy.Dialogs;

/// Engine selection and credentials: Claude Code CLI location (subscription) and the
/// Anthropic API key (stored DPAPI-encrypted, see AiSettings).
public partial class AiSettingsWindow : Window
{
    private string _provider;
    private bool _testing;

    public AiSettingsWindow(string? provider = null)
    {
        InitializeComponent();
        var s = AiSettings.Current;
        _provider = AiProviders.Find(provider ?? s.Provider).Id;

        TitleText.Text     = L10n.T("ai.settings.title");
        LblProvider.Text   = L10n.T("ai.settings.default").ToUpperInvariant();
        LblCodeHeader.Text = L10n.T("ai.provider.code").ToUpperInvariant();
        LblCliPath.Text    = L10n.T("ai.settings.clipath");
        BtnBrowse.Content  = L10n.T("ai.settings.browse");
        LblApiHeader.Text  = L10n.T("ai.provider.api").ToUpperInvariant();
        LblApiKey.Text     = L10n.T("ai.settings.apikey");
        BtnTest.Content    = L10n.T("ai.settings.test");
        BtnCancel.Content  = L10n.T("modal.cancel");
        BtnSave.Content    = L10n.T("topbar.save");

        foreach (var p in AiProviders.All)
        {
            var rb = new RadioButton
            {
                GroupName = "AiProvider", Tag = p.Id, IsChecked = p.Id == _provider, Margin = new Thickness(0, 0, 0, 8),
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = p.Name, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") },
                        new TextBlock { Text = p.Description, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, MaxWidth = 440,
                                        Foreground = (Brush)FindResource("TextMutedBrush") },
                    }
                }
            };
            rb.Checked += (_, _) => _provider = (string)rb.Tag;
            ProviderList.Children.Add(rb);
        }

        TxtCliPath.Text = s.ClaudeCliPath;
        TxtApiKey.Password = s.ApiKey;
        bool envKey = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
        ApiKeyNote.Text = L10n.T(envKey ? "ai.settings.apikey.env" : "ai.settings.apikey.note");
        UpdateCliStatus();
    }

    /// Shows the dialog; returns true if the user saved.
    public static bool Show(Window owner, string? provider = null) =>
        new AiSettingsWindow(provider) { Owner = owner }.ShowDialog() == true;

    private void TxtCliPath_TextChanged(object sender, TextChangedEventArgs e) => UpdateCliStatus();

    private void UpdateCliStatus()
    {
        var found = ClaudeCodeProvider.ResolveCli(TxtCliPath.Text.Trim());
        CliStatus.Text = found != null ? string.Format(L10n.T("ai.settings.cli.found"), found) : L10n.T("ai.settings.cli.missing");
        CliStatus.Foreground = (Brush)FindResource(found != null ? "TextMutedBrush" : "DangerBrush");
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Claude Code (claude.exe;claude.cmd)|claude.exe;claude.cmd|*.*|*.*" };
        if (dlg.ShowDialog(this) == true) TxtCliPath.Text = dlg.FileName;
    }

    private AiSettings Collect()
    {
        var cur = AiSettings.Current;
        var s = new AiSettings
        {
            Provider = _provider,
            Models = new(cur.Models),
            ClaudeCliPath = TxtCliPath.Text.Trim(),
        };
        s.ApiKey = TxtApiKey.Password;
        return s;
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (_testing) return;
        _testing = true;
        BtnTest.IsEnabled = false;
        var info = AiProviders.Find(_provider);
        ShowTest(string.Format(L10n.T("ai.settings.testing"), info.Name), ok: null);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var reply = await info.Create(Collect()).CompleteAsync(
                "You are a connectivity check. Reply with the single word OK.", "Ping", null, cts.Token);
            ShowTest(string.Format(L10n.T("ai.settings.test.ok"), info.Name, reply.Trim()), ok: true);
        }
        catch (OperationCanceledException) { ShowTest(L10n.T("ai.err.timeout"), ok: false); }
        catch (AiException ex) { ShowTest(ex.Message, ok: false); }
        catch (Exception ex) { ShowTest(L10n.T("ai.err.unexpected") + " " + ex.Message, ok: false); }
        finally
        {
            _testing = false;
            BtnTest.IsEnabled = true;
        }
    }

    private void ShowTest(string text, bool? ok)
    {
        TestCard.Visibility = Visibility.Visible;
        TestText.Text = text;
        (TestCard.Background, TestText.Foreground) = ok switch
        {
            true  => ((Brush)new SolidColorBrush(Color.FromRgb(0xEC, 0xFD, 0xF5)), (Brush)new SolidColorBrush(Color.FromRgb(0x04, 0x78, 0x57))),
            false => (new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2)), new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C))),
            _     => ((Brush)FindResource("AccentSoftBrush"), (Brush)FindResource("TextBrush")),
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Collect().Save();
        DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
