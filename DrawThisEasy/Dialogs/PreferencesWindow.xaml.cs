using System.Windows;
using DrawThisEasy.Models;
using DrawThisEasy.Services;

namespace DrawThisEasy.Dialogs;

public partial class PreferencesWindow : Window
{
    public PreferencesWindow()
    {
        InitializeComponent();

        TitleText.Text   = L10n.T("pref.title");
        LblConnector.Text = L10n.T("pref.connector").ToUpperInvariant();
        LblRouting.Text  = L10n.T("conn.routing");
        LblStroke.Text   = L10n.T("conn.stroke");
        ChkSnap.Content  = L10n.T("pref.snap");
        LblUnits.Text    = L10n.T("pref.units");
        LblFilesHeader.Text = L10n.T("pref.files").ToUpperInvariant();
        ChkAutosave.Content = L10n.T("pref.autosave");
        ChkRestoreSession.Content = L10n.T("pref.restore.session");
        BtnCancel.Content = L10n.T("modal.cancel");
        BtnSave.Content  = L10n.T("topbar.save");

        CmbRouting.Items.Add(L10n.T("conn.straight"));
        CmbRouting.Items.Add(L10n.T("conn.curved"));
        CmbRouting.Items.Add(L10n.T("conn.elbow"));

        CmbStroke.Items.Add(L10n.T("conn.solid"));
        CmbStroke.Items.Add(L10n.T("conn.dashed"));
        CmbStroke.Items.Add(L10n.T("conn.dotted"));

        CmbUnits.Items.Add(L10n.T("unit.pixels"));
        CmbUnits.Items.Add(L10n.T("unit.cm"));
        CmbUnits.Items.Add(L10n.T("unit.inches"));
        CmbUnits.Items.Add(L10n.T("unit.picas"));

        var s = AppSettings.Current;
        CmbRouting.SelectedIndex = (int)s.DefaultRouting;
        CmbStroke.SelectedIndex  = (int)s.DefaultStroke;
        CmbUnits.SelectedIndex   = (int)s.Units;
        ChkSnap.IsChecked        = s.SnapEnabled;
        ChkAutosave.IsChecked    = s.AutosaveEnabled;
        ChkRestoreSession.IsChecked = s.RestoreOpenFilesOnStartup;
    }

    /// Shows the dialog; returns true if the user saved changes.
    public static bool Show(Window owner)
    {
        var w = new PreferencesWindow { Owner = owner };
        return w.ShowDialog() == true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        new AppSettings
        {
            DefaultRouting = (ConnectorRouting)System.Math.Max(0, CmbRouting.SelectedIndex),
            DefaultStroke  = (StrokeStyle)System.Math.Max(0, CmbStroke.SelectedIndex),
            Units          = (RulerUnit)System.Math.Max(0, CmbUnits.SelectedIndex),
            SnapEnabled    = ChkSnap.IsChecked == true,
            AutosaveEnabled = ChkAutosave.IsChecked == true,
            AutosaveIntervalSeconds = AppSettings.Current.AutosaveIntervalSeconds,
            RestoreOpenFilesOnStartup = ChkRestoreSession.IsChecked == true
        }.Save();

        DialogResult = true;
        Close();
    }
}
