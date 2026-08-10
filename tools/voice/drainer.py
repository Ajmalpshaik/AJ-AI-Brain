#!/usr/bin/env python
# The single voice of the AJ AI Brain. Everything spoken on this machine comes out of this one
# process, in order, one line at a time.
#
# WHY A DRAINER AND NOT "JUST SPEAK IT":
# Ajmal asked for every single action to be narrated. The naive version - each hook shells out and
# speaks - breaks in two ways that only show up once it is actually running:
#
#   1. OVERLAP. Independent tool calls fire in parallel, so five processes start talking over each
#      other and you understand none of it. Speech has to be serialised through one place, and that
#      place is this file: it drains the queue strictly in order and blocks while each clip plays.
#
#   2. COLD START. Measured on this machine 2026-08-11: importing edge_tts costs ~1.4s, the network
#      synthesis itself only ~1.1s. Paying that import on every line means ~2.7s of lag per action -
#      the narration falls hopelessly behind the work. So this process imports once and STAYS WARM,
#      draining whatever arrives, and exits by itself after idleExitSeconds. Warm cost per line is
#      ~1.1s, and a cached line is instant.
#
# NEVER GOES SILENT: the neural voice needs internet and a Python package that lives in a gitignored
# venv, so on a fresh copy of the Brain neither may exist. Both are treated as upgrades, not
# requirements - if the package is missing or the network is down it speaks through the Windows
# built-in voice instead, which is present on every Windows machine. A voice assistant that goes
# quiet when the wifi drops is worse than a robotic one that keeps talking.
#
# NEVER BLOCKS THE WORK: nothing here is allowed to slow down or break the actual Revit job. Callers
# drop a file in the queue and leave; every failure in this file is caught, logged and swallowed.
#
# Started automatically by whoever enqueues a line - it is not a service and there is nothing to
# install, start or remember. Run it by hand only to debug:
#     semantic-index/venv/Scripts/python.exe tools/voice/drainer.py --foreground

import ctypes
import hashlib
import json
import os
import subprocess
import sys
import time

VOICE_DIR = os.path.dirname(os.path.abspath(__file__))
CONFIG_PATH = os.path.join(VOICE_DIR, "voice-config.json")

# Runtime state lives outside the Brain, in the folder the AJ Tools add-in also writes to. It is the
# meeting point for the two voices (see say.mjs for the full reasoning), and it keeps generated audio
# out of a repo whose whole purpose is to be copied between machines.
RUNTIME_DIR = os.path.join(
    os.environ.get("LOCALAPPDATA") or os.path.expanduser("~"), "AJTools", "voice"
)
QUEUE_DIR = os.path.join(RUNTIME_DIR, "queue")
CACHE_DIR = os.path.join(RUNTIME_DIR, "cache")
LOCK_PATH = os.path.join(QUEUE_DIR, ".drainer.lock")
LOG_PATH = os.path.join(RUNTIME_DIR, "voice.log")

FOREGROUND = "--foreground" in sys.argv

# Absolute ceiling on how long one spoken line may hold the queue. Lines are a dozen words, so this
# is never reached in normal use - it exists purely so that no single clip can wedge the voice.
MAX_CLIP_SECONDS = 20.0


def log(message):
    """Best-effort diagnostics. A failure to log must never take the voice down with it."""
    try:
        os.makedirs(RUNTIME_DIR, exist_ok=True)  # A log line can arrive before main() makes the dirs.
        stamp = time.strftime("%Y-%m-%d %H:%M:%S")
        with open(LOG_PATH, "a", encoding="utf-8") as handle:
            handle.write("[{0}] {1}\n".format(stamp, message))
        if FOREGROUND:
            print(message, flush=True)
    except Exception:
        pass


def load_config():
    """Config is re-read on every line, so `voice off` takes effect mid-sentence-stream rather than
    at the next session. Keys starting with _comment are documentation for Ajmal, not settings."""
    try:
        with open(CONFIG_PATH, "r", encoding="utf-8") as handle:
            return json.load(handle)
    except Exception as error:
        log("config unreadable, using defaults: {0}".format(error))
        return {}


def profile_for(config, name):
    profiles = config.get("profiles", {})
    if name in profiles:
        return profiles[name]
    if "jarvis" in profiles:
        return profiles["jarvis"]
    return {
        "neural": "en-GB-RyanNeural",
        "neuralRate": "+18%",
        "fallback": "Microsoft David Desktop",
        "fallbackRate": 2,
    }


def trim(text, max_words):
    """Short reply in voice, per Ajmal's ask. A summary that runs long gets cut at a word boundary
    rather than mid-word, because a clipped word sounds like a crash."""
    words = str(text).split()
    if max_words and len(words) > max_words:
        return " ".join(words[:max_words]).rstrip(",;:") + "."
    return " ".join(words)


# --------------------------------------------------------------------------------------------------
# Speaking
# --------------------------------------------------------------------------------------------------

_edge_tts = None
_edge_tts_import_failed = False
_neural_cold_until = 0.0


def neural_available():
    """Import edge_tts once and remember the answer. Retrying a failed import on every line would
    cost real time on machines where the package simply is not installed."""
    global _edge_tts, _edge_tts_import_failed
    if _edge_tts is not None:
        return True
    if _edge_tts_import_failed:
        return False
    try:
        import edge_tts  # noqa: PLC0415 - deliberately deferred; this is the expensive import
        _edge_tts = edge_tts
        return True
    except Exception as error:
        _edge_tts_import_failed = True
        log("neural voice unavailable ({0}) - using the Windows built-in voice".format(error))
        return False


def cache_path(text, voice, rate):
    """Cache by exactly what was spoken. 'Done.' gets synthesised once, ever, and then plays instantly
    and offline forever after - which is most of what a per-action narrator actually says."""
    key = hashlib.sha1("{0}|{1}|{2}".format(text, voice, rate).encode("utf-8")).hexdigest()
    return os.path.join(CACHE_DIR, key + ".mp3")


def synthesise_neural(text, voice, rate, destination):
    """Write the clip to a temp file and rename it into place, so a half-downloaded clip caused by a
    dropped connection can never be cached and replayed as a permanent stutter."""
    import asyncio

    partial = destination + ".part"

    async def run():
        communicate = _edge_tts.Communicate(text, voice, rate=rate)
        written = 0
        with open(partial, "wb") as handle:
            async for chunk in communicate.stream():
                if chunk["type"] == "audio":
                    handle.write(chunk["data"])
                    written += len(chunk["data"])
        return written

    written = asyncio.run(run())
    if written <= 0:
        raise RuntimeError("neural voice returned no audio")
    os.replace(partial, destination)


def play(path):
    """
    Play through winmm, which is part of Windows itself - no audio package to install, and no console
    window.

    NOT `play ... wait`, even though that is the obvious one-liner. `wait` hands control to MCI until
    the clip ends, and in a detached background process with no window and no message pump it can sit
    there indefinitely. One stuck clip in that mode wedges the whole queue permanently: the drainer
    never returns, never idles out, and the voice simply stops for the rest of the day with nothing in
    the log to say why.

    So playback is started without waiting, and this waits on its own terms - polling the play mode,
    bounded by the clip's own reported length plus a margin, and hard-capped regardless. Waiting here
    is still what serialises the queue; it just can no longer become an infinite wait.
    """
    mci = ctypes.windll.winmm.mciSendStringW
    buffer = ctypes.create_unicode_buffer(256)
    alias = "ajvoice{0}".format(os.getpid())

    def command(text):
        code = mci(text, buffer, 256, 0)
        return code, buffer.value

    # A stale alias left by a clip that failed mid-play would break every later line.
    command("close {0}".format(alias))
    if command('open "{0}" type mpegvideo alias {1}'.format(path, alias))[0] != 0:
        raise RuntimeError("could not open clip")

    try:
        # The clip tells us how long it is, so the wait can be sized to the actual audio.
        code, value = command("status {0} length".format(alias))
        try:
            length_seconds = int(value) / 1000.0 if code == 0 else 0.0
        except ValueError:
            length_seconds = 0.0

        if command("play {0}".format(alias))[0] != 0:
            raise RuntimeError("could not play clip")

        deadline = time.time() + min(length_seconds + 1.5, MAX_CLIP_SECONDS)
        while time.time() < deadline:
            code, mode = command("status {0} mode".format(alias))
            if code != 0 or mode != "playing":
                break
            time.sleep(0.05)
    finally:
        command("close {0}".format(alias))


def speak_fallback(text, voice_name, rate):
    """The always-works path: the Windows built-in voice, driven through PowerShell so it needs no
    Python package at all. Slower to start than the neural path, but it is only ever reached when
    the neural path is genuinely unavailable, and it keeps the assistant talking offline."""
    safe_text = str(text).replace("'", "''")
    safe_voice = str(voice_name).replace("'", "''")
    script = (
        "Add-Type -AssemblyName System.Speech; "
        "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; "
        "try {{ $s.SelectVoice('{0}') }} catch {{ }}; "
        "$s.Rate = {1}; "
        "$s.Speak('{2}'); "
        "$s.Dispose()"
    ).format(safe_voice, int(rate), safe_text)

    creation_flags = 0x08000000  # CREATE_NO_WINDOW - never flash a console over Revit
    subprocess.run(
        ["powershell", "-NoProfile", "-NonInteractive", "-Command", script],
        creationflags=creation_flags,
        timeout=60,
        capture_output=True,
    )


def speak(text, profile_name, config):
    """Neural if it can, Windows built-in if it must, and never an exception to the caller."""
    global _neural_cold_until

    profile = profile_for(config, profile_name)
    text = trim(text, config.get("maxWords", 14))
    if not text:
        return

    neural_voice = profile.get("neural", "en-GB-RyanNeural")
    neural_rate = profile.get("neuralRate", "+18%")
    destination = cache_path(text, neural_voice, neural_rate)

    if os.path.exists(destination):
        try:
            play(destination)
            return
        except Exception as error:
            log("cached clip would not play, re-synthesising: {0}".format(error))
            try:
                os.remove(destination)
            except Exception:
                pass

    if neural_available() and time.time() >= _neural_cold_until:
        try:
            synthesise_neural(text, neural_voice, neural_rate, destination)
            play(destination)
            return
        except Exception as error:
            # Almost always "no internet". Stop paying the network timeout on every following line.
            cooldown = config.get("neuralCooldownSeconds", 60)
            _neural_cold_until = time.time() + cooldown
            log("neural voice failed ({0}) - offline voice for the next {1}s".format(error, cooldown))

    try:
        speak_fallback(text, profile.get("fallback", "Microsoft David Desktop"), profile.get("fallbackRate", 2))
    except Exception as error:
        log("could not speak at all: {0}".format(error))


# --------------------------------------------------------------------------------------------------
# The queue
# --------------------------------------------------------------------------------------------------

def claim_lock():
    """One drainer at a time, or the overlap problem comes straight back. A lock file left behind by
    a killed process must not silence the voice forever, so a lock whose process is gone is taken."""
    try:
        if os.path.exists(LOCK_PATH):
            with open(LOCK_PATH, "r", encoding="utf-8") as handle:
                existing = handle.read().strip()
            if existing.isdigit() and process_alive(int(existing)) and int(existing) != os.getpid():
                return False
        with open(LOCK_PATH, "w", encoding="utf-8") as handle:
            handle.write(str(os.getpid()))
        return True
    except Exception as error:
        log("lock failed: {0}".format(error))
        return False


def process_alive(pid):
    """OpenProcess on a dead PID fails; on a live one it succeeds and we close the handle again."""
    try:
        handle = ctypes.windll.kernel32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
        if handle:
            ctypes.windll.kernel32.CloseHandle(handle)
            return True
        return False
    except Exception:
        return False


def release_lock():
    try:
        if os.path.exists(LOCK_PATH):
            with open(LOCK_PATH, "r", encoding="utf-8") as handle:
                if handle.read().strip() == str(os.getpid()):
                    os.remove(LOCK_PATH)
    except Exception:
        pass


def pending():
    """Queue files are named with a zero-padded millisecond timestamp, so plain filename order IS
    chronological order - no index, no database, and anything that can write a file can enqueue."""
    try:
        names = [n for n in os.listdir(QUEUE_DIR) if n.endswith(".json")]
        names.sort()
        return [os.path.join(QUEUE_DIR, n) for n in names]
    except Exception:
        return []


def main():
    os.makedirs(QUEUE_DIR, exist_ok=True)
    os.makedirs(CACHE_DIR, exist_ok=True)

    if not claim_lock():
        return  # Another drainer already owns the queue. Nothing to do, and that is the normal case.

    # One line per drainer lifetime, on purpose. This process is invisible by design - no console, no
    # window - so without a start/stop record the only symptom of it dying is a voice that goes quiet,
    # which is indistinguishable from having nothing to say. That ambiguity is what made the
    # 2026-08-11 failure so slow to pin down.
    log("drainer started (pid {0})".format(os.getpid()))

    try:
        idle_limit = load_config().get("idleExitSeconds", 45)
        last_spoke = time.time()

        while True:
            items = pending()

            if not items:
                if time.time() - last_spoke > idle_limit:
                    return  # Nothing to say for a while - stop holding memory and let go of the lock.
                time.sleep(0.15)
                continue

            for item_path in items:
                config = load_config()  # Re-read so `voice off` lands immediately, mid-queue.

                try:
                    with open(item_path, "r", encoding="utf-8") as handle:
                        item = json.load(handle)
                except Exception:
                    item = None

                # Remove first, speak second. A clip that fails must not be retried forever, and a
                # queue that keeps growing is worse than a line that goes unsaid.
                try:
                    os.remove(item_path)
                except Exception:
                    pass

                if not item:
                    continue

                if not config.get("enabled", True):
                    continue  # Muted: drop the backlog rather than say it all at once when unmuted.

                speak(item.get("text", ""), item.get("profile", "jarvis"), config)
                last_spoke = time.time()
                idle_limit = config.get("idleExitSeconds", 45)
    except Exception as error:
        log("drainer stopped: {0}".format(error))
    finally:
        log("drainer exiting (pid {0})".format(os.getpid()))
        release_lock()


if __name__ == "__main__":
    # This normally runs under pythonw.exe, which has no console: anything raised outside main()'s
    # own try - a bad path, a missing DLL, a typo in a constant - would kill it with no message
    # anywhere, and the only symptom would be a queue that silently stops draining. That exact
    # failure cost real time on 2026-08-11, so the traceback always lands in the log.
    try:
        main()
    except Exception:
        import traceback
        log("drainer crashed before it could start:\n" + traceback.format_exc())
