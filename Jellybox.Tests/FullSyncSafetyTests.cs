using LetterboxdSync.ScheduledTasks;
using MediaBrowser.Controller.Playlists;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jellybox.Tests;

public class FullSyncSafetyTests
{
    [Fact]
    public async Task ApplyFullSyncAsync_DoesNotRemoveOriginalEntriesWhenStagingFails()
    {
        var playlistManager = new Mock<IPlaylistManager>(MockBehavior.Strict);
        var playlistId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var stagedMovieIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var originalEntryIds = new[] { Guid.NewGuid().ToString("N") };

        playlistManager
            .Setup(manager => manager.AddItemToPlaylistAsync(playlistId, stagedMovieIds, userId))
            .ThrowsAsync(new InvalidOperationException("Jellyfin is unavailable."));

        var task = new LetterboxdSyncTask(NullLogger<LetterboxdSyncTask>.Instance, null!, playlistManager.Object, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.ApplyFullSyncAsync(playlistId, stagedMovieIds, originalEntryIds, userId));

        playlistManager.Verify(
            manager => manager.RemoveItemFromPlaylistAsync(It.IsAny<string>(), It.IsAny<List<string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyFullSyncAsync_StagesBeforeRemovingOriginalEntries()
    {
        var playlistManager = new Mock<IPlaylistManager>(MockBehavior.Strict);
        var playlistId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var stagedMovieIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var originalEntryIds = new[] { Guid.NewGuid().ToString("N") };
        var calls = new List<string>();

        playlistManager
            .Setup(manager => manager.AddItemToPlaylistAsync(playlistId, stagedMovieIds, userId))
            .Callback(() => calls.Add("add"))
            .Returns(Task.CompletedTask);
        playlistManager
            .Setup(manager => manager.RemoveItemFromPlaylistAsync(playlistId.ToString(), originalEntryIds.ToList()))
            .Callback(() => calls.Add("remove"))
            .Returns(Task.CompletedTask);

        var task = new LetterboxdSyncTask(NullLogger<LetterboxdSyncTask>.Instance, null!, playlistManager.Object, null!);

        await task.ApplyFullSyncAsync(playlistId, stagedMovieIds, originalEntryIds, userId);

        Assert.Equal(new[] { "add", "remove" }, calls);
    }
}
