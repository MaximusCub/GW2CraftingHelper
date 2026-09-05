using System;

namespace VendorOfferUpdater
{
    public class QueryOptions
    {
        /// <summary>
        /// Delay used when no run has set one. Named so that a client method
        /// reached without a QueryVendorItemsAsync call first still throttles.
        /// </summary>
        public const int DefaultDelayBetweenRequestsMs = 250;

        /// <summary>
        /// Attempts one ask gets before its section is recorded unresolved.
        /// Counts the first try, so 5 means the first try plus four retries.
        /// </summary>
        public const int DefaultMaxAttempts = 5;

        public int MaxPrefixDepth { get; init; } = 2;

        public int MaxTotalRequests { get; init; } = 2000;

        public TimeSpan MaxRuntime { get; init; } = TimeSpan.FromMinutes(30);

        public int DelayBetweenRequestsMs { get; init; } = DefaultDelayBetweenRequestsMs;

        public int MaxAttempts { get; init; } = DefaultMaxAttempts;

        /// <summary>
        /// First step of the exponential backoff between attempts. The HTTP
        /// 403 cooldown is 30 times this, since a 403 from this wiki is a
        /// temporary block on the address rather than one refused query.
        /// </summary>
        public int RetryBackoffBaseMs { get; init; } = 1000;

        /// <summary>
        /// How many sections in a row may go unanswered before the run stops
        /// asking. One refused section is worth carrying on past; several in
        /// a row means the wiki has stopped answering this address, and every
        /// further section would spend its whole attempt ladder finding that
        /// out again. Reset by any section that answers.
        /// </summary>
        public int MaxConsecutiveUnresolvedSections { get; init; } = 3;

        public bool DryRun { get; init; }
    }
}
