using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Unleash.Communication;
using Unleash.Internal;
using Unleash.Metrics;
using Unleash.Scheduling;
using Unleash.Strategies;

namespace Unleash
{
    internal class UnleashServices : IDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly IUnleashScheduledTaskManager scheduledTaskManager;

        internal CancellationToken CancellationToken { get; }
        internal IUnleashContextProvider ContextProvider { get; }
        internal ThreadSafeToggleCollection ToggleCollection { get; }
        internal bool IsMetricsDisabled { get; }
        internal ThreadSafeMetricsBucket MetricsBucket { get; }

        public UnleashServices(UnleashSettings settings, Dictionary<string, IStrategy> strategyMap)
        {
            var fileSystem = settings.FileSystem ?? new FileSystem(settings.Encoding);

            var backupFile = settings.GetFeatureToggleFilePath();
            var etagBackupFile = settings.GetFeatureToggleETagFilePath();

            // Cancellation
            CancellationToken = cancellationTokenSource.Token;
            ContextProvider = settings.UnleashContextProvider;


            var loader = new CachedFilesLoader(settings.JsonSerializer, fileSystem, backupFile, etagBackupFile);
            var cachedFilesResult = loader.EnsureExistsAndLoad();

            ToggleCollection = new ThreadSafeToggleCollection
            {
                Instance = cachedFilesResult.InitialToggleCollection ?? new ToggleCollection()
            };

            MetricsBucket = new ThreadSafeMetricsBucket();

            IUnleashApiClient apiClient;
            if (settings.UnleashApiClient == null)
            {
                var httpClient = settings.HttpClientFactory.Create(settings.UnleashApi);
                apiClient = new UnleashApiClient(httpClient, settings.JsonSerializer, new UnleashApiClientRequestHeaders()
                {
                    AppName = settings.AppName,
                    InstanceTag = settings.InstanceTag,
                    CustomHttpHeaders = settings.CustomHttpHeaders
                });
                if (settings.LoadTogglesImmediately)
                {
                    var toggles = apiClient.FetchToggles("", CancellationToken.None);
                    ToggleCollection.Instance = toggles.Result.ToggleCollection;
                }
            }
            else
            {
                // Mocked backend: fill instance collection 
                apiClient = settings.UnleashApiClient;
                var toggles = apiClient.FetchToggles("", CancellationToken.None);
                ToggleCollection.Instance = toggles.Result.ToggleCollection;
            }

            scheduledTaskManager = settings.ScheduledTaskManager;

            IsMetricsDisabled = settings.SendMetricsInterval == null;

            var fetchFeatureTogglesTask = new FetchFeatureTogglesTask(
                apiClient, 
                ToggleCollection, 
                settings.JsonSerializer, 
                fileSystem, 
                backupFile, 
                etagBackupFile)
            {
                ExecuteDuringStartup = true,
                Interval = settings.FetchTogglesInterval,
                Etag = cachedFilesResult.InitialETag
            };            

            var scheduledTasks = new List<IUnleashScheduledTask>(){
                fetchFeatureTogglesTask
            };

            if (settings.SendMetricsInterval != null)
            {
                var clientRegistrationBackgroundTask = new ClientRegistrationBackgroundTask(
                    apiClient, 
                    settings,
                    strategyMap.Select(pair => pair.Key).ToList())
                {
                    Interval = TimeSpan.Zero,
                    ExecuteDuringStartup = true
                };

                scheduledTasks.Add(clientRegistrationBackgroundTask);

                var clientMetricsBackgroundTask = new ClientMetricsBackgroundTask(
                    apiClient, 
                    settings, 
                    MetricsBucket)
                {
                    ExecuteDuringStartup = false,
                    Interval = settings.SendMetricsInterval.Value
                };

                scheduledTasks.Add(clientMetricsBackgroundTask);
            }

            scheduledTaskManager.Configure(scheduledTasks, CancellationToken);
        }

        public void Dispose()
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }

            scheduledTaskManager?.Dispose();
            ToggleCollection?.Dispose();
        }
    }
}