#!/usr/bin/env python3
"""Generates the game's sound effects, music and voice lines with the ElevenLabs API.

The key is read from DevBridge/elevenlabs_key.txt (one line) or the ELEVENLABS_API_KEY environment
variable; it is never printed. Files land in Assets/Resources/Audio. Files that already exist are skipped,
so the script can be re-run after a failure, and deleting a file makes it regenerate.

    python Tools/generate_audio.py            # everything
    python Tools/generate_audio.py sfx        # only one group: sfx, ambience, music, voices
"""
import json, os, sys, time, urllib.request, urllib.error

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
API = "https://api.elevenlabs.io"

# name: (prompt, seconds)
SFX = {
    "swing": ("fast whoosh of an arm or club swinging through the air, short, dry, no impact", 0.6),
    "swing_2": ("quick air whoosh of a swung stick, short and sharp", 0.6),
    "punch": ("a bare fist punch landing on a body, dull meaty thud, close", 0.6),
    "punch_2": ("heavy body punch impact, short thump", 0.6),
    "hit_flesh": ("a wooden club hitting an animal's body, heavy thud with a crack", 0.7),
    "chop": ("an axe chopping into a tree trunk, single sharp woody thunk", 0.7),
    "chop_2": ("hatchet striking wood, one solid chop", 0.7),
    "tree_fall": ("a tree creaking, cracking and falling over, crashing into undergrowth", 3.0),
    "rock": ("a pickaxe striking stone, single sharp crack with small falling chips", 0.8),
    "metal": ("a hammer hitting a car body panel, single metallic clang", 0.9),
    "search": ("rummaging through a cupboard, items shifting, short", 1.2),
    "loot": ("picking up supplies, a soft cloth rustle and a small pleasant click", 0.7),
    "craft": ("a hammer tapping wood on a workbench, two taps", 0.8),
    "craft_done": ("a short satisfying wooden clack and a small bright chime, item finished", 1.0),
    "place": ("a heavy wooden object set down on a concrete floor, solid thud", 0.8),
    "break": ("wooden planks splintering and breaking apart", 1.0),
    "hurt": ("a man grunting in pain from a blow, short", 0.7),
    "eat": ("someone chewing a bite of food, short", 1.0),
    "drink": ("someone taking two gulps of water from a bottle", 1.2),
    "cloth": ("tearing and wrapping a cloth bandage, short", 1.0),
    "sizzle": ("meat sizzling over a campfire, short", 1.2),
    "levelup": ("a warm rising three-note chime, achievement, hopeful", 1.5),
    "click": ("a soft low wooden interface click", 0.5),
    "footstep": ("a single boot footstep on concrete, soft", 0.5),
    "footstep_2": ("a single boot footstep on gravelly ground, soft", 0.5),
    "footstep_3": ("one quiet footstep on a hard surface", 0.5),
    "growl": ("a wolf growling and snarling, aggressive, close", 1.6),
    "growl_2": ("a large predator animal snarling with a short bark", 1.5),
    "bite": ("an animal snapping its jaws in a quick vicious bite", 0.7),
    "death_animal": ("an animal's short dying yelp followed by a body falling", 1.2),
    "raid_horn": ("an ominous low horn blast mixed with distant monstrous screeches, alarm, danger arriving", 4.0),
    "door_open": ("a heavy shop door being pushed open with a short hinge creak", 1.0),
    "door_close": ("a heavy double door swinging shut with a solid thud", 0.9),
    "rope": ("a rope sliding fast through a metal ring, rappelling zip", 1.5),
    "sleep": ("a slow tired exhale and a sleeping bag rustling", 2.0),
    "dawn": ("a rooster-free quiet dawn: soft birdsong swelling with a gentle warm tone", 3.0),
}

AMBIENCE = {
    "amb_roof": ("steady high wind over a rooftop of an abandoned city, distant flapping tarp, lonely, seamless loop", 22),
    "amb_city": ("an empty ruined city street, soft wind between buildings, distant crows, a loose metal sign creaking, seamless loop", 22),
    "amb_forest": ("a daytime forest, birdsong, leaves rustling in a light breeze, peaceful, seamless loop", 22),
    "amb_night": ("night in the wilderness, crickets, a far-off owl, low wind, uneasy, seamless loop", 22),
}

# name: (prompt, seconds)
MUSIC = {
    "music_menu": ("Instrumental title theme for a post-apocalyptic survival game. Slow, melancholic but hopeful. Sparse acoustic guitar and soft piano over warm strings and a distant low drum. No vocals.", 75),
    "music_day": ("Instrumental calm exploration music for a survival game. Gentle fingerpicked acoustic guitar, light ambient pads, a sense of quiet and open space. Unobtrusive, loops well. No vocals.", 90),
    "music_night": ("Instrumental dark ambient tension music. Low drones, sparse piano notes, distant metallic textures, uneasy and watchful. Slow, minimal, loops well. No vocals.", 80),
    "music_raid": ("Instrumental combat music for a survival game. Driving tribal drums, urgent low strings, tense and relentless, medium-fast tempo. Loops well. No vocals.", 70),
}

# Two Drifters. Voice ids are ElevenLabs premade voices; change them here to recast.
VOICES = {"m": "pNInz6obpgDQGcFmaJgB", "f": "EXAVITQu4vr4xnSDxMaL"}
LINES = {
    "greet": "Easy. We don't want any trouble.",
    "friend": "Good to see a friendly face out here.",
    "thanks": "You saved us. We won't forget it.",
    "help": "Help! Somebody! Get them off us!",
    "warn": "Hey! Back off! Last warning!",
    "hostile": "That's the one! Take them down!",
    "gift": "Here. Take this. You've earned it.",
}


def key():
    k = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    path = os.path.join(ROOT, "DevBridge", "elevenlabs_key.txt")
    if not k and os.path.exists(path):
        k = open(path, encoding="utf-8-sig").read().strip()
    if not k:
        sys.exit("No key. Put it in DevBridge/elevenlabs_key.txt or set ELEVENLABS_API_KEY.")
    return k


def post(path, body, k, timeout=300):
    req = urllib.request.Request(API + path, data=json.dumps(body).encode(), method="POST",
                                 headers={"xi-api-key": k, "Content-Type": "application/json", "Accept": "audio/mpeg"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


def make(name, fn):
    """Runs fn() to get audio bytes and saves them as name.mp3. Returns 'made', 'skipped' or 'FAILED: why'."""
    out = os.path.join(OUT, name + ".mp3")
    if os.path.exists(out) and os.path.getsize(out) > 1000:
        return "skipped"
    for attempt in range(3):
        try:
            data = fn()
            if len(data) < 1000:
                return "FAILED: empty reply"
            with open(out, "wb") as f:
                f.write(data)
            return "made"
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", "replace")[:300]
            if e.code == 429 and attempt < 2:
                time.sleep(8 * (attempt + 1))
                continue
            return "FAILED: HTTP %d %s" % (e.code, detail)
        except Exception as e:
            if attempt < 2:
                time.sleep(4)
                continue
            return "FAILED: %s" % e
    return "FAILED"


def main():
    only = sys.argv[1] if len(sys.argv) > 1 else ""
    k = key()
    os.makedirs(OUT, exist_ok=True)
    results = {}

    def run(group, name, fn):
        if only and only != group:
            return
        r = make(name, fn)
        results[name] = r
        print("%-16s %s" % (name, r), flush=True)

    for name, (prompt, secs) in SFX.items():
        run("sfx", name, lambda p=prompt, s=secs: post("/v1/sound-generation", {"text": p, "duration_seconds": max(0.5, s), "prompt_influence": 0.45}, k))
    for name, (prompt, secs) in AMBIENCE.items():
        run("ambience", name, lambda p=prompt, s=secs: post("/v1/sound-generation", {"text": p, "duration_seconds": s, "prompt_influence": 0.4}, k))
    for name, (prompt, secs) in MUSIC.items():
        run("music", name, lambda p=prompt, s=secs: post("/v1/music", {"prompt": p, "music_length_ms": int(s * 1000)}, k, timeout=600))
    for who, voice in VOICES.items():
        for line, text in LINES.items():
            run("voices", "v_%s_%s" % (who, line),
                lambda t=text, v=voice: post("/v1/text-to-speech/%s?output_format=mp3_44100_128" % v,
                                             {"text": t, "model_id": "eleven_multilingual_v2",
                                              "voice_settings": {"stability": 0.4, "similarity_boost": 0.75, "style": 0.5}}, k))

    made = sum(1 for r in results.values() if r == "made")
    skipped = sum(1 for r in results.values() if r == "skipped")
    failed = {n: r for n, r in results.items() if r.startswith("FAILED")}
    print("\n%d made, %d already there, %d failed." % (made, skipped, len(failed)))
    for n, r in failed.items():
        print("  %s: %s" % (n, r))


if __name__ == "__main__":
    main()
