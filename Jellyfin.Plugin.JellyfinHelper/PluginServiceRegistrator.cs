using System;
using System.IO;
using System.IO.Abstractions;
using Jellyfin.Plugin.JellyfinHelper.Services.Activity;
using Jellyfin.Plugin.JellyfinHelper.Services.Arr;
using Jellyfin.Plugin.JellyfinHelper.Services.Backup;
using Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;
using Jellyfin.Plugin.JellyfinHelper.Services.ConfigAccess;
using Jellyfin.Plugin.JellyfinHelper.Services.Link;
using Jellyfin.Plugin.JellyfinHelper.Services.PluginLog;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.Scoring;
using Jellyfin.Plugin.JellyfinHelper.Services.Recommendation.WatchHistory;
using Jellyfin.Plugin.JellyfinHelper.Services.Seerr;
using Jellyfin.Plugin.JellyfinHelper.Services.Statistics;
using Jellyfin.Plugin.JellyfinHelper.Services.Timeline;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.JellyfinHelper;

/// <summary>
/// Registers services for dependency injection.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        _ = applicationHost; // Required by interface but unused
        serviceCollection.AddHttpClient("ArrIntegration", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        serviceCollection.AddHttpClient("SeerrIntegration", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        serviceCollection.AddSingleton<ICleanupConfigHelper, CleanupConfigHelper>();
        serviceCollection.AddSingleton<ICleanupTrackingService, CleanupTrackingService>();
        serviceCollection.AddSingleton<ITrashService, TrashService>();
        serviceCollection.AddSingleton<IPluginConfigurationService, PluginConfigurationService>();
        serviceCollection.AddSingleton<IPluginLogService, PluginLogService>();
        serviceCollection.AddSingleton<IMediaStatisticsService, MediaStatisticsService>();
        serviceCollection.AddSingleton<IStatisticsCacheService, StatisticsCacheService>();
        serviceCollection.AddSingleton<IGrowthTimelineService, GrowthTimelineService>();
        serviceCollection.AddSingleton<ILibraryInsightsService, LibraryInsightsService>();
        serviceCollection.AddSingleton<IBackupService, BackupService>();
        serviceCollection.AddSingleton<IFileSystem, FileSystem>();
        serviceCollection.AddSingleton<ISymlinkHelper, SymlinkHelper>();
        serviceCollection.AddSingleton<ILinkHandler, StrmLinkHandler>();
        serviceCollection.AddSingleton<ILinkHandler, SymlinkHandler>();
        serviceCollection.AddSingleton<ILinkRepairService, LinkRepairService>();
        serviceCollection.AddSingleton<IArrIntegrationService, ArrIntegrationService>();
        serviceCollection.AddSingleton<ISeerrIntegrationService, SeerrIntegrationService>();
        serviceCollection.AddSingleton<IWatchHistoryService, WatchHistoryService>();
        serviceCollection.AddSingleton(sp =>
        {
            var dataPath = Plugin.Instance?.DataFolderPath;
            string? weightsPath = null;
            if (!string.IsNullOrEmpty(dataPath))
            {
                weightsPath = Path.Join(dataPath, "ml_weights.json");
            }

            return new LearnedScoringStrategy(weightsPath);
        });
        serviceCollection.AddSingleton(sp =>
        {
            var dataPath = Plugin.Instance?.DataFolderPath;
            string? neuralWeightsPath = null;
            if (!string.IsNullOrEmpty(dataPath))
            {
                neuralWeightsPath = Path.Join(dataPath, "neural_weights.json");
            }

            return new NeuralScoringStrategy(neuralWeightsPath);
        });
        serviceCollection.AddSingleton(sp =>
        {
            // When used inside Ensemble, disable standalone genre penalty (penalty = 1.0)
            return new HeuristicScoringStrategy(genrePenaltyFloor: 1.0);
        });
        serviceCollection.AddSingleton(sp =>
        {
            var dataPath = Plugin.Instance?.DataFolderPath;
            string? statePath = null;
            if (!string.IsNullOrEmpty(dataPath))
            {
                statePath = Path.Join(dataPath, "ensemble_state.json");
            }

            var config = Plugin.Instance?.Configuration;
            var alphaMin = config?.EnsembleAlphaMin ?? 0.3;
            var alphaMax = config?.EnsembleAlphaMax ?? 0.8;
            var genrePenaltyFloor = config?.EnsembleGenrePenaltyFloor ?? 0.10;

            var learned = sp.GetRequiredService<LearnedScoringStrategy>();
            var heuristic = sp.GetRequiredService<HeuristicScoringStrategy>();
            var neural = sp.GetRequiredService<NeuralScoringStrategy>();

            return new EnsembleScoringStrategy(learned, heuristic, neural, statePath, alphaMin, alphaMax, genrePenaltyFloor);
        });
        serviceCollection.AddSingleton<IScoringStrategy>(ResolveScoringStrategy);
        serviceCollection.AddSingleton<IRecommendationEngine, RecommendationEngine>();
        serviceCollection.AddSingleton<IRecommendationCacheService, RecommendationCacheService>();
        serviceCollection.AddSingleton<IUserActivityInsightsService, UserActivityInsightsService>();
        serviceCollection.AddSingleton<IUserActivityCacheService, UserActivityCacheService>();
    }

    /// <summary>
    ///     Resolves the active <see cref="IScoringStrategy"/> based on plugin configuration.
    ///     Valid strategy values: "ensemble" (default), "heuristic", "learned", "neural".
    /// </summary>
    private static IScoringStrategy ResolveScoringStrategy(IServiceProvider sp)
    {
        var config = Jellyfin.Plugin.JellyfinHelper.Plugin.Instance?.Configuration;
        var strategy = config?.RecommendationStrategy ?? "ensemble";

        if (string.Equals(strategy, "heuristic", StringComparison.OrdinalIgnoreCase))
        {
            // Create a standalone instance with full genre penalty (0.10) enabled.
            // The DI-registered singleton uses genrePenaltyFloor=1.0 because it's used
            // inside EnsembleScoringStrategy where the ensemble applies its own penalty.
            var penaltyFloor = config?.EnsembleGenrePenaltyFloor ?? 0.10;
            return new HeuristicScoringStrategy(genrePenaltyFloor: penaltyFloor);
        }

        if (string.Equals(strategy, "learned", StringComparison.OrdinalIgnoreCase))
        {
            // Return the DI-registered LearnedScoringStrategy (which has the correct weights path)
            // via the EnsembleScoringStrategy's internal reference so there's only one instance.
            var ensemble = sp.GetRequiredService<EnsembleScoringStrategy>();
            return ensemble.LearnedStrategy;
        }

        if (string.Equals(strategy, "neural", StringComparison.OrdinalIgnoreCase))
        {
            return sp.GetRequiredService<NeuralScoringStrategy>();
        }

        // Default: Ensemble strategy (resolved via DI with all config applied)
        return sp.GetRequiredService<EnsembleScoringStrategy>();
    }
}
