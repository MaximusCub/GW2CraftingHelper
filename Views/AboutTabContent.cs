using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Views.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// The About tab: static,
    /// mostly-derived information about the module itself - name, version,
    /// author/contributors, source URL, the Blish HUD version it targets,
    /// this repo's own license, a Blish HUD MIT-license credit line, and
    /// the module's data directory (useful when a user needs to attach
    /// snapshot.json/status.json/etc. to a bug report). Same shape as
    /// LogTabContent.cs: one FlowPanel(CanScroll),
    /// Build(Container) populates it once, no relayout registry
    /// - nothing here is interactive beyond plain
    /// selectable/copyable text, so there is nothing to keep "sticky"
    /// across tab revisits (see MainView.cs's own cross-cutting note on
    /// rebuild-per-visit).
    /// <para>
    /// Name/version/author/url/description/dependencies are read live from
    /// ModuleParameters.Manifest - the exact same object Blish HUD itself
    /// already parsed and validated in order to even load this module, so
    /// under normal operation this read cannot fail - with a defensive
    /// fallback to hand-parsing the packaged manifest.json if it ever does
    /// (null Manifest, an unexpectedly blank Name, or any exception).
    /// Mirrors the try/catch-with-graceful-fallback shape already used four
    /// times in Module.Initialize() for seed files.
    /// </para>
    /// <para>
    /// Two of Manifest's own properties (Version, and a dependency's
    /// VersionRange) are typed as SemVer.Version/SemVer.Range from the
    /// external "SemVer" NuGet package that Blish HUD embeds via Costura at
    /// runtime - this project has no compile-time reference to that
    /// package, so those two fields are read via reflection (ToString()
    /// only) instead of a direct property access, to avoid adding a new
    /// package reference for a two-field, display-only read.
    /// </para>
    /// </summary>
    public class AboutTabContent
    {
        private static readonly Logger Logger = Logger.GetLogger<AboutTabContent>();

        private const string ModuleDisplayName = "GW2 Crafting Helper";
        // The ONE phrasing for a value the module could not resolve.
        // Three lived here - "unknown" for a version, "Not set in
        // manifest.json" for the source URL, "Not listed in manifest.json"
        // for the author - so one screen answered the same question three
        // ways, and two of the three named an implementation detail the
        // reader has no way to act on.
        private const string NotAvailableText = "Not available";
        private const string BlishHudDependencyNamespace = "bh.blishhud";

        // The Blish HUD MIT-license credit line, verified against
        // Blish HUD's own repo. Kept as its own constant (not folded into the
        // "Built with:" row) because "Built with:" reports the live
        // SemVer.Range this module targets - a distinct, manifest-derived
        // value - while this is fixed attribution text.
        private const string BlishHudCreditLine = "Built on Blish HUD (MIT License) - github.com/blish-hud/Blish-HUD";

        // The GW2/ArenaNet fan-content disclaimer. The maintainer
        // approved this exact wording - ship the literal string as-is, do
        // not derive or reword it.
        private const string ArenaNetDisclaimerText =
            "GW2 Crafting Helper is a fan-made tool and is not affiliated with, endorsed by, or supported by ArenaNet or NCSOFT. Guild Wars 2 and all associated trademarks are the property of NCSOFT Corporation. All game data comes from the official Guild Wars 2 API.";

        // The gw2efficiency design-reference credit. Like
        // ArenaNetDisclaimerText, the maintainer
        // approved this exact wording for the "Licenses & Attributions"
        // section. Ship this literal string as-is - do not derive it from
        // other constants or otherwise reword it, since the approval covers
        // this exact text (including the Patreon/PayPal URLs, which render
        // as plain text - no hyperlink control exists in this file).
        private const string Gw2EfficiencyCreditText =
            "The crafting logic in this module - how it weighs craft versus buy, prices materials, values Mystic Clovers, and models vendor purchases - is built to follow the publicly observable approach of gw2efficiency (gw2efficiency.com), the Guild Wars 2 companion site created by David Reess (queicherius), Saskia Van Leeuwen, and Ecmel Tugcu, with help from their open-source contributors. Where gw2efficiency has published its methods as open-source code, such as the MIT-licensed recipe-calculation and recipe-nesting libraries, those served as a valuable design reference; this module ships its own independent implementation and never calls gw2efficiency at runtime. gw2efficiency does the hard, ongoing work of keeping tools like this accurate and free for the whole community, so if this module has saved you time or gold, please consider supporting the original team via Patreon (https://www.patreon.com/gw2efficiency) or PayPal (https://paypal.me/devoxa). We are grateful for the trail they blazed.";

        // Manual fallback for the "Built with Blish HUD" note, only ever
        // shown if BOTH the live Dependencies read (ReadBlishHudDependencyRange)
        // and the manifest.json fallback read fail to produce a value -
        // mirrors manifest.json's own currently-declared
        // dependencies.bh.blishhud value (d1 Feature 2, option (a): a
        // doc-only literal, same maintenance cost as any other
        // rarely-changing constant).
        private const string FallbackBlishHudVersionRange = ">=1.3.0";

        private static readonly Color InfoTextColor = new Color(170, 170, 170);

        // Every horizontal constant on this tab comes from AboutLayoutMath,
        // which derives them from the plan tables' own pinned-right-edge
        // rule and from the reading measure it declares.
        private const int Inset = AboutLayoutMath.AboutInset;
        private const int RowHeight = 30;
        private const int RowLabelY = 7;
        private const int RowInputY = 3;
        private const int InputHeight = 26;

        // 22, not 20: a wrapped line sits at y=2 and its lowest Font16 ink
        // is y=23.
        private const int ProseLineHeight = 22;

        /// <summary>Gap between one block on the board and the next.</summary>
        private const int BlockGap = 20;

        // The module name sits at the Display tier now, so the band holds a
        // 36px line box: the title at y=2 puts its lowest ink at y=39, and
        // the rule at HeaderRowHeight - 3 clears it by two.
        private const int HeaderRowHeight = 44;
        private const int HeaderTitleY = 2;

        // The ramp's section-title band, named once in PlanContentHeightMath
        // and aliased here rather than re-derived.
        private const int SectionHeaderRowHeight = PlanContentHeightMath.SectionHeaderRowHeight;
        private const int SectionHeaderTitleY = PlanContentHeightMath.SectionHeaderTitleY;
        private const int IconSize = 32;
        private const int IconToNameGap = 10;
        private const int NameToVersionGap = 8;

        private static readonly Color SectionDividerColor = new Color(130, 130, 130);

        private readonly ModuleParameters _moduleParameters;
        private readonly string _dataDirectoryPath;
        private readonly Texture2D _moduleIconTexture;

        private FlowPanel _rootPanel;

        // One absolutely-placed panel inside the scroller: two columns
        // cannot be expressed by a top-to-bottom FlowPanel, and every block
        // on it has to be re-placed when the width changes.
        private Panel _documentPanel;

        private Panel _headerPanel;
        private Image _iconImage;
        private Label _nameLabel;
        private Label _versionLabel;
        private Panel _headerRule;

        /// <summary>One label/value row of the identity card.</summary>
        private sealed class FactRow
        {
            public Panel Panel;
            public Label LabelControl;
            public string LabelText;
            public Label ValueLabel;
            public string ValueText;
            public TextBox ValueBox;
        }

        /// <summary>A titled block of prose: the 38px band every other
        /// SectionTitle in the module draws, its 2px rule, and one wrapped
        /// paragraph.</summary>
        private sealed class ProseBlock
        {
            public Panel Panel;
            public Label TitleLabel;
            public Panel Rule;
            public Label Body;
            public string BodyText;
        }

        private readonly List<FactRow> _factRows = new List<FactRow>();
        private readonly List<ProseBlock> _proseBlocks = new List<ProseBlock>();
        private ProseBlock _factsBlock;
        private Label _descriptionLabel;
        private string _descriptionText = "";

        // Width the blocks below are currently placed at. Survives Build,
        // which is why Build lays out through ApplyLayout rather than the
        // guarded Relayout - see Relayout.
        private int _panelWidth;

        // Holds the wrap/ellipsize half of a resize until the drag stops -
        // see Relayout.
        private readonly ResizeSettleDebounce _resizeSettle;

        // False while Build is midway through replacing the blocks below.
        // Module keeps ONE AboutTabContent and Blish re-runs Build on it at
        // every tab open, off the UI thread, while the settle callback is
        // marshalled onto the main thread - so without this, opening the
        // tab inside a settle window would run ApplyLayout against blocks
        // Build has just nulled. Volatile so the reader that sees true also
        // sees the finished blocks; same gate SettingsTabContent uses, for
        // the same reason.
        private volatile bool _buildComplete;

        /// <summary>
        /// Clears the built flag on the MAIN thread, before Blish queues
        /// the off-thread Build. Clearing it inside Build leaves the flag
        /// reading true for the whole interval between the tab switch and
        /// Build's first statement, and a settle callback landing in that
        /// window dereferences the blocks Build is about to replace.
        /// Mirrors SettingsTabContent.BeginRebuild.
        /// </summary>
        public void BeginRebuild()
        {
            _buildComplete = false;
        }

        public AboutTabContent(ModuleParameters moduleParameters, string dataDirectoryPath, Texture2D moduleIconTexture)
        {
            _moduleParameters = moduleParameters ?? throw new ArgumentNullException(nameof(moduleParameters));
            _dataDirectoryPath = dataDirectoryPath ?? "";
            _moduleIconTexture = moduleIconTexture;

            _resizeSettle = new ResizeSettleDebounce(
                RefitTextAfterResizeSettle,
                MainThreadMarshal.Run,
                ResizeSettleDebounce.DefaultSettleMs,
                ex =>
                {
                    Logger.Warn(ex, "About text re-fit wait failed");
                    ModuleLog.Shared.Write(ModuleLogLevel.Warn, "about",
                        $"About text re-fit wait failed: {ex.GetType().Name} - {ex.Message}");
                });
        }

        public void Build(Container container)
        {
            _buildComplete = false;

            var info = LoadAboutInfo();

            _factRows.Clear();
            _proseBlocks.Clear();
            _descriptionLabel = null;
            _factsBlock = null;

            _rootPanel = new FlowPanel()
            {
                Size = new Point(container.ContentRegion.Width, container.ContentRegion.Height),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = container,
            };

            _documentPanel = new Panel()
            {
                Size = new Point(ContentWidth(container), 0),
                Parent = _rootPanel,
            };

            BuildHeader(info);

            _descriptionText = info.Description ?? "";
            if (!string.IsNullOrWhiteSpace(_descriptionText))
            {
                _descriptionLabel = CreateProseLabel(_documentPanel);
            }

            _factsBlock = CreateProseBlock("Module", null);

            // Trailing colons dropped from all six: inside a two-column
            // table with a rule, a colon on every label is punctuation doing
            // a column's job.
            AddFactRow(AboutLayoutMath.SourceLabel, string.IsNullOrWhiteSpace(info.Url) ? NotAvailableText : info.Url, copyable: true);
            AddFactRow(AboutLayoutMath.AuthorLabel, info.AuthorDisplay ?? NotAvailableText);
            AddFactRow(AboutLayoutMath.BuiltWithLabel, $"Blish HUD {info.BlishVersionRange ?? FallbackBlishHudVersionRange}");

            // "License" (this project's own license) and the Blish HUD
            // credit are two separate, differently-sourced rows and are
            // deliberately kept side by side rather than merged: "License"
            // is this repo's own MIT license, while "Credits" is d1's
            // already-verified Blish HUD MIT-license credit line, carried
            // over unchanged. Do not collapse these into one row or drop
            // either without updating this comment.
            AddFactRow(AboutLayoutMath.LicenseLabel, "MIT (see LICENSE in the repo)");
            AddFactRow(AboutLayoutMath.CreditsLabel, BlishHudCreditLine, copyable: true);
            AddFactRow(AboutLayoutMath.DataDirectoryLabel, string.IsNullOrWhiteSpace(_dataDirectoryPath) ? NotAvailableText : _dataDirectoryPath, copyable: true);

            CreateProseBlock("Disclaimer", ArenaNetDisclaimerText);
            CreateProseBlock("gw2efficiency", Gw2EfficiencyCreditText);

            ApplyLayout(ContentWidth(container), measureText: true);

            // The tab used to resize its root panel and nothing else, so a
            // window widened after the tab was opened left the prose wrapped
            // at whatever width it opened at, permanently. Both paths go
            // through Relayout so they cannot drift.
            container.Resized += (_, __) =>
            {
                _rootPanel.Size = new Point(
                    container.ContentRegion.Width,
                    container.ContentRegion.Height);
                Relayout(ContentWidth(container));
            };

            _buildComplete = true;
        }

        /// <summary>
        /// Releases what outlives this tab's control tree. Called from
        /// Module.Unload; safe when the tab was never opened, and safe
        /// twice. Mirrors SettingsTabContent.Teardown.
        /// </summary>
        public void Teardown()
        {
            _resizeSettle.Cancel();
        }

        private static int ContentWidth(Container container)
        {
            int width = container.ContentRegion.Width - WindowSizing.ScrollbarAllowance;
            return width > 0 ? width : 0;
        }

        /// <summary>
        /// The plan header's own pair, reused verbatim: the module name at
        /// Display 32 with its version at SmallHeading 20 regular beside it,
        /// baseline-aligned. That pair is what SmallHeading exists for; the
        /// tab's title was sitting one tier low at SectionTitle.
        /// </summary>
        private void BuildHeader(AboutInfo info)
        {
            _headerPanel = new Panel()
            {
                Size = new Point(AboutLayoutMath.FactsMinWidth, HeaderRowHeight),
                Parent = _documentPanel,
            };

            if (_moduleIconTexture != null)
            {
                // Unframed on purpose: this is a logo, not an item, and the
                // framed item-icon path is for items.
                _iconImage = new Image()
                {
                    Texture = new AsyncTexture2D(_moduleIconTexture),
                    Size = new Point(IconSize, IconSize),
                    Location = new Point(Inset, (HeaderRowHeight - IconSize) / 2),
                    Parent = _headerPanel,
                };
            }

            _nameLabel = new Label()
            {
                Text = info.Name,
                Font = UiFonts.Display,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(Inset, HeaderTitleY),
                Parent = _headerPanel,
            };

            _versionLabel = new Label()
            {
                Text = "v" + info.Version,
                Font = UiFonts.SmallHeading,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(Inset, HeaderTitleY),
                Parent = _headerPanel,
            };

            _headerRule = new Panel()
            {
                Size = new Point(AboutLayoutMath.FactsMinWidth, 2),
                Location = new Point(0, HeaderRowHeight - 3),
                BackgroundColor = SectionDividerColor,
                Parent = _headerPanel,
            };
        }

        private ProseBlock CreateProseBlock(string title, string body)
        {
            var block = new ProseBlock
            {
                BodyText = body,
                Panel = new Panel()
                {
                    Size = new Point(AboutLayoutMath.FactsMinWidth, SectionHeaderRowHeight),
                    Parent = _documentPanel,
                },
            };

            block.TitleLabel = new Label()
            {
                Text = title,
                Font = UiFonts.SectionTitle,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(Inset, SectionHeaderTitleY),
                Parent = block.Panel,
            };

            // These two headings were the only SectionTitle bands in the
            // module drawing no rule.
            block.Rule = new Panel()
            {
                Size = new Point(AboutLayoutMath.FactsMinWidth, 2),
                Location = new Point(0, SectionHeaderRowHeight - 3),
                BackgroundColor = SectionDividerColor,
                Parent = block.Panel,
            };

            if (body != null)
            {
                block.Body = CreateProseLabel(block.Panel);
                _proseBlocks.Add(block);
            }

            return block;
        }

        private static Label CreateProseLabel(Panel parent)
        {
            return new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                AutoSizeWidth = false,
                AutoSizeHeight = false,
                TextColor = InfoTextColor,
                Parent = parent,
            };
        }

        private void AddFactRow(string label, string value, bool copyable = false)
        {
            var row = new FactRow
            {
                LabelText = label,
                ValueText = value,
                Panel = new Panel()
                {
                    Size = new Point(AboutLayoutMath.FactsMinWidth, RowHeight),
                    Parent = _documentPanel,
                },
            };

            row.LabelControl = new Label()
            {
                Font = UiFonts.Body,
                Text = label,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(Inset, RowLabelY),
                Parent = row.Panel,
            };

            if (copyable)
            {
                // Plain TextBox, not a click-to-launch-browser button (d1
                // Feature 2: no precedent anywhere in this codebase, and no
                // confirmed-safe way to launch an external process from
                // inside the GW2 overlay sandbox). TextBox natively
                // supports select-all/copy (TextInputBase.HandleCopy), so
                // this is already "selectable/copyable" with no extra
                // control needed - the field is never read back or
                // persisted, so a user editing it in-place is harmless and
                // resets on the next tab visit anyway.
                row.ValueBox = new TextBox()
                {
                    Text = value,
                    Size = new Point(AboutLayoutMath.ValueFloor, InputHeight),
                    Location = new Point(Inset, RowInputY),
                    Parent = row.Panel,
                }.ReleaseOnDispose().ReleaseOnEnter();
            }
            else
            {
                row.ValueLabel = new Label()
                {
                    Font = UiFonts.Body,
                    Text = value,
                    AutoSizeWidth = false,
                    AutoSizeHeight = true,
                    TextColor = InfoTextColor,
                    Location = new Point(Inset, RowLabelY),
                    Parent = row.Panel,
                };
            }

            _factRows.Add(row);
        }

        /// <summary>
        /// The RESIZE entry point: a no-op when the width has not actually
        /// moved (Resized fires on height-only changes too, and repeatedly
        /// while the window is dragged). Positions track the drag; the
        /// wrapping and ellipsizing wait for it to stop, the module's
        /// standing split for text measurement on a resize path.
        /// <para>
        /// Build must NOT come through here. Module reuses one
        /// AboutTabContent instance across every tab open and Blish re-runs
        /// Build on each of them, so the freshly-created blocks of the
        /// second open would be measured against the width the first open
        /// left in <see cref="_panelWidth"/>, this guard would short-circuit,
        /// and every block would stay stacked at (0, 0) inside a
        /// zero-height panel - a blank tab. Build calls
        /// <see cref="ApplyLayout"/> directly instead, which is also what
        /// SettingsTabContent does.
        /// </para>
        /// </summary>
        private void Relayout(int panelWidth)
        {
            if (panelWidth == _panelWidth)
            {
                return;
            }

            ApplyLayout(panelWidth, measureText: false);
            _resizeSettle.Schedule();
        }

        /// <summary>
        /// The trailing half of a resize. Skipped while a rebuild is in
        /// flight, whose own Build pass measures everything anyway - see
        /// <see cref="_buildComplete"/>.
        /// </summary>
        private void RefitTextAfterResizeSettle()
        {
            if (!_buildComplete || _panelWidth <= 0)
            {
                return;
            }

            ApplyLayout(_panelWidth, measureText: true);
        }

        /// <summary>
        /// Places every block at the given panel width: the header band
        /// across the top, the identity card in the left column, the two
        /// prose blocks in the right one - stacked into one column below
        /// AboutLayoutMath.TwoColumnThreshold, with the text still capped at
        /// the reading measure either way.
        /// </summary>
        private void ApplyLayout(int panelWidth, bool measureText)
        {
            if (_documentPanel == null || panelWidth <= 0)
            {
                return;
            }

            _panelWidth = panelWidth;
            _documentPanel.Width = panelWidth;

            int columnCount = AboutLayoutMath.ColumnCount(panelWidth);
            int columnWidth = AboutLayoutMath.ColumnWidth(panelWidth);
            int rightX = columnCount == 1 ? 0 : AboutLayoutMath.SecondColumnX(panelWidth);

            int y = LayoutHeader(panelWidth);

            int leftY = y;
            int rightY = columnCount == 1 ? 0 : y;

            if (_descriptionLabel != null)
            {
                leftY += LayoutProse(
                    _descriptionLabel, _descriptionText, 0, leftY, columnWidth, measureText) + BlockGap;
            }

            leftY = LayoutFactsCard(0, leftY, columnWidth, measureText);

            if (columnCount == 1)
            {
                rightY = leftY + BlockGap;
            }

            for (int i = 0; i < _proseBlocks.Count; i++)
            {
                if (i > 0)
                {
                    rightY += BlockGap;
                }

                rightY = LayoutProseBlock(_proseBlocks[i], rightX, rightY, columnWidth, measureText);
            }

            _documentPanel.Height = (leftY > rightY ? leftY : rightY) + BlockGap;
        }

        private int LayoutHeader(int panelWidth)
        {
            _headerPanel.Location = new Point(0, 0);
            _headerPanel.Size = new Point(panelWidth, HeaderRowHeight);
            _headerRule.Size = new Point(panelWidth, 2);

            int nameX = _iconImage == null ? Inset : Inset + IconSize + IconToNameGap;
            _nameLabel.Location = new Point(nameX, HeaderTitleY);

            int nameWidth = (int)Math.Ceiling(UiFonts.Display.MeasureString(_nameLabel.Text ?? "").Width);

            // Same baseline as the title beside it, not the same top: the
            // two tiers have different line boxes.
            int baseline = HeaderTitleY + TypeRampMetrics.Regular32.BaselineY;
            _versionLabel.Location = new Point(
                nameX + nameWidth + NameToVersionGap,
                TypeRampMetrics.BaselineAlignedY(TypeRampMetrics.Regular20, baseline));

            return HeaderRowHeight + BlockGap;
        }

        private int LayoutFactsCard(int x, int y, int columnWidth, bool measureText)
        {
            y = LayoutProseBlock(_factsBlock, x, y, columnWidth, measureText) + TitleToContentGap;

            int labelBand = AboutLayoutMath.LabelFloor;
            var font = UiFonts.Body;
            foreach (var row in _factRows)
            {
                int width = (int)Math.Ceiling(font.MeasureString(row.LabelText).Width);
                if (width > labelBand)
                {
                    labelBand = width;
                }
            }

            int valueX = AboutLayoutMath.ValueX(labelBand);
            foreach (var row in _factRows)
            {
                row.Panel.Location = new Point(x, y);
                row.Panel.Size = new Point(columnWidth, RowHeight);

                if (row.ValueBox != null)
                {
                    row.ValueBox.Location = new Point(valueX, RowInputY);
                    row.ValueBox.Width = AboutLayoutMath.CopyBoxWidth(columnWidth, labelBand);
                }
                else
                {
                    int budget = AboutLayoutMath.ValueMaxWidth(columnWidth, labelBand);
                    row.ValueLabel.Location = new Point(valueX, RowLabelY);
                    row.ValueLabel.Width = budget;
                    if (!measureText)
                    {
                        y += RowHeight;
                        continue;
                    }

                    string shown = LabelHelpers.EllipsizeToWidth(font, row.ValueText, budget);
                    if (!string.Equals(row.ValueLabel.Text, shown, StringComparison.Ordinal))
                    {
                        row.ValueLabel.Text = shown;
                    }

                    string full = string.Equals(shown, row.ValueText, StringComparison.Ordinal)
                        ? null
                        : row.ValueText;
                    TooltipFacility.ApplyPlain(row.ValueLabel, full);
                    TooltipFacility.ApplyPlain(row.Panel, full);
                }

                y += RowHeight;
            }

            return y;
        }

        private int LayoutProseBlock(ProseBlock block, int x, int y, int columnWidth, bool measureText)
        {
            block.Panel.Location = new Point(x, y);
            block.Rule.Size = new Point(columnWidth, 2);

            int height = SectionHeaderRowHeight;
            if (block.Body != null)
            {
                height += TitleToContentGap
                    + LayoutProse(
                        block.Body, block.BodyText, 0,
                        SectionHeaderRowHeight + TitleToContentGap, columnWidth, measureText);
            }

            block.Panel.Size = new Point(columnWidth, height);
            return y + height;
        }

        // Gap between a section's title band and its first content row -
        // the same 6 the Settings board uses, aliased so the two tabs'
        // section rhythm cannot drift.
        private const int TitleToContentGap = SettingsFormLayout.TitleToContentGap;

        /// <summary>
        /// Wraps one paragraph into an already-created label and returns the
        /// height it took: one <see cref="ProseLineHeight"/> row per physical
        /// line, capped at the reading measure however wide the column is.
        /// </summary>
        private static int LayoutProse(
            Label label, string text, int x, int y, int columnWidth, bool measureText)
        {
            int budget = AboutLayoutMath.TextBudget(columnWidth);

            // At measureText false the paragraph keeps the wrap it already
            // has and only its box moves; the label's own Height is the
            // cache, written explicitly here and never auto-sized.
            if (measureText)
            {
                var wrapped = TextWrapMath.Wrap(
                    text, budget, budget, LabelHelpers.MeasureWith(UiFonts.Body));
                string joined = string.Join("\n", wrapped.Lines);

                if (!string.Equals(label.Text, joined, StringComparison.Ordinal))
                {
                    label.Text = joined;
                }

                label.Size = new Point(budget, wrapped.Lines.Count * ProseLineHeight);

                TooltipFacility.ApplyPlain(label, wrapped.Truncated ? text : null);
            }
            else
            {
                label.Width = budget;
            }

            label.Location = new Point(x + Inset, y);
            return label.Height;
        }

        private class AboutInfo
        {
            public string Name;
            public string Version;
            public string Description;
            public string Url;
            public string AuthorDisplay;
            public string BlishVersionRange;
        }

        // CS0649 (never assigned) is wrong about the two DTOs below: every field
        // is written by reflection, by the JsonConvert.DeserializeObject call in
        // ReadFromManifestJsonFallback, which the compiler cannot see.
#pragma warning disable CS0649
        private class ManifestFallbackContributorDto
        {
            [JsonProperty("name")]
            public string Name;
        }

        private class ManifestFallbackDto
        {
            [JsonProperty("name")]
            public string Name;

            [JsonProperty("version")]
            public string Version;

            [JsonProperty("description")]
            public string Description;

            [JsonProperty("url")]
            public string Url;

            [JsonProperty("author")]
            public ManifestFallbackContributorDto Author;

            [JsonProperty("contributors")]
            public List<ManifestFallbackContributorDto> Contributors;

            [JsonProperty("dependencies")]
            public Dictionary<string, string> Dependencies;
        }
#pragma warning restore CS0649

        private AboutInfo LoadAboutInfo()
        {
            return TryReadFromLiveManifest() ?? ReadFromManifestJsonFallback();
        }

        private AboutInfo TryReadFromLiveManifest()
        {
            try
            {
                var manifest = _moduleParameters.Manifest;
                if (manifest == null)
                {
                    return null;
                }

                string name = manifest.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                return new AboutInfo
                {
                    Name = name,
                    Version = ReadVersionText(manifest) ?? NotAvailableText,
                    Description = manifest.Description ?? "",
                    Url = manifest.Url ?? "",
                    AuthorDisplay = ResolveAuthorDisplay(manifest.Author, manifest.Contributors),
                    BlishVersionRange = ReadBlishHudDependencyRange(manifest.Dependencies),
                };
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to read the live module manifest for the About tab, falling back to manifest.json");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "about", $"Failed to read the live module manifest for the About tab, falling back to manifest.json: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        // manifest.Version is a SemVer.Version (external package, see class
        // doc comment) - read via reflection so this project never needs a
        // compile-time reference to it. ToString() on that type is what
        // Blish HUD's own Manifest.GetDetailedName() uses to render a
        // version, so this matches Blish's own display convention.
        private static string ReadVersionText(Manifest manifest)
        {
            try
            {
                var versionProperty = manifest.GetType().GetProperty("Version");
                object versionValue = versionProperty?.GetValue(manifest);
                string text = versionValue?.ToString();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (Exception ex)
            {
                Logger.Debug("Could not read the live manifest's Version via reflection: {0}", ex.Message);
                return null;
            }
        }

        private static string ResolveAuthorDisplay(ModuleContributor author, List<ModuleContributor> contributors)
        {
            if (author != null && !string.IsNullOrWhiteSpace(author.Name))
            {
                return author.Name;
            }

            if (contributors == null || contributors.Count == 0)
            {
                return null;
            }

            var names = contributors
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => c.Name)
                .ToList();

            return names.Count == 0 ? null : string.Join(", ", names);
        }

        // A dependency's VersionRange is a SemVer.Range (external package,
        // see class doc comment) - read via reflection for the same reason
        // as ReadVersionText above. ModuleDependency.IsBlishHud itself is a
        // plain bool and safe to call directly.
        private static string ReadBlishHudDependencyRange(List<ModuleDependency> dependencies)
        {
            if (dependencies == null)
            {
                return null;
            }

            foreach (var dependency in dependencies)
            {
                if (dependency == null || !dependency.IsBlishHud)
                {
                    continue;
                }

                try
                {
                    var rangeProperty = dependency.GetType().GetProperty("VersionRange");
                    object rangeValue = rangeProperty?.GetValue(dependency);
                    string text = rangeValue?.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug("Could not read a live manifest dependency's VersionRange via reflection: {0}", ex.Message);
                }
            }

            return null;
        }

        private AboutInfo ReadFromManifestJsonFallback()
        {
            try
            {
                using (var stream = TryOpenManifestJsonFallbackStream())
                {
                    if (stream == null)
                    {
                        return FallbackDefaultInfo();
                    }

                    using (var reader = new StreamReader(stream))
                    {
                        string json = reader.ReadToEnd();
                        var dto = JsonConvert.DeserializeObject<ManifestFallbackDto>(json);
                        if (dto == null)
                        {
                            return FallbackDefaultInfo();
                        }

                        string authorDisplay = null;
                        if (dto.Author != null && !string.IsNullOrWhiteSpace(dto.Author.Name))
                        {
                            authorDisplay = dto.Author.Name;
                        }
                        else if (dto.Contributors != null && dto.Contributors.Count > 0)
                        {
                            var names = dto.Contributors
                                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Name))
                                .Select(c => c.Name)
                                .ToList();
                            if (names.Count > 0)
                            {
                                authorDisplay = string.Join(", ", names);
                            }
                        }

                        string blishRange = null;
                        if (dto.Dependencies != null &&
                            dto.Dependencies.TryGetValue(BlishHudDependencyNamespace, out string range) &&
                            !string.IsNullOrWhiteSpace(range))
                        {
                            blishRange = range;
                        }

                        return new AboutInfo
                        {
                            Name = string.IsNullOrWhiteSpace(dto.Name) ? ModuleDisplayName : dto.Name,
                            Version = string.IsNullOrWhiteSpace(dto.Version) ? NotAvailableText : dto.Version,
                            Description = dto.Description ?? "",
                            Url = dto.Url ?? "",
                            AuthorDisplay = authorDisplay,
                            BlishVersionRange = blishRange,
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to read the manifest.json fallback for the About tab");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "about", $"Failed to read the manifest.json fallback for the About tab: {ex.GetType().Name} - {ex.Message}");
                return FallbackDefaultInfo();
            }
        }

        /// <summary>
        /// Locates the packaged manifest.json next to this module's own
        /// loaded assembly. Deliberately NOT
        /// ContentsManager.GetFileStream("manifest.json") - ContentsManager
        /// is rooted at the module package's "ref" subfolder (Blish HUD's
        /// own ContentsManager.GetModuleInstance calls
        /// module.DataReader.GetSubPath("ref")), but BlishHUD.targets'
        /// BuildBlishHUDModule target copies manifest.json to the package
        /// ROOT alongside the compiled module DLL, never into ref/ - so
        /// ContentsManager can never see it there. Reading next to
        /// Assembly.GetExecutingAssembly().Location matches how the
        /// package is actually laid out on disk.
        /// </summary>
        private static Stream TryOpenManifestJsonFallbackStream()
        {
            try
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrWhiteSpace(assemblyLocation))
                {
                    return null;
                }

                string directory = Path.GetDirectoryName(assemblyLocation);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return null;
                }

                string manifestPath = Path.Combine(directory, "manifest.json");
                return File.Exists(manifestPath) ? File.OpenRead(manifestPath) : null;
            }
            catch (Exception ex)
            {
                Logger.Debug("Could not locate the packaged manifest.json next to the module assembly: {0}", ex.Message);
                return null;
            }
        }

        private static AboutInfo FallbackDefaultInfo()
        {
            return new AboutInfo
            {
                Name = ModuleDisplayName,
                Version = NotAvailableText,
                Description = "",
                Url = "",
                AuthorDisplay = null,
                BlishVersionRange = null,
            };
        }
    }
}
