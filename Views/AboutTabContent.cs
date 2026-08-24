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
        // reader has no way to act on (audit batch J, L7).
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

        private const int RightEdgePadding = 20;
        private const int RowHeight = 30;
        // 22, not 20: AddLabeledInfoSection puts a heading at y=2 in a panel
        // of exactly this height, and that heading's lowest Font16 ink is
        // y=23. AddInfoLine's own rows already size themselves to their
        // wrapped label and only use this as a floor.
        private const int InfoRowHeight = 22;
        private const int InfoLineBottomPadding = 4;
        private const int SpacerHeight = 10;
        private const int HeaderRowHeight = 44;
        private const int NameColumnX = 16;
        private const int NameColumnWidth = 150;
        private const int ValueColumnX = NameColumnX + NameColumnWidth;
        private const int IconSize = 32;
        private const int MinValueBoxWidth = 200;

        private readonly ModuleParameters _moduleParameters;
        private readonly string _dataDirectoryPath;
        private readonly Texture2D _moduleIconTexture;

        private FlowPanel _rootPanel;

        public AboutTabContent(ModuleParameters moduleParameters, string dataDirectoryPath, Texture2D moduleIconTexture)
        {
            _moduleParameters = moduleParameters ?? throw new ArgumentNullException(nameof(moduleParameters));
            _dataDirectoryPath = dataDirectoryPath ?? "";
            _moduleIconTexture = moduleIconTexture;
        }

        public void Build(Container container)
        {
            var info = LoadAboutInfo();

            int panelWidth = container.ContentRegion.Width - RightEdgePadding;

            _rootPanel = new FlowPanel()
            {
                Size = new Point(container.ContentRegion.Width, container.ContentRegion.Height),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = container
            };

            container.Resized += (_, __) =>
            {
                _rootPanel.Size = new Point(
                    container.ContentRegion.Width,
                    container.ContentRegion.Height);
            };

            AddHeaderRow(info, panelWidth);

            if (!string.IsNullOrWhiteSpace(info.Description))
            {
                AddInfoLine(info.Description, panelWidth);
            }

            AddSpacer(panelWidth);

            AddValueRow("Source:", string.IsNullOrWhiteSpace(info.Url) ? NotAvailableText : info.Url, panelWidth, copyable: true);
            AddValueRow("Author:", info.AuthorDisplay ?? NotAvailableText, panelWidth);
            AddValueRow("Built with:", $"Blish HUD {info.BlishVersionRange ?? FallbackBlishHudVersionRange}", panelWidth);

            // "License:" (this project's own license) and "Credits:" (the
            // Blish HUD attribution d1 Feature 2 asked for) are two
            // separate, differently-sourced rows and are deliberately kept
            // side by side rather than merged: "License:" is this repo's
            // own MIT license (not present in d1's original wireframe -
            // added here so a reader always sees which license applies to
            // this module's own code), while "Credits:" is d1's
            // already-verified Blish HUD MIT-license credit line, carried
            // over unchanged. Do not collapse these into one row or drop
            // either without updating this comment.
            AddValueRow("License:", "MIT (see LICENSE in the repo)", panelWidth);
            AddValueRow("Credits:", BlishHudCreditLine, panelWidth, copyable: true);
            AddValueRow("Data directory:", string.IsNullOrWhiteSpace(_dataDirectoryPath) ? NotAvailableText : _dataDirectoryPath, panelWidth, copyable: true);

            AddSpacer(panelWidth);
            AddLabeledInfoSection("Disclaimer:", ArenaNetDisclaimerText, panelWidth);

            AddSpacer(panelWidth);
            AddLabeledInfoSection("Credits: gw2efficiency", Gw2EfficiencyCreditText, panelWidth);
        }

        private void AddHeaderRow(AboutInfo info, int panelWidth)
        {
            var headerPanel = new Panel()
            {
                Size = new Point(panelWidth, HeaderRowHeight),
                Parent = _rootPanel
            };

            int nameX = NameColumnX;
            if (_moduleIconTexture != null)
            {
                new Image()
                {
                    Texture = new AsyncTexture2D(_moduleIconTexture),
                    Size = new Point(IconSize, IconSize),
                    Location = new Point(NameColumnX, (HeaderRowHeight - IconSize) / 2),
                    Parent = headerPanel
                };
                nameX = NameColumnX + IconSize + 10;
            }

            new Label()
            {
                Text = $"{info.Name} v{info.Version}",
                Font = UiFonts.Title,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(nameX, 10),
                Parent = headerPanel
            };
        }

        // The manifest "description" can run well past a single ~800px-wide
        // line at this tab's default font (unlike the short string this
        // method was originally sized for), so it needs to wrap. The wrap
        // is done ourselves via Blish HUD's own DrawUtil.WrapText (rather
        // than the Label control's own WrapText property) because Label's
        // wrap width is pinned from whatever Size the control already has
        // at its very first internal layout pass - a pass that fires as
        // soon as any AutoSize flag is applied in an object initializer,
        // which happens before a later Width assignment in that same
        // initializer would ever take effect. Pre-wrapping with embedded
        // "\n"s and letting the Label AutoSizeWidth/AutoSizeHeight to the
        // already-wrapped result sidesteps that ordering trap entirely.
        // The row panel is then sized to the label's resulting (possibly
        // multi-line) height so wrapped text is never clipped and later
        // rows are pushed down instead of overlapping it.
        private void AddInfoLine(string text, int panelWidth)
        {
            var font = UiFonts.Body;
            int maxTextWidth = Math.Max(1, panelWidth - NameColumnX - RightEdgePadding);
            string wrappedText = DrawUtil.WrapText(font, text, maxTextWidth);

            var label = new Label()
            {
                Text = wrappedText,
                Font = font,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(NameColumnX, 2)
            };

            int rowHeight = Math.Max(InfoRowHeight, label.Height + InfoLineBottomPadding);

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, rowHeight),
                Parent = _rootPanel
            };

            label.Parent = rowPanel;
        }

        // A short row-name label (same Location/style as AddValueRow's
        // name labels) followed immediately by an AddInfoLine wrapped
        // paragraph, for multi-line body text that still needs a "Name:"
        // heading - unlike the top-of-tab Description line (AddInfoLine
        // called directly, no heading needed since it is self-evidently
        // the module's own description) or AddValueRow's rows (single-line
        // values only, never wrapped).
        private void AddLabeledInfoSection(string label, string text, int panelWidth)
        {
            var labelPanel = new Panel()
            {
                Size = new Point(panelWidth, InfoRowHeight),
                Parent = _rootPanel
            };

            new Label()
            {
                Font = UiFonts.Body,
                Text = label,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(NameColumnX, 2),
                Parent = labelPanel
            };

            AddInfoLine(text, panelWidth);
        }

        private void AddSpacer(int panelWidth)
        {
            new Panel()
            {
                Size = new Point(panelWidth, SpacerHeight),
                Parent = _rootPanel
            };
        }

        private void AddValueRow(string label, string value, int panelWidth, bool copyable = false)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };

            new Label()
            {
                Font = UiFonts.Body,
                Text = label,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
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
                int width = Math.Max(MinValueBoxWidth, panelWidth - ValueColumnX - RightEdgePadding);
                new TextBox()
                {
                    Text = value,
                    Size = new Point(width, 26),
                    Location = new Point(ValueColumnX, 3),
                    Parent = rowPanel
                };
            }
            else
            {
                new Label()
                {
                    Font = UiFonts.Body,
                    Text = value,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    TextColor = InfoTextColor,
                    Location = new Point(ValueColumnX, 7),
                    Parent = rowPanel
                };
            }
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
                    BlishVersionRange = ReadBlishHudDependencyRange(manifest.Dependencies)
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
                            BlishVersionRange = blishRange
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
                BlishVersionRange = null
            };
        }
    }
}
