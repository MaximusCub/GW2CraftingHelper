using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// One ItemMetadataService instance is shared by every plan generation in
    /// a module session (Module.Initialize constructs exactly one and hands it
    /// to both the pipeline and the plan view), and the generate path is
    /// re-entrant: the "Use Own Materials" checkbox stays clickable while a
    /// generation is in flight, and its confirm callback starts another one.
    /// So two GetMetadataAsync calls can be inside the same instance at once.
    /// <para>
    /// These are stress tests, not logic mirrors: they drive the real
    /// GetMetadataAsync over a thread-safe fake client and assert only the
    /// service's own published contract (every requested id that the API knows
    /// comes back, and a negative-cached id stays negative-cached). Against
    /// the unlocked Dictionary/HashSet these guarded, they fail - measured on
    /// 2026-08-25 before the lock landed, see the commit message.
    /// </para>
    /// </summary>
    public class ItemMetadataServiceConcurrencyTests
    {
        // 4 writers x 4,000 ids is 20 batches each of the service's 200-id
        // batch size, i.e. ~80 interleaved bulk-insert bursts into one
        // Dictionary - enough for an unsynchronized resize to lose entries or
        // throw, and still under a second when the writes are gated.
        private const int WriterCount = 4;
        private const int IdsPerWriter = 4000;

        [Fact]
        public async Task ConcurrentGetMetadata_over_disjoint_ids_returns_every_requested_item()
        {
            var api = new ConcurrentItemApiClient();
            var idsByWriter = new List<int[]>();
            for (int w = 0; w < WriterCount; w++)
            {
                var ids = new int[IdsPerWriter];
                for (int i = 0; i < IdsPerWriter; i++)
                {
                    int id = (w * IdsPerWriter) + i + 1;
                    ids[i] = id;
                    api.Add(id, $"Item {id}");
                }

                idsByWriter.Add(ids);
            }

            var service = new ItemMetadataService(api);
            var results = await RunConcurrently(
                idsByWriter, ids => service.GetMetadataAsync(ids, CancellationToken.None));

            for (int w = 0; w < WriterCount; w++)
            {
                var result = results[w];
                Assert.Equal(IdsPerWriter, result.Count);
                foreach (var id in idsByWriter[w])
                {
                    Assert.True(result.ContainsKey(id), $"writer {w} lost item {id}");
                    Assert.Equal($"Item {id}", result[id].Name);
                }
            }
        }

        [Fact]
        public async Task ConcurrentGetMetadata_over_shared_ids_agrees_on_every_item()
        {
            var api = new ConcurrentItemApiClient();
            var shared = new int[IdsPerWriter];
            for (int i = 0; i < IdsPerWriter; i++)
            {
                shared[i] = i + 1;
                api.Add(shared[i], $"Item {shared[i]}");
            }

            var idsByWriter = Enumerable.Range(0, WriterCount).Select(_ => shared).ToList();
            var service = new ItemMetadataService(api);
            var results = await RunConcurrently(
                idsByWriter, ids => service.GetMetadataAsync(ids, CancellationToken.None));

            foreach (var result in results)
            {
                Assert.Equal(IdsPerWriter, result.Count);
                foreach (var id in shared)
                {
                    Assert.True(result.ContainsKey(id), $"lost item {id}");
                }
            }
        }

        /// <summary>
        /// The negative cache is the other unlocked collection on this path:
        /// ids the API does not know are added to it by every caller at once,
        /// and a lost or torn add costs a doubled round trip forever after.
        /// </summary>
        [Fact]
        public async Task ConcurrentGetMetadata_over_missing_ids_negative_caches_them_once()
        {
            var api = new ConcurrentItemApiClient();
            var missing = new int[IdsPerWriter];
            for (int i = 0; i < IdsPerWriter; i++)
            {
                missing[i] = i + 1;
            }

            var idsByWriter = Enumerable.Range(0, WriterCount).Select(_ => missing).ToList();
            var service = new ItemMetadataService(api);
            var results = await RunConcurrently(
                idsByWriter, ids => service.GetMetadataAsync(ids, CancellationToken.None));

            foreach (var result in results)
            {
                Assert.Empty(result);
            }

            // Every id is negative-cached now, so a further call must not
            // reach the API at all. This is the observable half of
            // _knownMissing: a torn HashSet would keep re-fetching.
            int callsBefore = api.CallCount;
            var again = await service.GetMetadataAsync(missing, CancellationToken.None);
            Assert.Empty(again);
            Assert.Equal(callsBefore, api.CallCount);
        }

        private static async Task<List<IReadOnlyDictionary<int, ItemMetadata>>> RunConcurrently(
            IReadOnlyList<int[]> idsByWriter,
            Func<int[], Task<IReadOnlyDictionary<int, ItemMetadata>>> call)
        {
            // A barrier rather than a plain Task.WhenAll start: the fake API
            // completes synchronously, so without it the first writer can run
            // to completion before the last one is even scheduled and nothing
            // ever overlaps.
            using (var barrier = new Barrier(idsByWriter.Count))
            {
                var tasks = idsByWriter
                    .Select(ids => Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        return call(ids);
                    }))
                    .ToArray();

                var results = await Task.WhenAll(tasks);
                return results.ToList();
            }
        }

        /// <summary>
        /// Thread-safe stand-in for the live items endpoint.
        /// InMemoryItemApiClient records its calls into an unguarded List and
        /// so cannot be used from several threads at once - the point of this
        /// fixture is that only the SERVICE is under test for thread safety.
        /// </summary>
        private sealed class ConcurrentItemApiClient : IItemApiClient
        {
            private readonly object _gate = new object();
            private readonly Dictionary<int, RawItem> _items = new Dictionary<int, RawItem>();
            private int _callCount;

            public int CallCount => Volatile.Read(ref _callCount);

            public void Add(int id, string name)
            {
                lock (_gate)
                {
                    _items[id] = new RawItem { Id = id, Name = name, Icon = $"icon/{id}" };
                }
            }

            public Task<IReadOnlyList<RawItem>> GetItemsAsync(
                IReadOnlyList<int> itemIds, CancellationToken ct)
            {
                Interlocked.Increment(ref _callCount);

                var results = new List<RawItem>(itemIds.Count);
                lock (_gate)
                {
                    foreach (var id in itemIds)
                    {
                        if (_items.TryGetValue(id, out var item))
                        {
                            results.Add(item);
                        }
                    }
                }

                return Task.FromResult<IReadOnlyList<RawItem>>(results);
            }
        }
    }
}
