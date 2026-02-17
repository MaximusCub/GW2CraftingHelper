using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    public static class BoundedConcurrency
    {
        public static async Task ForEachAsync<T>(
            IEnumerable<T> items,
            int maxConcurrency,
            Func<T, Task> action,
            CancellationToken ct)
        {
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
