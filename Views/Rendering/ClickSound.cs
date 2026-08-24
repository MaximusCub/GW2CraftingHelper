using Blish_HUD;
using Blish_HUD.Content;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework.Audio;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// Plays the module's UI click at a volume the user controls, instead
    /// of at the volume Blish picks.
    /// <para>
    /// Measured playback path, from the vendored Blish HUD 1.3.0 and
    /// MonoGame 3.8.0.1641 binaries (ilspycmd).
    /// <c>ContentService.PlaySoundEffectByName</c> ends in
    /// <c>SoundEffect.FromStream(...).Play(GameService.GameIntegration.
    /// Audio.Volume, 0f, 0f)</c>, and that Volume is
    /// <c>AudioIntegration.GetVolume()</c>: a 20-sample rolling average of
    /// the GAME's own output peak meter, clamped to a hard ceiling of
    /// <c>0.4</c>, or - when "use game volume" is off - Blish's own Volume
    /// setting, which itself has a 0.4 range cap and a 0.2 default. The
    /// single highest-leverage input for loudness is therefore that first
    /// argument, and the only way to raise it is to play the effect
    /// ourselves.
    /// </para>
    /// <para>
    /// The asset is Blish's own button-click.wav, read once from ref.dat
    /// (the same archive and sub-path ContentService reads: its audio
    /// reader is <c>new ZipArchiveReader(RefPath).GetSubPath("audio")</c>),
    /// so this changes the click's VOLUME and nothing else about it.
    /// ZipArchiveReader.GetFileStream returns a fully-buffered
    /// MemoryStream, so the archive handle is closed again immediately and
    /// only the decoded SoundEffect is retained. Blish itself keeps no
    /// SoundEffect at all - PlaySoundEffectByName re-decodes the wav on
    /// every single click - so caching one 22 KB effect here is strictly
    /// less work per click than the path it replaces.
    /// </para>
    /// <para>
    /// DELIBERATE DIVERGENCE (recorded in KNOWN-ISSUES): playing the
    /// effect ourselves decouples the click from Blish's game-volume
    /// coupling, including its "mute if the game makes no sound" rule.
    /// That rule is why the click is inaudible in the first place, and its
    /// zero case is not only a muted game: the peak buffer also reads zero
    /// whenever GW2 is not running or simply is not making noise right
    /// now, which would make the Settings tab's own Test button dead on
    /// arrival exactly when someone is trying to set the volume with the
    /// game quiet. The user-facing mute is the slider's own 0.
    /// </para>
    /// </summary>
    internal static class ClickSound
    {
        private static readonly Logger Logger = Blish_HUD.Logger.GetLogger(typeof(ClickSound));

        // ContentService's audio reader is already rooted at ref.dat's
        // "audio" folder, so the entry name carries no "audio/" prefix of
        // its own - see PressFeedback's own note on the StandardButton bug
        // that same detail causes.
        private const string AudioSubPath = "audio";
        private const string ClickSoundEntry = "button-click.wav";

        // ContentService.RefPath's own fallback for a launch that named no
        // --ref path (relative to Blish's working directory, which this
        // module shares with it).
        private const string DefaultRefPath = "ref.dat";

        // Blish's own PlaySoundEffectByName gives up permanently after 3
        // consecutive failures; the same budget here keeps a broken audio
        // stack from throwing and logging once per click forever.
        private const int MaxPlayAttempts = 3;

        private static readonly object LoadLock = new object();

        private static SoundEffect _effect;
        private static bool _loadFailed;
        private static int _playRemainingAttempts = MaxPlayAttempts;

        // Written from the settings row (UI thread) and from Module's own
        // load (Blish's module-load thread), read on every click. Volatile
        // so a click on either thread sees the last value written on the
        // other; the value is clamped on the way IN so every read is
        // already inside [0,100].
        private static volatile int _volumePercent = ClickSoundVolume.DefaultPercent;

        /// <summary>
        /// The live click volume, 0-100. 0 means no playback at all.
        /// </summary>
        internal static int VolumePercent
        {
            get
            {
                return _volumePercent;
            }
            set
            {
                _volumePercent = ClickSoundVolume.Clamp(value);
            }
        }

        /// <summary>
        /// Plays the click at <see cref="VolumePercent"/>. Silent - with no
        /// asset load and no pooled voice - at 0, and on a machine with no
        /// audio device.
        /// <para>
        /// That last guard is kept from PlaySoundEffectByName's own
        /// preamble but reads a different signal. Blish tests
        /// <c>GameIntegration.Audio.AudioDevice == null</c>, whose type is
        /// NAudio's MMDevice - referencing it here would pull the whole
        /// NAudio.Wasapi assembly into this module for one null check
        /// (measured: it is a CS0012 without it). MonoGame answers the same
        /// question in a type this module already references:
        /// <c>SoundEffect</c>'s stream constructor throws
        /// <c>NoAudioHardwareException</c> when the sound system failed to
        /// initialize, which EnsureLoaded turns into a permanent, quiet
        /// give-up.
        /// </para>
        /// </summary>
        internal static void Play()
        {
            int percent = _volumePercent;
            if (ClickSoundVolume.IsSilent(percent)) return;
            if (_playRemainingAttempts <= 0) return;

            try
            {
                var effect = EnsureLoaded();
                if (effect == null) return;

                effect.Play(ClickSoundVolume.ToVolume(percent), 0f, 0f);
                _playRemainingAttempts = MaxPlayAttempts;
            }
            catch (Exception ex)
            {
                _playRemainingAttempts--;
                Logger.Warn(ex, "Failed to play the module click sound.");
            }
        }

        /// <summary>
        /// Drops the cached effect and re-arms the load, so a module
        /// reloaded inside one Blish session does not inherit the previous
        /// load's disposed effect or its failure verdict. Blish keeps
        /// module statics for the life of the process, which is why this
        /// exists at all.
        /// </summary>
        internal static void Unload()
        {
            lock (LoadLock)
            {
                var effect = _effect;
                _effect = null;
                _loadFailed = false;
                _playRemainingAttempts = MaxPlayAttempts;

                if (effect == null) return;

                try
                {
                    effect.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to dispose the module click sound.");
                }
            }
        }

        /// <summary>
        /// The wav, decoded once. Returns null - permanently, after one
        /// attempt - when ref.dat cannot be read: a missing asset is a
        /// silent click, never a per-click exception.
        /// </summary>
        private static SoundEffect EnsureLoaded()
        {
            var loaded = _effect;
            if (loaded != null) return loaded;

            lock (LoadLock)
            {
                if (_effect != null) return _effect;
                if (_loadFailed) return null;

                // Set BEFORE the load rather than in the catch: every exit
                // below other than a successful decode is a permanent
                // give-up, and this way a throw from anywhere inside cannot
                // leave the flag unset and re-attempt on the next click.
                _loadFailed = true;

                string refPath = ApplicationSettings.Instance?.RefPath;
                if (string.IsNullOrWhiteSpace(refPath))
                {
                    refPath = DefaultRefPath;
                }

                try
                {
                    using (var reader = new ZipArchiveReader(refPath, AudioSubPath))
                    {
                        if (!reader.FileExists(ClickSoundEntry)) return null;

                        using (var stream = reader.GetFileStream(ClickSoundEntry))
                        {
                            if (stream == null) return null;

                            _effect = SoundEffect.FromStream(stream);
                        }
                    }
                }
                catch (NoAudioHardwareException)
                {
                    // Not a fault to warn about: this machine simply has no
                    // working audio output, which is exactly the case
                    // Blish's own AudioDevice null check covers.
                    Logger.Debug("No audio hardware available; the module click sound stays silent.");
                    return null;
                }

                _loadFailed = false;
                return _effect;
            }
        }
    }
}
