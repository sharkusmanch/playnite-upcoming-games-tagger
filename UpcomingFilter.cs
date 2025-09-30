using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace UpcomingFilter
{
    public class UpcomingFilter : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private UpcomingFilterSettingsViewModel settings { get; set; }
        private Tag upcomingTag = null;

        public override Guid Id { get; } = Guid.Parse("22b532e7-0caa-4c5a-bacf-3009c4eb7eeb");

        public UpcomingFilter(IPlayniteAPI api) : base(api)
        {
            settings = new UpcomingFilterSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info("UpcomingFilter: Application started, initializing upcoming games tag");
            InitializeUpcomingTag();
            UpdateUpcomingGamesTag();
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (settings.Settings.AutoUpdateOnLibraryChange)
            {
                logger.Info("UpcomingFilter: Library updated, refreshing upcoming games tag");
                UpdateUpcomingGamesTag();
            }
        }

        private void InitializeUpcomingTag()
        {
            try
            {
                // Find existing tag or create new one
                upcomingTag = PlayniteApi.Database.Tags
                    .FirstOrDefault(t => t.Name == settings.Settings.TagName);

                if (upcomingTag == null)
                {
                    upcomingTag = new Tag(settings.Settings.TagName);
                    PlayniteApi.Database.Tags.Add(upcomingTag);

                    if (settings.Settings.ShowNotifications)
                    {
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            "upcoming-tag-created",
                            $"Created '{settings.Settings.TagName}' tag for upcoming games",
                            NotificationType.Info));
                    }

                    logger.Info($"UpcomingFilter: Created new tag '{settings.Settings.TagName}'");
                }
                else
                {
                    logger.Info($"UpcomingFilter: Found existing tag '{settings.Settings.TagName}'");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "UpcomingFilter: Failed to initialize upcoming tag");
            }
        }

        private void UpdateUpcomingGamesTag()
        {
            if (upcomingTag == null)
            {
                InitializeUpcomingTag();
                if (upcomingTag == null)
                {
                    logger.Error("UpcomingFilter: Could not initialize tag, aborting update");
                    return;
                }
            }

            try
            {
                var upcomingGames = GetUpcomingGames();
                var currentTaggedGames = PlayniteApi.Database.Games
                    .Where(g => g.TagIds?.Contains(upcomingTag.Id) == true)
                    .ToHashSet();

                var gamesToAdd = upcomingGames.Where(g => !currentTaggedGames.Contains(g)).ToList();
                var gamesToRemove = currentTaggedGames.Where(g => !upcomingGames.Contains(g)).ToList();

                // Add tag to upcoming games
                foreach (var game in gamesToAdd)
                {
                    if (game.TagIds == null)
                        game.TagIds = new List<Guid>();

                    if (!game.TagIds.Contains(upcomingTag.Id))
                    {
                        game.TagIds.Add(upcomingTag.Id);
                        PlayniteApi.Database.Games.Update(game);
                    }
                }

                // Remove tag from games that are no longer upcoming
                foreach (var game in gamesToRemove)
                {
                    if (game.TagIds?.Contains(upcomingTag.Id) == true)
                    {
                        game.TagIds.Remove(upcomingTag.Id);
                        PlayniteApi.Database.Games.Update(game);
                    }
                }

                // Log and notify about changes
                if (gamesToAdd.Any() || gamesToRemove.Any())
                {
                    var message = $"Updated '{settings.Settings.TagName}' tag: +{gamesToAdd.Count} games, -{gamesToRemove.Count} games";
                    logger.Info($"UpcomingFilter: {message}");

                    if (settings.Settings.ShowNotifications && (gamesToAdd.Count > 0 || gamesToRemove.Count > 0))
                    {
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            "upcoming-tag-updated",
                            message,
                            NotificationType.Info));
                    }
                }

                logger.Info($"UpcomingFilter: Tag now applied to {upcomingGames.Count} upcoming games");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "UpcomingFilter: Failed to update upcoming games tag");
            }
        }

        private List<Game> GetUpcomingGames()
        {
            var currentDate = DateTime.Now.Date;
            var futureThreshold = settings.Settings.DaysAheadThreshold > 0
                ? currentDate.AddDays(settings.Settings.DaysAheadThreshold)
                : DateTime.MaxValue;

            var upcomingGames = PlayniteApi.Database.Games.Where(game =>
            {
                // Include games with release dates in the future
                if (game.ReleaseDate.HasValue)
                {
                    var releaseDate = game.ReleaseDate.Value.Date;
                    return releaseDate > currentDate && releaseDate <= futureThreshold;
                }

                // Optionally include games without release dates
                return settings.Settings.IncludeGamesWithoutReleaseDate;
            }).ToList();

            return upcomingGames;
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    Description = "Update Upcoming Games Tag",
                    MenuSection = "@Upcoming Filter",
                    Action = (menuArgs) => {
                        UpdateUpcomingGamesTag();
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
            return new UpcomingFilterSettingsView();
        }
    }
}