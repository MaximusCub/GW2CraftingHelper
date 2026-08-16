using System.Collections.Generic;
using Blish_HUD.Settings;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class ModuleSettings
    {
        public SettingEntry<int> ModalDialogX { get; private set; }
        public SettingEntry<int> ModalDialogY { get; private set; }

        // User-provided coin valuations for non-coin currencies (karma,
        // laurels, ...), stored as JSON (currencyId -> copper per unit).
        // SettingCollection has no built-in support for a CurrencyValuation
        // object, so it is persisted as a raw string; CurrencyValuationSerializer
        // (Blish-free) does the actual conversion so that logic is unit-testable.
        public SettingEntry<string> CurrencyValuationsJson { get; private set; }

        // gw2efficiency-style "value own materials" (M28; M34-B2a #3
        // upgraded this from a display-only opportunity-cost tweak into a
        // real force-buy pre-pass - see OwnedMaterialsForceBuyPrePass):
        // when enabled, a node is force-excluded from crafting whenever
        // buying it outright costs less than 85% of its own components'
        // fresh buy cost (gw2efficiency's getCheaperToBuyItemIds), and the
        // plan's profit figure is reduced by owned materials' sell
        // opportunity cost. Default TRUE to match gw2efficiency's own
        // "valueOwnItems" default (m34-r2-gw2e-owned-materials.md Section
        // 3.1: valueOwnItems defaults true whenever the separate "use own
        // materials" master toggle - CraftingPlanView's own checkbox,
        // default off - is on). This setting's force-buy effect only
        // applies when an account snapshot is actually driving reduction
        // (CraftingPlanPipeline's own, deliberately narrower gate - see its
        // Step 6.5 comment) - with no snapshot it stays fully inert, same
        // as the profit-display opportunity-cost figure it also drives.
        // Now surfaced as a checkbox in the Settings tab (see
        // SettingsTabContent) - previously flip-only-via-JSON like
        // ScrollDiagnosticsEnabled below.
        public SettingEntry<bool> ValueOwnMaterials { get; private set; }

        // M37 (KNOWN-ISSUES #24, gw2e parity): per-material Homestead
        // Refinement efficiency tier (0/1/2), echoing gw2efficiency's own
        // per-output-material userEfficiencyTiers setting exactly - three
        // independent settings, not one combined toggle, matching gw2e's
        // own three-material shape (docs/research/m37-r1-homestead.md
        // Section 1.2). Default 0 for all three: gw2e's own hardcoded
        // default AND its own no-API-key fallback, and matches this repo's
        // "no invented data" posture better than assuming any upgrade
        // level. Deliberately NO master "do you even own Homestead" gate -
        // gw2e has none either; see KNOWN-ISSUES.md item 24 for that
        // recorded, deferred divergence option.
        public SettingEntry<int> HomesteadFiberTier { get; private set; }
        public SettingEntry<int> HomesteadMetalTier { get; private set; }
        public SettingEntry<int> HomesteadWoodTier { get; private set; }

        // M33 C1 (#12 diagnostics): gates the scroll-machinery diagnostic
        // logging in CraftingPlanView (wheel events, restore/guard writes
        // and state transitions). Default false; instrumentation only -
        // never changes scroll/guard/restore behavior.
        // M39 (log system): SUBSUMED by LogDiagnosticsEnabled below per the
        // tab-roadmap-proposal synthesis (Section 2.1) - the Settings tab
        // now ships exactly ONE diagnostics checkbox (LogDiagnosticsEnabled),
        // not two. This setting is kept defined (not removed) purely for
        // backward compatibility with any already-persisted value: renaming
        // the key outright would silently drop a hand-set true for existing
        // users, whereas CraftingPlanView.ScrollDiagEnabled now reads BOTH
        // this and LogDiagnosticsEnabled (a plain bool OR - trivially cheap,
        // no extra I/O) so an old persisted true still gates the
        // [scrolldiag] channel exactly as before. No UI checkbox for this
        // one; new users only ever see LogDiagnosticsEnabled.
        public SettingEntry<bool> ScrollDiagnosticsEnabled { get; private set; }

        // M39 (log system, d2-log-system.md Section 5): size cap for the
        // module log file (data/module_log.jsonl), in bytes. Default 2 MB.
        // Checked on every ModuleLog write (self-trimming) - see
        // ModuleLogStore.AppendLine.
        public SettingEntry<int> LogMaxSizeBytes { get; private set; }

        // M39 (log system, d2-log-system.md Section 5): age-based retention
        // for the module log file, in days. Default 14. Enforced once per
        // session at Module.LoadAsync - see ModuleLogStore.PruneOlderThan.
        public SettingEntry<int> LogRetentionDays { get; private set; }

        // M39 (log system, d2-log-system.md Section 5/tab-roadmap-proposal
        // Section 2.1): the ONE diagnostics toggle for the whole module -
        // subsumes ScrollDiagnosticsEnabled above and additionally gates
        // whether Debug-level ModuleLog entries reach the file sink (they
        // always still land in the in-memory ring regardless - see
        // ModuleLog's own policy). Default false, matching
        // ScrollDiagnosticsEnabled's own prior default. Has a real Settings
        // tab checkbox (idiom (a), immediate-apply, no Save button - see
        // SettingsTabContent).
        public SettingEntry<bool> LogDiagnosticsEnabled { get; private set; }

        // M39 (d1-snapshot-about-settings.md Feature 3): replaces Module.cs's
        // previously-hardcoded `StaleThreshold` constant. Default 10 minutes
        // (matching the constant it replaces), clamped 1-120. Read directly
        // by Module.Update()'s own staleness check via
        // GetClampedSnapshotRefreshIntervalMinutes below - a hand-edited
        // settings file with an out-of-range value must clamp, never crash
        // or disable the auto-refresh gate (same contract as
        // GetClampedLogMaxSizeBytes/GetClampedLogRetentionDays above).
        public SettingEntry<int> SnapshotRefreshIntervalMinutes { get; private set; }

        public ModuleSettings(SettingCollection settings)
        {
            ModalDialogX = settings.DefineSetting(
                "ModalDialogX", -1,
                () => "Modal Dialog X",
                () => "Horizontal position of the modal dialog");

            ModalDialogY = settings.DefineSetting(
                "ModalDialogY", -1,
                () => "Modal Dialog Y",
                () => "Vertical position of the modal dialog");

            CurrencyValuationsJson = settings.DefineSetting(
                "CurrencyValuationsJson", string.Empty,
                () => "Currency Valuations",
                () => "User-provided coin values for non-coin currencies (JSON)");

            ValueOwnMaterials = settings.DefineSetting(
                "ValueOwnMaterials", true,
                () => "Value own materials",
                () => "Force-buy items where buying beats crafting from fresh components by more than 15%, and value owned materials at their sell opportunity cost instead of treating them as free");

            HomesteadFiberTier = settings.DefineSetting(
                "HomesteadFiberTier", 0,
                () => "Homestead Fiber efficiency tier",
                () => "Farm refinement efficiency upgrades owned (0, 1, or 2)");

            HomesteadMetalTier = settings.DefineSetting(
                "HomesteadMetalTier", 0,
                () => "Homestead Metal efficiency tier",
                () => "Metal Forge refinement efficiency upgrades owned (0, 1, or 2)");

            HomesteadWoodTier = settings.DefineSetting(
                "HomesteadWoodTier", 0,
                () => "Homestead Wood efficiency tier",
                () => "Lumber Mill refinement efficiency upgrades owned (0, 1, or 2)");

            ScrollDiagnosticsEnabled = settings.DefineSetting(
                "ScrollDiagnosticsEnabled", false,
                () => "Scroll diagnostics",
                () => "Log scroll machinery events for debugging");

            LogMaxSizeBytes = settings.DefineSetting(
                "LogMaxSizeBytes", 2 * 1024 * 1024,
                () => "Log max size (bytes)",
                () => "Maximum size of the module log file on disk before old entries are trimmed");

            LogRetentionDays = settings.DefineSetting(
                "LogRetentionDays", 14,
                () => "Log retention (days)",
                () => "Number of days of module log history to keep on disk");

            LogDiagnosticsEnabled = settings.DefineSetting(
                "LogDiagnosticsEnabled", false,
                () => "Diagnostics logging",
                () => "Log fine-grained diagnostic events (including scroll machinery) to the Log tab and file");

            SnapshotRefreshIntervalMinutes = settings.DefineSetting(
                "SnapshotRefreshIntervalMinutes", 10,
                () => "Snapshot refresh interval (minutes)",
                () => "How long a cached account snapshot may sit before an automatic background refresh is triggered");
        }

        /// <summary>
        /// Reads the persisted Homestead Refinement efficiency tiers.
        /// Values outside 0-2 (possible only via a hand-edited settings
        /// file, since the Settings tab and SettingsInputParser reject
        /// them) are clamped rather than thrown - a corrupt/out-of-range
        /// persisted value must never crash plan generation. See
        /// HomesteadEfficiencyTiers' own constructor for why clamping
        /// happens here rather than there: that constructor fails loudly by
        /// design for a directly-constructed caller.
        /// </summary>
        public HomesteadEfficiencyTiers GetHomesteadEfficiencyTiers()
        {
            var map = new Dictionary<int, int>
            {
                { Gw2Constants.RefinedHomesteadFiberItemId, ClampTier(HomesteadFiberTier.Value) },
                { Gw2Constants.RefinedHomesteadMetalItemId, ClampTier(HomesteadMetalTier.Value) },
                { Gw2Constants.RefinedHomesteadWoodItemId, ClampTier(HomesteadWoodTier.Value) }
            };
            return new HomesteadEfficiencyTiers(map);
        }

        private static int ClampTier(int tier)
        {
            if (tier < 0) return 0;
            if (tier > 2) return 2;
            return tier;
        }

        // Mirrors SettingsInputParser.TryParseLogMaxSizeMb's own 1-1000 MB
        // bound (same deliberate duplication as ClampTier/TryParseTier's
        // shared 0-2 range above, and for the same reason). A persisted
        // value outside this range is reachable only via a hand-edited
        // settings.json - the Settings tab's own parser rejects it before
        // it is ever assigned - but ModuleLogStore.AppendLine's self-trim
        // check is `if (maxSizeBytes > 0)`: a persisted 0 or negative value
        // would silently disable the size cap for the whole session, which
        // is the exact "endless crap on disk" outcome this feature exists
        // to prevent.
        private const int MinLogMaxSizeBytes = 1 * 1024 * 1024;
        private const int MaxLogMaxSizeBytes = 1000 * 1024 * 1024;

        private static int ClampLogMaxSizeBytes(int maxSizeBytes)
        {
            if (maxSizeBytes < MinLogMaxSizeBytes) return MinLogMaxSizeBytes;
            if (maxSizeBytes > MaxLogMaxSizeBytes) return MaxLogMaxSizeBytes;
            return maxSizeBytes;
        }

        // Mirrors SettingsInputParser.TryParseRetentionDays's own 1-365 day
        // bound - see ClampLogMaxSizeBytes' own comment for why the
        // duplication is deliberate and why a persisted value must never
        // bypass this. ModuleLogStore.PruneOlderThan's own no-op guard is
        // `if (retentionDays <= 0) return;`, so a persisted 0/negative
        // value would silently disable age-based retention entirely.
        private const int MinLogRetentionDays = 1;
        private const int MaxLogRetentionDays = 365;

        private static int ClampRetentionDays(int retentionDays)
        {
            if (retentionDays < MinLogRetentionDays) return MinLogRetentionDays;
            if (retentionDays > MaxLogRetentionDays) return MaxLogRetentionDays;
            return retentionDays;
        }

        /// <summary>
        /// Clamped LogMaxSizeBytes for actual use - see
        /// ClampLogMaxSizeBytes' own comment. Callers (Module.cs's
        /// Configure call, and SettingsTabContent's live-push after a save)
        /// should always read this instead of LogMaxSizeBytes.Value
        /// directly, the same way GetHomesteadEfficiencyTiers already
        /// clamps rather than exposing HomesteadFiberTier.Value raw.
        /// </summary>
        public int GetClampedLogMaxSizeBytes()
        {
            return ClampLogMaxSizeBytes(LogMaxSizeBytes.Value);
        }

        /// <summary>
        /// Clamped LogRetentionDays for actual use - see
        /// ClampRetentionDays' own comment.
        /// </summary>
        public int GetClampedLogRetentionDays()
        {
            return ClampRetentionDays(LogRetentionDays.Value);
        }

        // Mirrors SettingsInputParser.TryParseRefreshIntervalMinutes' own
        // 1-120 minute bound - see ClampLogMaxSizeBytes' own comment above
        // for why the duplication is deliberate. Module.Update()'s
        // staleness check must never see a persisted 0/negative value (that
        // would make every tick immediately "stale", defeating the point of
        // the backoff/throttling already in place around
        // RefreshSnapshotInBackgroundAsync) or an absurdly large one (that
        // would silently disable auto-refresh for a hand-edited settings
        // file).
        private const int MinSnapshotRefreshIntervalMinutes = 1;
        private const int MaxSnapshotRefreshIntervalMinutes = 120;

        private static int ClampSnapshotRefreshIntervalMinutes(int minutes)
        {
            if (minutes < MinSnapshotRefreshIntervalMinutes) return MinSnapshotRefreshIntervalMinutes;
            if (minutes > MaxSnapshotRefreshIntervalMinutes) return MaxSnapshotRefreshIntervalMinutes;
            return minutes;
        }

        /// <summary>
        /// Clamped SnapshotRefreshIntervalMinutes for actual use - see
        /// ClampSnapshotRefreshIntervalMinutes' own comment. Module.Update()
        /// should always read this instead of
        /// SnapshotRefreshIntervalMinutes.Value directly.
        /// </summary>
        public int GetClampedSnapshotRefreshIntervalMinutes()
        {
            return ClampSnapshotRefreshIntervalMinutes(SnapshotRefreshIntervalMinutes.Value);
        }

        /// <summary>
        /// Reads the RAW persisted currency valuations - user-set overrides
        /// and explicit clears only, with no CurrencyDecisionDefaults
        /// default folded in. Returns CurrencyValuation.None when nothing
        /// has been configured or the stored value cannot be parsed. Used
        /// by the Settings tab (SettingsTabContent), which must be able to
        /// tell "the user typed this" apart from "this is just the curated
        /// default" - see GetEffectiveCurrencyValuation for the solver-
        /// facing counterpart that DOES fold defaults in.
        /// </summary>
        public CurrencyValuation GetCurrencyValuation()
        {
            return CurrencyValuationSerializer.Deserialize(CurrencyValuationsJson.Value);
        }

        /// <summary>
        /// currency-ux-package (Feature 1): the solver-facing counterpart
        /// of GetCurrencyValuation - same raw persisted overrides/clears,
        /// PLUS every CurrencyDecisionDefaults entry that is neither
        /// explicitly overridden nor explicitly cleared, via
        /// CurrencyValuation.TryGetEffectiveCopperValue (the one place the
        /// three-state precedence is implemented). This is the ONLY
        /// production call site that should ever see defaults applied -
        /// Module.cs is this method's sole caller, threading the result
        /// into CraftingPlanPipeline.GenerateStructuredAsync. Every other
        /// consumer of a CurrencyValuation (a directly-constructed test
        /// instance, or GetCurrencyValuation's raw read above) sees only
        /// what was actually persisted, by design - defaults are applied
        /// exactly once, here, rather than inside the solver itself, so a
        /// bare PlanSolver.Solve/CraftingPlanPipeline call with an
        /// explicit CurrencyValuation (as most of this repo's solver tests
        /// make) is never silently reshaped by a curated default it never
        /// asked for.
        /// </summary>
        public CurrencyValuation GetEffectiveCurrencyValuation()
        {
            // currency-ux-package review fix (finding 5, MEASURED): the
            // merge itself now lives on CurrencyValuation.WithDefaults (a
            // Blish-free Models type, therefore unit-testable) instead of
            // being inlined here - this class stays the sole production
            // caller, unchanged in every other respect.
            return CurrencyValuation.WithDefaults(GetCurrencyValuation());
        }

        /// <summary>
        /// Persists the given currency valuations.
        /// </summary>
        public void SetCurrencyValuation(CurrencyValuation valuation)
        {
            CurrencyValuationsJson.Value = CurrencyValuationSerializer.Serialize(valuation);
        }

        /// <summary>
        /// Maps the ValueOwnMaterials toggle onto the pipeline's
        /// OwnMaterialsMode enum. Defaults to Valued (see ValueOwnMaterials'
        /// own doc comment for why).
        /// </summary>
        public OwnMaterialsMode GetOwnMaterialsMode()
        {
            return ValueOwnMaterials.Value ? OwnMaterialsMode.Valued : OwnMaterialsMode.Free;
        }

        public void ResetToDefaults()
        {
            ModalDialogX.Value = -1;
            ModalDialogY.Value = -1;
            CurrencyValuationsJson.Value = string.Empty;
            ValueOwnMaterials.Value = true;
            HomesteadFiberTier.Value = 0;
            HomesteadMetalTier.Value = 0;
            HomesteadWoodTier.Value = 0;
            ScrollDiagnosticsEnabled.Value = false;
            LogMaxSizeBytes.Value = 2 * 1024 * 1024;
            LogRetentionDays.Value = 14;
            LogDiagnosticsEnabled.Value = false;
            SnapshotRefreshIntervalMinutes.Value = 10;
        }
    }
}
