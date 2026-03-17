using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpcomingGamesTagger
{
    public class UpcomingGamesTaggerSettings : ObservableObject
    {
        private string tagName = "Upcoming";
        private bool autoUpdateOnLibraryChange = true;
        private bool includeGamesWithoutReleaseDate = false;
        private int daysAheadThreshold = 365; // Only consider games releasing within a year
        private bool showNotifications = true;

        public string TagName { get => tagName; set => SetValue(ref tagName, value); }
        public bool AutoUpdateOnLibraryChange { get => autoUpdateOnLibraryChange; set => SetValue(ref autoUpdateOnLibraryChange, value); }
        public bool IncludeGamesWithoutReleaseDate { get => includeGamesWithoutReleaseDate; set => SetValue(ref includeGamesWithoutReleaseDate, value); }
        public int DaysAheadThreshold { get => daysAheadThreshold; set => SetValue(ref daysAheadThreshold, value); }
        public bool ShowNotifications { get => showNotifications; set => SetValue(ref showNotifications, value); }
    }

    public class UpcomingGamesTaggerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly UpcomingGamesTagger plugin;
        private UpcomingGamesTaggerSettings editingClone { get; set; }

        private UpcomingGamesTaggerSettings settings;
        public UpcomingGamesTaggerSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public UpcomingGamesTaggerSettingsViewModel(UpcomingGamesTagger plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<UpcomingGamesTaggerSettings>();

            // LoadPluginSettings returns null if no saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new UpcomingGamesTaggerSettings();
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}