namespace TaimisToolbench.Services
{
    /// <summary>
    /// Result of SnapshotFailureClassifier.Classify: the coarse
    /// SnapshotFailureKind plus the raw source counts a caller needs to
    /// render a specific message (e.g. "2 of 5 sources") without re-parsing
    /// the original exception itself.
    /// </summary>
    internal class SnapshotFailureClassification
    {
        public SnapshotFailureKind Kind { get; }

        public int FailedSourceCount { get; }

        public int TotalSourceCount { get; }

        public SnapshotFailureClassification(SnapshotFailureKind kind, int failedSourceCount, int totalSourceCount)
        {
            Kind = kind;
            FailedSourceCount = failedSourceCount;
            TotalSourceCount = totalSourceCount;
        }
    }
}
