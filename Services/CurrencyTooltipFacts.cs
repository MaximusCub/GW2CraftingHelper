namespace TaimisToolbench.Services
{
    /// <summary>
    /// Everything the game's own currency tooltip shows, in the four
    /// fields it shows them in. The currency-space twin of
    /// <see cref="ItemTooltipIdentity"/> plus the two body facts a
    /// currency has and an item does not: what the wallet holds, and the
    /// /v2/currencies prose.
    ///
    /// <para>
    /// It is a separate type from the item identity ON PURPOSE. Currency
    /// ids and item ids are different id spaces that collide numerically -
    /// id 24 is both a real item and the currency "Pristine Fractal
    /// Relics", and Gaeting Crystal exists as item 86094 AND as wallet
    /// currencies 39 and 77 - so a tooltip has to be built from the kind
    /// the CALLER knows its id to be. Two types is what makes that a
    /// compile-time choice instead of a name lookup.
    /// </para>
    /// </summary>
    internal readonly struct CurrencyTooltipFacts
    {
        private readonly string _name;
        private readonly string _iconUrl;
        private readonly string _description;
        private readonly int? _walletQuantity;

        private CurrencyTooltipFacts(
            string name, string iconUrl, string description, int? walletQuantity)
        {
            _name = name;
            _iconUrl = iconUrl;
            _description = description;
            _walletQuantity = walletQuantity;
        }

        internal string Name
        {
            get { return _name; }
        }

        internal string IconUrl
        {
            get { return _iconUrl; }
        }

        /// <summary>
        /// The /v2/currencies <c>description</c> for this currency, or
        /// null/empty when the session never fetched one. Never invented:
        /// an absent description drops the paragraph rather than
        /// substituting prose of the module's own.
        /// </summary>
        internal string Description
        {
            get { return _description; }
        }

        /// <summary>
        /// The account's wallet holding. Null when no wallet snapshot was
        /// read at all, which is a different statement from a holding of
        /// zero and drops the line rather than claiming the player has
        /// none.
        /// </summary>
        internal int? WalletQuantity
        {
            get { return _walletQuantity; }
        }

        /// <summary>Whether there is a subject to head the tooltip with.</summary>
        internal bool HasSubject
        {
            get { return !string.IsNullOrEmpty(_name); }
        }

        internal static CurrencyTooltipFacts For(
            string name, string iconUrl, string description, int? walletQuantity)
        {
            return new CurrencyTooltipFacts(name, iconUrl, description, walletQuantity);
        }
    }
}
