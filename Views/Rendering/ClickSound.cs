using System;
using Blish_HUD;
using Blish_HUD.Content;
using Microsoft.Xna.Framework.Audio;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// Plays the module's UI click at a user-controlled volume. Blish's
    /// own <c>PlaySoundEffectByName</c> plays at its game-derived volume -
    /// capped at 0.4, zero when the game is quiet or closed (measured,
    /// 1.3.0) - hence the reported inaudibility, and the only way past the
    /// cap is to play the effect ourselves. The asset is Blish's own
    /// button-click.wav from ref.dat, so only the volume changes; the
    /// divergence from Blish's mute-with-game rule is in KNOWN-ISSUES #52.
    /// </summary>
    internal static class ClickSound
    {
        private static readonly Logger Logger = Blish_HUD.Logger.GetLogger(typeof(ClickSound));

        // ContentService's audio reader is already rooted at ref.dat's
        // "audio" folder, so entry names carry no "audio/" prefix - see
        // FeedbackButton on the StandardButton bug that detail causes.
        private const string AudioSubPath = "audio";
        private const string ClickSoundEntry = "button-click.wav";

        // ContentService.RefPath's own fallback when no --ref path is given.
        private const string DefaultRefPath = "ref.dat";

        // Same give-up budget as Blish's PlaySoundEffectByName, so a broken
        // audio stack cannot throw and log once per click forever.
        private const int MaxPlayAttempts = 3;

        private static readonly object LoadLock = new object();

        private static SoundEffect _effect;
        private static bool _loadFailed;
        private static int _playRemainingAttempts = MaxPlayAttempts;

        // Written on the UI thread and Blish's module-load thread, read on
        // every click; clamped on the way in so every read is in [0,100].
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
        /// Plays the click at <see cref="VolumePercent"/>. Silent at 0 and
        /// on a machine with no audio device, with no asset load either way.
        /// </summary>
        internal static void Play()
        {
            int percent = _volumePercent;
            if (ClickSoundVolume.IsSilent(percent))
            {
                return;
            }

            if (_playRemainingAttempts <= 0)
            {
                return;
            }

            try
            {
                var effect = EnsureLoaded();
                if (effect == null)
                {
                    return;
                }

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
        /// Drops the cached effect and re-arms the load. Blish keeps module
        /// statics for the life of the process, so without this a module
        /// reloaded in-session would inherit a disposed effect or a stale
        /// failure verdict.
        /// </summary>
        internal static void Unload()
        {
            lock (LoadLock)
            {
                var effect = _effect;
                _effect = null;
                _loadFailed = false;
                _playRemainingAttempts = MaxPlayAttempts;

                if (effect == null)
                {
                    return;
                }

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
            if (loaded != null)
            {
                return loaded;
            }

            lock (LoadLock)
            {
                if (_effect != null)
                {
                    return _effect;
                }

                if (_loadFailed)
                {
                    return null;
                }

                // Set before the load, not in a catch: every exit below
                // other than a successful decode is a permanent give-up.
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
                        if (!reader.FileExists(ClickSoundEntry))
                        {
                            return null;
                        }

                        using (var stream = reader.GetFileStream(ClickSoundEntry))
                        {
                            if (stream == null)
                            {
                                return null;
                            }

                            _effect = SoundEffect.FromStream(stream);
                        }
                    }
                }
                catch (NoAudioHardwareException)
                {
                    // The no-audio-device case Blish covers with an NAudio
                    // MMDevice null check; testing that here would pull
                    // NAudio.Wasapi into this module (CS0012), and MonoGame
                    // reports the same condition via this exception.
                    Logger.Debug("No audio hardware available; the module click sound stays silent.");
                    return null;
                }

                _loadFailed = false;
                return _effect;
            }
        }
    }
}
