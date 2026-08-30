using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using System.Linq;
using LetterboxdSync.Configuration;
using LetterboxdSync.ScheduledTasks;

namespace LetterboxdSync
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

        public override string Name => "Letterboxd Sync";

        public override Guid Id => Guid.Parse("f62e84d4-5390-482a-a96d-a60d0ee89311");

        public static Plugin? Instance { get; private set; }

        internal static int NormalizeSyncIntervalHours(int intervalHours) => intervalHours is 1 or 6 or 12 or 24 ? intervalHours : 12;

        public override void UpdateConfiguration(BasePluginConfiguration configuration)
        {
            base.UpdateConfiguration(configuration);

            var task = _taskManager.ScheduledTasks.FirstOrDefault(worker => worker.ScheduledTask is LetterboxdSyncTask);
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
                    Name = "LetterboxdSyncConfigPage",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
                }
            };
        }
    }
}
