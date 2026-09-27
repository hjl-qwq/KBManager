using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KBManager.core;
using Xunit;

namespace KBManager.Tests
{
    /// <summary>
    /// Tests for the tag index, especially the behaviours the editor relies on:
    /// a brand-new file can be tagged before a scan, and renaming a file keeps
    /// its tags attached.
    /// </summary>
    public class KnowledgeBaseServiceTests : IDisposable
    {
        private readonly string _repo;
        private readonly KnowledgeBaseService _service = new();

        public KnowledgeBaseServiceTests()
        {
            _repo = Path.Combine(Path.GetTempPath(), "kbm_kb_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_repo);
        }

        public void Dispose()
        {
            if (Directory.Exists(_repo)) Directory.Delete(_repo, true);
        }

        [Fact]
        public async Task FindStaleRecords_Returns_Index_Entries_Missing_From_Disk()
        {
            await _service.AddTagToFileAsync(_repo, "keep.md", "t");
            await _service.AddTagToFileAsync(_repo, "gone.md", "t");
            await _service.AddTagToFileAsync(_repo, "sub/deep.md", "t");

            // Disk now only has two of the three indexed paths.
            var onDisk = new[] { "keep.md", "sub/deep.md" };

            var stale = await _service.FindStaleRecordsAsync(_repo, onDisk);
            Assert.True(stale.Success);
            Assert.Equal(new[] { "gone.md" }, stale.Data!.ToArray());
        }

        [Fact]
        public async Task FindStaleRecords_Handles_Missing_Database_And_Empty_Disk()
        {
            // No index yet: nothing can be stale.
            var beforeIndex = await _service.FindStaleRecordsAsync(_repo, Array.Empty<string>());
            Assert.True(beforeIndex.Success);
            Assert.Empty(beforeIndex.Data!);

            await _service.AddTagToFileAsync(_repo, "a.md", "t");

            // Index exists but disk is empty: the record is stale.
            var stale = await _service.FindStaleRecordsAsync(_repo, Array.Empty<string>());
            Assert.True(stale.Success);
            Assert.Equal(new[] { "a.md" }, stale.Data!.ToArray());

            // Path comparison is case-insensitive, matching the index normalisation.
            var upper = await _service.FindStaleRecordsAsync(_repo, new[] { "A.MD" });
            Assert.Empty(upper.Data!);
        }

        [Fact]
        public async Task DatabaseExists_Reflects_Whether_Index_Was_Created()
        {
            // Lets the explorer decide to bootstrap a fresh repository.
            Assert.False(_service.DatabaseExists(_repo));
            Assert.False(_service.DatabaseExists(string.Empty));

            await _service.CreateDatabaseAsync(_repo);
            Assert.True(_service.DatabaseExists(_repo));
        }

        [Fact]
        public async Task EnsureFileIndexed_Creates_Database_And_Is_Idempotent()
        {
            var first = await _service.EnsureFileIndexedAsync(_repo, "a.md");
            Assert.True(first.Success);
            Assert.True(first.Data);

            var second = await _service.EnsureFileIndexedAsync(_repo, "a.md");
            Assert.True(second.Success);
            Assert.False(second.Data);

            var files = await _service.ListFilesWithTagsAsync(_repo);
            Assert.Single(files.Data!);
        }

        [Fact]
        public async Task AddTag_Auto_Indexes_Unindexed_File()
        {
            // No database, no scan — the editor just created this note.
            var result = await _service.AddTagToFileAsync(_repo, "fresh/note.md", "draft");
            Assert.True(result.Success);

            var entry = await _service.GetFileWithTagsAsync(_repo, "fresh/note.md");
            Assert.True(entry.Success);
            Assert.Contains("draft", entry.Data!.Tags);
        }

        [Fact]
        public async Task AddTag_Twice_On_Same_File_Is_Rejected()
        {
            await _service.AddTagToFileAsync(_repo, "a.md", "x");
            var again = await _service.AddTagToFileAsync(_repo, "a.md", "x");
            Assert.False(again.Success);
        }

        [Fact]
        public async Task RemoveTag_Reclaims_Orphaned_Tag()
        {
            await _service.AddTagToFileAsync(_repo, "a.md", "solo");
            var removed = await _service.RemoveTagFromFileAsync(_repo, "a.md", "solo");
            Assert.True(removed.Success);

            var tags = await _service.ListAllTagsAsync(_repo);
            Assert.Empty(tags.Data!);
        }

        [Fact]
        public async Task Rename_Moves_Index_Record_And_Keeps_Tags()
        {
            await _service.AddTagToFileAsync(_repo, "old.md", "keep");
            await _service.AddTagToFileAsync(_repo, "old.md", "second");

            var renamed = await _service.RenameFileAsync(_repo, "old.md", "sub/new.md");
            Assert.True(renamed.Success);

            var moved = await _service.GetFileWithTagsAsync(_repo, "sub/new.md");
            Assert.True(moved.Success);
            Assert.Equal(new[] { "keep", "second" }, moved.Data!.Tags.OrderBy(t => t).ToArray());

            var old = await _service.GetFileWithTagsAsync(_repo, "old.md");
            Assert.False(old.Success);
        }

        [Fact]
        public async Task Rename_Folds_A_Stale_Destination_Record()
        {
            await _service.AddTagToFileAsync(_repo, "a.md", "fromA");
            await _service.AddTagToFileAsync(_repo, "b.md", "fromB");

            var renamed = await _service.RenameFileAsync(_repo, "a.md", "b.md");
            Assert.True(renamed.Success);

            var files = await _service.ListFilesWithTagsAsync(_repo);
            Assert.Single(files.Data!);
            Assert.Equal("b.md", files.Data![0].FileName);
            Assert.Equal(new[] { "fromA" }, files.Data![0].Tags.ToArray());

            // "fromB" is no longer used by any file, so it should have been reclaimed.
            var tags = await _service.ListAllTagsAsync(_repo);
            Assert.DoesNotContain("fromB", tags.Data!.Select(t => t.TagName));
        }

        [Fact]
        public async Task DeleteFile_Cleans_Orphaned_Tags()
        {
            await _service.AddTagToFileAsync(_repo, "a.md", "gone");
            await _service.DeleteFileAsync(_repo, "a.md");

            var tags = await _service.ListAllTagsAsync(_repo);
            Assert.Empty(tags.Data!);
        }

        [Fact]
        public async Task SearchByTag_Returns_Matching_Files_With_Their_Full_Tag_Set()
        {
            await _service.AddTagToFileAsync(_repo, "one.md", "shared");
            await _service.AddTagToFileAsync(_repo, "one.md", "extra");
            await _service.AddTagToFileAsync(_repo, "two.md", "shared");
            await _service.AddTagToFileAsync(_repo, "three.md", "other");

            var found = await _service.SearchFilesByTagAsync(_repo, "shared");
            Assert.True(found.Success);
            Assert.Equal(2, found.Data!.Count);

            // A result row should show every tag on the file, not only the match.
            var one = found.Data!.First(f => f.FileName == "one.md");
            Assert.Equal(new[] { "extra", "shared" }, one.Tags.ToArray());
        }

        [Fact]
        public async Task GetTagsWithFileCount_Orders_By_Usage()
        {
            await _service.AddTagToFileAsync(_repo, "a.md", "rare");
            await _service.AddTagToFileAsync(_repo, "b.md", "common");
            await _service.AddTagToFileAsync(_repo, "c.md", "common");

            var tags = await _service.GetTagsWithFileCountAsync(_repo);
            Assert.True(tags.Success);
            Assert.Equal("common", tags.Data![0].TagName);
            Assert.Equal(2, tags.Data![0].FileCount);
            Assert.Equal("rare", tags.Data![1].TagName);
        }
    }
}
