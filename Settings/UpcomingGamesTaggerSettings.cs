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
        private Guid? managedTagId = null;
        private int settingsVersion = 0;

        public string TagName { get => tagName; set => SetValue(ref tagName, value); }
        public bool AutoUpdateOnLibraryChange { get => autoUpdateOnLibraryChange; set => SetValue(ref autoUpdateOnLibraryChange, value); }
        public bool IncludeGamesWithoutReleaseDate { get => includeGamesWithoutReleaseDate; set => SetValue(ref includeGamesWithoutReleaseDate, value); }
        public int DaysAheadThreshold { get => daysAheadThreshold; set => SetValue(ref daysAheadThreshold, value); }
        public bool ShowNotifications { get => showNotifications; set => SetValue(ref showNotifications, value); }

        /// <summary>
        /// Id of the tag this plugin owns. Tracked by id rather than by name so that
        /// renaming TagName moves the existing tag instead of stranding it, and so a
        /// same-named tag the user created themselves is never adopted or stripped.
        /// </summary>
        public Guid? ManagedTagId { get => managedTagId; set => SetValue(ref managedTagId, value); }

        /// <summary>
        /// Schema version of this settings file. Absent in files written by 1.0.1 and
        /// earlier, which deserialize to 0. Used to tell a genuine upgrade apart from a
        /// fresh install, which ManagedTagId alone cannot do: a fresh install that saves
        /// settings before the tag is created also leaves ManagedTagId null.
        /// </summary>
        public int SettingsVersion { get => settingsVersion; set => SetValue(ref settingsVersion, value); }
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

        private const int CurrentSettingsVersion = 1;

        private readonly object _saveLock = new object();
        private bool _isEditing;

        /// <summary>
        /// True only when upgrading an install that predates <see cref="UpcomingGamesTaggerSettings.ManagedTagId"/>.
        /// Such an install really was managing the same-named tag, so adopting it once
        /// avoids stranding it. A fresh install must never adopt a tag it did not create.
        /// </summary>
        public bool AdoptExistingTagByName { get; }

        /// <summary>
        /// Tag name as it was when settings loaded. Adoption matches against this rather
        /// than the live TagName, so renaming the tag before the upgrade pass runs cannot
        /// make the plugin adopt some unrelated tag that happens to carry the new name.
        /// </summary>
        public string AdoptionTagName { get; }

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
                AdoptExistingTagByName = savedSettings.SettingsVersion < CurrentSettingsVersion;
                AdoptionTagName = savedSettings.TagName;
            }
            else
            {
                // Stamped up front so that saving this dialog before the tag exists does
                // not later look like an upgrade from a version that tagged by name.
                Settings = new UpcomingGamesTaggerSettings { SettingsVersion = CurrentSettingsVersion };
            }
        }

        /// <summary>
        /// Persists the id of a tag the plugin has just created or adopted. Called from
        /// the background update pass, so it takes the save lock.
        /// </summary>
        public void SetManagedTagId(Guid tagId)
        {
            lock (_saveLock)
            {
                Settings.ManagedTagId = tagId;
                Settings.SettingsVersion = CurrentSettingsVersion;

                // While the dialog is open, Settings also holds edits the user has not
                // confirmed yet. EndEdit or CancelEdit will persist the id instead.
                if (!_isEditing)
                {
                    plugin.SavePluginSettings(Settings);
                }
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            lock (_saveLock)
            {
                editingClone = Serialization.GetClone(Settings);
                _isEditing = true;
            }
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            lock (_saveLock)
            {
                // ManagedTagId is carried across the revert: a background update pass can
                // assign it while the dialog is open, and discarding it would strand the tag.
                var managedTagId = Settings.ManagedTagId;
                Settings = editingClone;
                Settings.ManagedTagId = managedTagId;
                _isEditing = false;

                // Written back so the file matches memory: a pass may have assigned an id
                // that SetManagedTagId deliberately did not persist mid-edit.
                plugin.SavePluginSettings(Settings);
            }
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            lock (_saveLock)
            {
                _isEditing = false;
                plugin.SavePluginSettings(Settings);
            }

            // A renamed tag or a changed threshold has to take effect now; the plugin
            // used to keep serving a stale tag until Playnite was restarted.
            plugin.OnSettingsSaved();
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