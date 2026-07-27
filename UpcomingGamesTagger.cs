using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace UpcomingGamesTagger
{
    public class UpcomingGamesTagger : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        // Polling hourly rather than scheduling a single callback for midnight keeps the
        // refresh correct across DST changes and machine sleep/resume.
        private static readonly TimeSpan RolloverCheckInterval = TimeSpan.FromHours(1);

        private UpcomingGamesTaggerSettingsViewModel settings { get; set; }
        private Tag _upcomingTag;
        private Timer _rolloverTimer;
        private DateTime _lastRunDate;
        private volatile bool _stopping;

        // Library updates, the menu action, the settings dialog and the rollover timer
        // can all trigger a pass; serialise them so two passes cannot interleave writes.
        private readonly object _updateLock = new object();

        public override Guid Id { get; } = Guid.Parse("22b532e7-0caa-4c5a-bacf-3009c4eb7eeb");

        public UpcomingGamesTagger(IPlayniteAPI api) : base(api)
        {
            settings = new UpcomingGamesTaggerSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info("UpcomingGamesTagger: Application started, scheduling upcoming games tag update");

            // The pass walks the whole library, so keep it off Playnite's startup path.
            RunUpdateInBackground("Startup tag update");

            _rolloverTimer = new Timer(OnRolloverCheck, null, RolloverCheckInterval, RolloverCheckInterval);
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Disposing the timer does not wait for a callback that is already running,
            // and a background pass may still be mid-walk, so signal them to stop writing
            // before Playnite closes the database underneath them.
            _stopping = true;
            _rolloverTimer?.Dispose();
            _rolloverTimer = null;
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (settings.Settings.AutoUpdateOnLibraryChange)
            {
                logger.Info("UpcomingGamesTagger: Library updated, refreshing upcoming games tag");

                // Playnite raises this on the UI thread, so the pass has to be handed off
                // rather than run inline; it walks the whole library.
                RunUpdateInBackground("Library update tag update");
            }
        }

        /// <summary>
        /// Called by the settings view model once changes are saved, so a renamed tag or
        /// an adjusted threshold applies without restarting Playnite.
        /// </summary>
        public void OnSettingsSaved()
        {
            RunUpdateInBackground("Settings change tag update");
        }

        private void RunUpdateInBackground(string context)
        {
            Task.Run(() =>
            {
                try
                {
                    UpdateUpcomingGamesTag();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"UpcomingGamesTagger: {context} failed");
                }
            });
        }

        /// <summary>
        /// Re-tags when the calendar day changes, so a long-running Playnite session does
        /// not keep yesterday's results.
        /// </summary>
        private void OnRolloverCheck(object state)
        {
            // A timer callback runs on the thread pool, where an escaping exception would
            // take down the whole process.
            try
            {
                if (DateTime.Now.Date == _lastRunDate)
                {
                    return;
                }

                logger.Info("UpcomingGamesTagger: Date changed, refreshing upcoming games tag");
                UpdateUpcomingGamesTag();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "UpcomingGamesTagger: Scheduled tag refresh failed");
            }
        }

        /// <summary>
        /// Resolves the tag this plugin owns, creating it when necessary.
        /// </summary>
        /// <returns>True when <see cref="_upcomingTag"/> is usable.</returns>
        private bool EnsureUpcomingTag()
        {
            var tagName = settings.Settings.TagName;
            var managedTagId = settings.Settings.ManagedTagId;

            if (managedTagId.HasValue)
            {
                _upcomingTag = PlayniteApi.Database.Tags.Get(managedTagId.Value);

                if (_upcomingTag != null)
                {
                    ApplyTagName(tagName);
                    return true;
                }

                logger.Warn($"UpcomingGamesTagger: Managed tag {managedTagId.Value} no longer exists, recreating it");
            }
            else if (settings.AdoptExistingTagByName)
            {
                // One-time upgrade path for installs predating ManagedTagId.
                var adoptionName = settings.AdoptionTagName;
                _upcomingTag = PlayniteApi.Database.Tags.FirstOrDefault(t => t.Name == adoptionName);

                if (_upcomingTag != null)
                {
                    logger.Info($"UpcomingGamesTagger: Adopting previously managed tag '{adoptionName}' ({_upcomingTag.Id})");
                    settings.SetManagedTagId(_upcomingTag.Id);
                    ApplyTagName(tagName);
                    return true;
                }
            }

            return CreateUpcomingTag(tagName);
        }

        /// <summary>
        /// Renames the owned tag in place rather than leaving the old one applied to every
        /// game and creating a second tag alongside it.
        /// </summary>
        private void ApplyTagName(string tagName)
        {
            if (_upcomingTag.Name == tagName)
            {
                return;
            }

            logger.Info($"UpcomingGamesTagger: Renaming managed tag '{_upcomingTag.Name}' to '{tagName}'");
            _upcomingTag.Name = tagName;
            PlayniteApi.Database.Tags.Update(_upcomingTag);
        }

        private bool CreateUpcomingTag(string tagName)
        {
            try
            {
                var tag = new Tag(tagName);
                PlayniteApi.Database.Tags.Add(tag);
                _upcomingTag = tag;
                settings.SetManagedTagId(tag.Id);

                logger.Info($"UpcomingGamesTagger: Created tag '{tagName}' ({tag.Id})");
                ShowNotification(
                    "upcoming-tag-created",
                    string.Format(ResourceProvider.GetString("LOCUpcomingGamesTaggerNotifTagCreated"), tagName));

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"UpcomingGamesTagger: Failed to create tag '{tagName}'");
                _upcomingTag = null;
                return false;
            }
        }

        private void UpdateUpcomingGamesTag()
        {
            lock (_updateLock)
            {
                var today = DateTime.Now.Date;

                if (_stopping)
                {
                    return;
                }

                try
                {
                    // Inside the try: resolving the tag can write to the database and to
                    // the settings file, and either can fail.
                    if (!EnsureUpcomingTag())
                    {
                        logger.Error("UpcomingGamesTagger: Could not resolve the managed tag, aborting update");
                        return;
                    }

                    var tagId = _upcomingTag.Id;
                    var gamesToAdd = new List<Game>();
                    var gamesToRemove = new List<Game>();

                    // Collect first, then write, so the database is not mutated while it
                    // is being enumerated.
                    foreach (var game in PlayniteApi.Database.Games)
                    {
                        var isTagged = game.TagIds?.Contains(tagId) == true;
                        var isUpcoming = UpcomingGameEvaluator.IsUpcoming(
                            game.ReleaseDate,
                            today,
                            settings.Settings.DaysAheadThreshold,
                            settings.Settings.IncludeGamesWithoutReleaseDate);

                        if (isUpcoming && !isTagged)
                        {
                            gamesToAdd.Add(game);
                        }
                        else if (!isUpcoming && isTagged)
                        {
                            gamesToRemove.Add(game);
                        }
                    }

                    if (gamesToAdd.Any() || gamesToRemove.Any())
                    {
                        int added;
                        int removed;

                        using (PlayniteApi.Database.BufferedUpdate())
                        {
                            added = ApplyToGames(gamesToAdd, game =>
                            {
                                if (game.TagIds == null)
                                {
                                    game.TagIds = new List<Guid>();
                                }

                                game.TagIds.Add(tagId);
                            });

                            removed = ApplyToGames(gamesToRemove, game => game.TagIds.Remove(tagId));
                        }

                        var message = string.Format(
                            ResourceProvider.GetString("LOCUpcomingGamesTaggerNotifTagUpdated"),
                            settings.Settings.TagName, added, removed);
                        logger.Info($"UpcomingGamesTagger: {message}");
                        ShowNotification("upcoming-tag-updated", message);
                    }

                    // Only on success, so a failed pass is retried at the next rollover check.
                    _lastRunDate = today;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "UpcomingGamesTagger: Failed to update upcoming games tag");
                }
            }
        }

        /// <summary>
        /// Applies <paramref name="mutate"/> to each game and persists it, isolating
        /// per-game failures so one bad entry cannot abandon the rest of the batch.
        /// </summary>
        /// <returns>How many games were actually updated.</returns>
        private int ApplyToGames(List<Game> games, Action<Game> mutate)
        {
            var updated = 0;

            foreach (var game in games)
            {
                if (_stopping)
                {
                    logger.Info("UpcomingGamesTagger: Shutting down, stopping tag update early");
                    break;
                }

                try
                {
                    // The list was collected before these writes began, so a game may have
                    // been deleted in the meantime.
                    if (PlayniteApi.Database.Games[game.Id] == null)
                    {
                        continue;
                    }

                    mutate(game);
                    PlayniteApi.Database.Games.Update(game);
                    updated++;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"UpcomingGamesTagger: Failed to update tags for game '{game.Name}' ({game.Id})");
                }
            }

            return updated;
        }

        private void ShowNotification(string id, string message)
        {
            if (!settings.Settings.ShowNotifications)
            {
                return;
            }

            // Notifications is bound to the UI and update passes run on background
            // threads. BeginInvoke rather than Invoke: the UI thread can be waiting on
            // _updateLock for a menu-triggered pass, and blocking here would deadlock.
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                PlayniteApi.Notifications.Add(new NotificationMessage(id, message, NotificationType.Info))));
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    Description = ResourceProvider.GetString("LOCUpcomingGamesTaggerMenuUpdateTag"),
                    MenuSection = "@" + ResourceProvider.GetString("LOCUpcomingGamesTaggerMenuSection"),
                    Action = (menuArgs) => {
                        // Runs the pass on a background thread so the UI stays responsive
                        // while the library is walked.
                        var result = PlayniteApi.Dialogs.ActivateGlobalProgress(
                            progressArgs => UpdateUpcomingGamesTag(),
                            new GlobalProgressOptions(ResourceProvider.GetString("LOCUpcomingGamesTaggerProgressUpdating"), false));

                        // The dialog swallows the exception, so a user who asked for this
                        // explicitly would otherwise see it close as if it had worked.
                        if (result.Error != null)
                        {
                            logger.Error(result.Error, "UpcomingGamesTagger: Manual tag update failed");
                        }
                    }
                }
            };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new UpcomingGamesTaggerSettingsView();
        }
    }
}
