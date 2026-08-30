using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using System.Linq;
using LetterboxdWatchedSync.Configuration;
using LetterboxdWatchedSync.ScheduledTasks;

namespace LetterboxdWatchedSync
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly ITaskManager _taskManager;

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ITaskManager taskManager)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            _taskManager = taskManager;
        }

        public override string Name => "Letterboxd Watched Sync";

        public override Guid Id => Guid.Parse("c8d62b9a-41e1-4c12-9c47-73d8ab5d63f9");

        public static Plugin? Instance { get; private set; }

        internal static int NormalizeSyncIntervalHours(int intervalHours) => intervalHours is 1 or 6 or 12 or 24 ? intervalHours : 24;

        public override void UpdateConfiguration(BasePluginConfiguration configuration)
        {
            base.UpdateConfiguration(configuration);

            var task = _taskManager.ScheduledTasks.FirstOrDefault(worker => worker.ScheduledTask is LetterboxdWatchedSyncTask);
            if (task != null)
            {
                task.Triggers = new[]
                {
                    new TaskTriggerInfo
                    {
                        Type = TaskTriggerInfoType.IntervalTrigger,
                        IntervalTicks = TimeSpan.FromHours(NormalizeSyncIntervalHours(Configuration.SyncIntervalHours)).Ticks
                    }
                };
            }
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "LetterboxdWatchedSyncConfigPage",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
                }
            };
        }
    }
}
