using System;
using System.IO;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{

    public class StatusStoreTests : IDisposable
    {

        private readonly string _tempDir;
        private readonly StatusStore _store;

        public StatusStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GW2CraftingHelper_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _store = new StatusStore(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Fact]
        public void Load_NoFile_ReturnsEmpty()
        {
            Assert.Equal("", _store.Load());
        }

        [Fact]
        public void Save_Null_Load_ReturnsEmpty()
        {
            _store.Save(null);
            Assert.Equal("", _store.Load());
        }

        [Fact]
        public void Save_Load_RoundTrips()
        {
            _store.Save("Updated \u2014 1:00 PM");
            Assert.Equal("Updated \u2014 1:00 PM", _store.Load());
        }

        [Fact]
        public void Save_Overwrite_ReturnsLatest()
        {
            _store.Save("First");
            _store.Save("Second");
            Assert.Equal("Second", _store.Load());
        }
    }

}
