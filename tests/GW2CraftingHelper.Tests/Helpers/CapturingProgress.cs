using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Tests.Helpers
{
    /// <summary>
    /// Synchronous IProgress implementation that captures all reports to a list.
    /// Unlike Progress{T}, this does not post through SynchronizationContext,
    /// making it reliable in unit test runners.
    /// </summary>
    internal class CapturingProgress<T> : IProgress<T>
    {
        private readonly List<T> _reports = new List<T>();

        public IReadOnlyList<T> Reports => _reports;

        public void Report(T value)
        {
            _reports.Add(value);
        }
    }
}
