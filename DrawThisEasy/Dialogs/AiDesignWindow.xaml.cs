using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DrawThisEasy.Models;
using DrawThisEasy.Services;
using DrawThisEasy.Services.Ai;

namespace DrawThisEasy.Dialogs;

/// "Design with AI": the user describes a diagram, the selected engine designs it with our
/// primitives (see AiDiagram), and the caller places the resulting model.
public partial class AiDesignWindow : Window
{
    private static string _lastPrompt = "";
    private static readonly string[] ExampleKeys = { "ai.example.1", "ai.example.2", "ai.example.3", "ai.example.4" };

    private readonly DiagramModel _current;
    private CancellationTokenSource? _cts;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch _clock = new();
    private int _received;

    /// The generated diagram (set when the dialog returns true).
    public DiagramModel? Result { get; private set; }
    /// True when the result is an edited version of the current diagram (replace, don't insert).
    public bool EditedCurrent { get; private set; }

    public AiDesignWindow(DiagramModel current)
    {
        InitializeComponent();
        _current = current;

        TitleText.Text      = L10n.T("ai.title");
        SubtitleText.Text   = L10n.T("ai.subtitle");
        LblEngine.Text      = L10n.T("ai.engine").ToUpperInvariant();
        BtnSettings.Content = L10n.T("ai.settings.button");
        LblPrompt.Text      = L10n.T("ai.prompt").ToUpperInvariant();
        PromptHint.Text     = L10n.T("ai.prompt.hint");
        LblExamples.Text    = L10n.T("ai.examples");
        ChkEditCurrent.Content = L10n.T("ai.editcurrent");
        ShortcutHint.Text   = L10n.T("ai.shortcut.hint");
        BtnCancel.Content   = L10n.T("modal.cancel");
        BtnGenerate.Content = L10n.T("ai.generate");

        foreach (var p in AiProviders.All)
            CmbProvider.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p, ToolTip = p.Description });
        var active = AiProviders.Find(AiSettings.Current.Provider);
        CmbProvider.SelectedIndex = AiProviders.All.ToList().IndexOf(active);

        foreach (var key in ExampleKeys)
        {
            var text = L10n.T(key);
            var idle = (System.Windows.Media.Brush)FindResource("BgBrush");
            var hover = (System.Windows.Media.Brush)FindResource("AccentSoftBrush");
            var chip = new Border
            {
                Child = new TextBlock
                {
                    Text = text, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 250, FontSize = 11.5,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                },
                ToolTip = text,
                Background = idle,
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand,
            };
            chip.MouseEnter += (_, _) => chip.Background = hover;
            chip.MouseLeave += (_, _) => chip.Background = idle;
            chip.MouseLeftButtonUp += (_, _) =>
            {
                if (_cts != null) return;
                PromptBox.Text = text; PromptBox.Focus(); PromptBox.CaretIndex = text.Length;
            };
            ExamplePanel.Children.Add(chip);
        }

        bool hasContent = current.Shapes.Count > 0;
        ChkEditCurrent.IsEnabled = hasContent;
        if (!hasContent) ChkEditCurrent.Opacity = 0.55;

        PromptBox.Text = _lastPrompt;
        PromptBox.CaretIndex = PromptBox.Text.Length;
        UpdateHint();

        _timer.Tick += (_, _) => UpdateStatus();
        Loaded += (_, _) => PromptBox.Focus();
        Closed += (_, _) => { _cts?.Cancel(); _timer.Stop(); };
    }

    private AiProviderInfo SelectedProvider =>
        (CmbProvider.SelectedItem as ComboBoxItem)?.Tag as AiProviderInfo ?? AiProviders.All[0];

    private void CmbProvider_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadModels();

    private void LoadModels()
    {
        var p = SelectedProvider;
        CmbModel.Items.Clear();
        foreach (var m in p.ModelChoices) CmbModel.Items.Add(m);
        var model = AiSettings.Current.ModelFor(p);
        if (CmbModel.Items.Contains(model)) CmbModel.SelectedItem = model;
        else { CmbModel.SelectedIndex = -1; CmbModel.Text = model; }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (AiSettingsWindow.Show(this, SelectedProvider.Id))
        {
            CmbProvider.SelectedIndex = AiProviders.All.ToList().IndexOf(AiProviders.Find(AiSettings.Current.Provider));
            LoadModels();
        }
    }

    private void PromptBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateHint();
    private void UpdateHint() => PromptHint.Visibility = PromptBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void PromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            Generate_Click(sender, e);
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null) return;
        var request = PromptBox.Text.Trim();
        if (request.Length == 0) { ShowError(L10n.T("ai.err.noprompt")); return; }
        _lastPrompt = PromptBox.Text;

        // Remember the engine + model the user just picked.
        var info = SelectedProvider;
        var settings = AiSettings.Current;
        settings.Provider = info.Id;
        var model = CmbModel.Text.Trim();
        settings.Models[info.Id] = model.Length > 0 ? model : info.DefaultModel;
        settings.Save();

        bool editCurrent = ChkEditCurrent.IsChecked == true && ChkEditCurrent.IsEnabled;
        SetBusy(true);
        _cts = new CancellationTokenSource();
        try
        {
            var provider = info.Create(settings);
            var progress = new Progress<int>(n => _received = n);
            var result = await AiDiagram.GenerateAsync(provider, request, editCurrent ? _current : null, progress, _cts.Token);
            Result = result.Model;
            EditedCurrent = editCurrent;
            DialogResult = true;
        }
        catch (OperationCanceledException) { /* user cancelled */ }
        catch (AiException ex) { ShowError(ex.Message); }
        catch (Exception ex) { ShowError(L10n.T("ai.err.unexpected") + " " + ex.Message); }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            if (IsLoaded) SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        PromptBox.IsReadOnly = busy;
        CmbProvider.IsEnabled = CmbModel.IsEnabled = BtnSettings.IsEnabled = BtnGenerate.IsEnabled = !busy;
        ExamplePanel.IsEnabled = !busy;
        ChkEditCurrent.IsEnabled = !busy && _current.Shapes.Count > 0;
        StatusCard.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            ErrorCard.Visibility = Visibility.Collapsed;
            _received = 0;
            _clock.Restart();
            UpdateStatus();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _clock.Stop();
        }
    }

    private void UpdateStatus()
    {
        var secs = (int)_clock.Elapsed.TotalSeconds;
        var text = string.Format(L10n.T("ai.status.working"), SelectedProvider.Name, secs);
        if (_received > 0) text += " · " + string.Format(L10n.T("ai.status.received"), _received);
        StatusText.Text = text;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorCard.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null) { _cts.Cancel(); return; }   // stop the request, keep the dialog
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
