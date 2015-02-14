﻿using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Plugins.Trailers.LocalTrailers
{
    /// <summary>
    /// Class LocalTrailerDownloadTask
    /// </summary>
    class LocalTrailerDownloadTask : IScheduledTask, IConfigurableScheduledTask
    {
        /// <summary>
        /// The _library manager
        /// </summary>
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IChannelManager _channelManager;
        private readonly ILibraryMonitor _libraryMonitor;

        public LocalTrailerDownloadTask(ILibraryManager libraryManager, ILogger logger, ILibraryMonitor libraryMonitor, IChannelManager channelManager)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _channelManager = channelManager;
            _libraryMonitor = libraryMonitor;
        }

        /// <summary>
        /// Gets the category.
        /// </summary>
        /// <value>The category.</value>
        public string Category
        {
            get { return "Trailers"; }
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        /// <value>The description.</value>
        public string Description
        {
            get { return "Downloads local trailers for movies in your library."; }
        }

        /// <summary>
        /// Executes the specified cancellation token.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="progress">The progress.</param>
        /// <returns>Task.</returns>
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var items = _libraryManager.RootFolder
                .RecursiveChildren
                .OfType<Movie>()
                .Where(i => i.LocationType == LocationType.FileSystem && i.LocalTrailerIds.Count == 0)
                .ToList();

            var numComplete = 0;

            var movieProviderIds = new List<MetadataProviders>
            {
                MetadataProviders.Imdb,
                MetadataProviders.Tmdb
            };

            foreach (var item in items)
            {
                try
                {
                    await new LocalTrailerDownloader(_logger, _channelManager, _libraryMonitor)
                        .DownloadTrailerForItem(item, ChannelMediaContentType.MovieExtra, movieProviderIds, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error downloading trailer for {0}", ex, item.Name);
                }

                numComplete++;

                double percent = numComplete;
                percent /= items.Count;
                progress.Report(percent * 100);
            }

            progress.Report(100);
        }

        /// <summary>
        /// Gets the default triggers.
        /// </summary>
        /// <returns>IEnumerable{ITaskTrigger}.</returns>
        public IEnumerable<ITaskTrigger> GetDefaultTriggers()
        {
            return new ITaskTrigger[]
                {
                    new DailyTrigger { TimeOfDay = TimeSpan.FromHours(2) }
                };
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "Download local trailers"; }
        }

        public bool IsEnabled
        {
            get { return Plugin.Instance.Configuration.EnableLocalTrailerDownloads; }
        }

        public bool IsHidden
        {
            get { return !Plugin.Instance.Configuration.EnableLocalTrailerDownloads; }
        }
    }
}
