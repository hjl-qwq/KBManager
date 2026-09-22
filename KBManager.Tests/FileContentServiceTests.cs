using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using KBManager.core;
using Xunit;

namespace KBManager.Tests
{
    /// <summary>
    /// Tests for the file read/write service that backs the built-in editor,
    /// with particular attention to path-traversal safety and to preserving a
    /// file's original encoding/line-ending conventions.
    /// </summary>
    public class FileContentServiceTests : IDisposable
    {
        private readonly string _repo;
        private readonly FileContentService _service = new();

        public FileContentServiceTests()
        {
            _repo = Path.Combine(Path.GetTempPath(), "kbm_fc_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_repo);
        }

        public void Dispose()
        {
            if (Directory.Exists(_repo)) Directory.Delete(_repo, true);
        }

        // ---- path safety ------------------------------------------------------

        [Theory]
        [InlineData("/etc/passwd")]
        [InlineData("C:/Windows/system32/config")]
        [InlineData("../outside.md")]
        [InlineData("a/../../outside.md")]
        [InlineData("..")]
        [InlineData("")]
        [InlineData("   ")]
        public void NormalizeRelativePath_Rejects_Unsafe_Input(string input)
        {
            var result = _service.NormalizeRelativePath(input);
            Assert.False(result.Success);
        }

        [Theory]
        [InlineData("notes/a.md", "notes/a.md")]
        [InlineData("./notes/a.md", "notes/a.md")]
        [InlineData("notes//a.md", "notes/a.md")]
        [InlineData(@"notes\a.md", "notes/a.md")]
        [InlineData("notes/./sub/../a.md", "notes/a.md")]
        public void NormalizeRelativePath_Canonicalises(string input, string expected)
        {
            var result = _service.NormalizeRelativePath(input);
            Assert.True(result.Success);
            Assert.Equal(expected, result.Data);
        }

        [Fact]
        public void ResolveFullPath_Keeps_Paths_Inside_Repository()
        {
            var ok = _service.ResolveFullPath(_repo, "notes/a.md");
            Assert.True(ok.Success);
            Assert.StartsWith(Path.GetFullPath(_repo), ok.Data!);

            // A sibling directory that merely shares a name prefix must not pass.
            var escape = _service.ResolveFullPath(_repo, "../" + Path.GetFileName(_repo) + "_evil/x.md");
            Assert.False(escape.Success);
        }

        [Fact]
        public async Task ReadAsync_Refuses_To_Escape_Repository()
        {
            var secret = Path.Combine(Path.GetDirectoryName(_repo)!, "secret_" + Guid.NewGuid().ToString("N")[..6] + ".md");
            await File.WriteAllTextAsync(secret, "top secret");
            try
            {
                var result = await _service.ReadTextAsync(_repo, "../" + Path.GetFileName(secret));
                Assert.False(result.Success);
            }
            finally
            {
                File.Delete(secret);
            }
        }

        // ---- create / read / write round trip ---------------------------------

        [Fact]
        public async Task Create_Then_Read_Returns_Initial_Content()
        {
            var created = await _service.CreateFileAsync(_repo, "notes/new.md", "# Hello\n");
            Assert.True(created.Success);
            Assert.True(File.Exists(Path.Combine(_repo, "notes", "new.md")));

            var read = await _service.ReadTextAsync(_repo, "notes/new.md");
            Assert.True(read.Success);
            Assert.Equal("# Hello\n", read.Data!.Content);
            Assert.False(read.Data.HasUtf8Bom);
        }

        [Fact]
        public async Task Create_Fails_When_File_Exists_Unless_Overwriting()
        {
            await _service.CreateFileAsync(_repo, "a.md", "one");
            var again = await _service.CreateFileAsync(_repo, "a.md", "two");
            Assert.False(again.Success);

            var forced = await _service.CreateFileAsync(_repo, "a.md", "two", overwrite: true);
            Assert.True(forced.Success);
            var read = await _service.ReadTextAsync(_repo, "a.md");
            Assert.Equal("two", read.Data!.Content);
        }

        [Fact]
        public async Task Write_RoundTrips_Content()
        {
            await _service.CreateFileAsync(_repo, "doc.md", "start");
            var written = await _service.WriteTextAsync(_repo, "doc.md", "line1\nline2\n", lineEnding: "\n");
            Assert.True(written.Success);

            var read = await _service.ReadTextAsync(_repo, "doc.md");
            Assert.Equal("line1\nline2\n", read.Data!.Content);
        }

        [Fact]
        public async Task Read_Detects_And_Write_Preserves_Crlf()
        {
            var path = Path.Combine(_repo, "crlf.md");
            await File.WriteAllTextAsync(path, "a\r\nb\r\n", new UTF8Encoding(false));

            var read = await _service.ReadTextAsync(_repo, "crlf.md");
            Assert.True(read.Success);
            Assert.Equal("\r\n", read.Data!.LineEnding);

            // Editor buffers normally hold "\n"; saving must restore CRLF.
            await _service.WriteTextAsync(_repo, "crlf.md", "a\nb\nc\n", read.Data.LineEnding, read.Data.HasUtf8Bom);

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal("a\r\nb\r\nc\r\n", Encoding.UTF8.GetString(bytes));
        }

        [Fact]
        public async Task Read_Detects_And_Write_Preserves_Utf8Bom()
        {
            var path = Path.Combine(_repo, "bom.md");
            await File.WriteAllTextAsync(path, "héllo", new UTF8Encoding(true));

            var read = await _service.ReadTextAsync(_repo, "bom.md");
            Assert.True(read.Success);
            Assert.True(read.Data!.HasUtf8Bom);

            await _service.WriteTextAsync(_repo, "bom.md", "héllo2", read.Data.LineEnding, read.Data.HasUtf8Bom);

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.Equal("héllo2", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        }

        [Fact]
        public async Task Write_Does_Not_Leave_Temp_File_Behind()
        {
            await _service.CreateFileAsync(_repo, "t.md", "x");
            await _service.WriteTextAsync(_repo, "t.md", "y");
            Assert.Empty(Directory.GetFiles(_repo, "*.kbtmp", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task Read_Missing_File_Fails()
        {
            var read = await _service.ReadTextAsync(_repo, "nope.md");
            Assert.False(read.Success);
        }

        // ---- delete / rename --------------------------------------------------

        [Fact]
        public async Task Delete_Removes_File()
        {
            await _service.CreateFileAsync(_repo, "gone.md", "x");
            var deleted = await _service.DeleteFileAsync(_repo, "gone.md");
            Assert.True(deleted.Success);
            Assert.False(File.Exists(Path.Combine(_repo, "gone.md")));

            var again = await _service.DeleteFileAsync(_repo, "gone.md");
            Assert.False(again.Success);
        }

        [Fact]
        public async Task Rename_Moves_File_And_Creates_Destination_Directory()
        {
            await _service.CreateFileAsync(_repo, "old.md", "content");
            var renamed = await _service.RenameFileAsync(_repo, "old.md", "deep/nested/new.md");
            Assert.True(renamed.Success);
            Assert.Equal("deep/nested/new.md", renamed.Data);

            Assert.False(File.Exists(Path.Combine(_repo, "old.md")));
            var read = await _service.ReadTextAsync(_repo, "deep/nested/new.md");
            Assert.Equal("content", read.Data!.Content);
        }

        [Fact]
        public async Task Rename_Refuses_To_Overwrite_By_Default()
        {
            await _service.CreateFileAsync(_repo, "a.md", "A");
            await _service.CreateFileAsync(_repo, "b.md", "B");

            var blocked = await _service.RenameFileAsync(_repo, "a.md", "b.md");
            Assert.False(blocked.Success);

            var read = await _service.ReadTextAsync(_repo, "b.md");
            Assert.Equal("B", read.Data!.Content);
        }

        [Fact]
        public async Task Exists_Reflects_Disk_State()
        {
            Assert.False(_service.Exists(_repo, "x.md"));
            await _service.CreateFileAsync(_repo, "x.md", "");
            Assert.True(_service.Exists(_repo, "x.md"));
        }
    }
}
