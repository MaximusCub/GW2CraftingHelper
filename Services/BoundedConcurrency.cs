using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    internal static class BoundedConcurrency
    {
        public static async Task ForEachAsync<T>(
            IEnumerable<T> items,
            int maxConcurrency,
            Func<T, Task> action,
            CancellationToken ct)
        {
            // A zero or negative bound builds a semaphore no Release can
            // open: every task parks on WaitAsync and Task.WhenAll never
            // completes. In the overlay that is a plan generation that spins
            // behind a progress bar forever, so it fails loudly here.
            if (maxConcurrency < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
            }

            var materialized = items.ToList();
            if (materialized.Count == 0)
            {
                return;
            }

            using (var semaphore = new SemaphoreSlim(maxConcurrency))
            {
                var tasks = materialized.Select(async item =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        await action(item);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToList();

                await Task.WhenAll(tasks);
            }
        }
    }
}
