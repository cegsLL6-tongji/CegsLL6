using AeonHacs;
using System;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Xml;

namespace CegsLL6;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    SoundPlayer soundPlayer;
    HacsLog EventLog = Hacs.EventLog;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LoadPreferences();

        var window = new AeonHacs.Wpf.Views.MainWindow();
        MainWindow = window;

        HacsBridge bridge = new();
        bridge.CloseUI = () => Dispatcher.Invoke(window.Close);
        bridge.Started = () => Dispatcher.Invoke(() =>
        {
            window.LoadControlPanel(new Views.ControlPanel(bridge.HacsImplementation));
            MainWindow.Show();
        });
        Task.Run(bridge.Start);
    }

    /// <summary>
    /// Save preferences and close the event log before the application exits.
    /// </summary>
    /// <param name="e"></param>
    protected override void OnExit(ExitEventArgs e)
    {
        SavePreferences();
        Hacs.EventLog.Close();
        base.OnExit(e);
    }

    string PreferencesFileName = "Preferences.xaml";
    void LoadPreferences()
    {
        ResourceDictionary prefs = null;
        try
        {
            using (var reader = XmlReader.Create(PreferencesFileName))
                prefs = (ResourceDictionary)XamlReader.Load(reader);
        }
        catch (Exception e) { var ignore = e; }

        if (prefs == null)
            prefs = new ResourceDictionary();
        Resources["PreferencesDictionary"] = prefs;
        Resources.MergedDictionaries.Add(prefs);
    }

    void SavePreferences()
    {
        try
        {
            var prefs = (ResourceDictionary)Resources["PreferencesDictionary"];
            if (prefs != null)
            {
                var writerSettings = new XmlWriterSettings()
                {
                    OmitXmlDeclaration = true,
                    Indent = true,
                    IndentChars = "\t",
                    CloseOutput = true,
                };
                using (var writer = System.Xml.XmlWriter.Create(PreferencesFileName, writerSettings))
                    XamlWriter.Save(prefs, writer);
            }
        }
        catch (Exception e) { var ignore = e; }
    }
}
