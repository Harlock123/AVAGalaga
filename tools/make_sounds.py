#!/usr/bin/env python3
"""Synthesises AVAGalaga's sound effects and tunes into ../src/AVAGalaga/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAGalaga", "Sounds")
RNG = random.Random(1981)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def main():
    os.makedirs(OUT, exist_ok=True)
    # The fighter's shot: a quick downward zap.
    write("Fire.wav", tone(0.13, 1900, 420, "square", release=1.6), 0.45)
    # Enemies hit: drones squeak, moths chirp, commanders clang then burst.
    write("DroneHit.wav", cat(tone(0.05, 1200, 700, "square"), tone(0.16, 900, 120, "noise", release=1.4)), 0.6)
    write("MothHit.wav", cat(tone(0.05, 1500, 1100, "square"), tone(0.18, 700, 90, "noise", release=1.2)), 0.6)
    write("CommanderHurt.wav", mix(tone(0.22, 880, 860, "tri", release=2.5), tone(0.22, 1320, 1300, "tri", release=2.5, gain=0.6)), 0.6)
    write("CommanderDie.wav", cat(tone(0.08, 600, 900, "square"), tone(0.45, 400, 50, "noise", release=1.0)), 0.75)
    # The fighter blown up: a long, falling roar.
    write("FighterDie.wav", mix(tone(1.1, 900, 40, "noise", release=0.7), tone(1.1, 300, 40, "saw", release=1.2, gain=0.4)), 0.85)
    # A dive: the falling whistle.
    write("Dive.wav", tone(0.9, 2200, 500, "tri", attack=0.03, release=0.6, vibrato=0.03, vib_rate=11), 0.45)
    # The tractor beam's warble (loops while the beam is out).
    beam = []
    for k in range(8):
        beam.extend(tone(0.125, 300 + 60 * (k % 4), 520 + 60 * (k % 4), "pulse", attack=0.01, release=0.2, gain=0.6))
    write("Beam.wav", beam, 0.5)
    # Captured: a sad, falling phrase. Rescued: rising and bright.
    write("Captured.wav", cat(*[tone(0.22, hz(n), kind="tri", release=0.5) for n in ["E5", "C5", "A4", "F4", "D4"]], tone(0.6, hz("A3"), kind="tri", release=0.8)), 0.6)
    write("Rescued.wav", cat(*[tone(0.09, hz(n), kind="pulse", release=0.6) for n in ["C5", "E5", "G5", "C6", "E6", "G6"]], tone(0.4, hz("C7"), kind="pulse", release=0.9)), 0.5)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="square", release=0.4) for n in ["G5", "C6", "E6", "G6"] * 3]), 0.45)
    write("Badge.wav", cat(tone(0.06, hz("A5"), kind="pulse"), tone(0.06, hz("E6"), kind="pulse")), 0.4)

    # The stage tune: a bright, marching call (new; in D).
    write("StageStart.wav", tune(150,
        [("D5", 0.5), ("F#5", 0.5), ("A5", 0.5), ("D6", 0.5), ("C#6", 0.5), ("A5", 0.5), ("B5", 1),
         ("G5", 0.5), ("B5", 0.5), ("D6", 0.5), ("E6", 0.5), ("F#6", 1.5), ("-", 0.5)],
        [("D3", 1), ("A2", 1), ("D3", 1), ("G2", 1), ("G2", 1), ("A2", 1), ("D3", 1.5), ("-", 0.5)]), 0.7)
    # The challenging stage: a quick, playful run.
    write("Challenge.wav", tune(176,
        [("E5", 0.5), ("G5", 0.5), ("B5", 0.5), ("E6", 0.5), ("D6", 0.5), ("B5", 0.5), ("G5", 0.5), ("A5", 0.5),
         ("C6", 0.5), ("E6", 0.5), ("G6", 0.5), ("F#6", 0.5), ("E6", 2)],
        [("E3", 1), ("B2", 1), ("E3", 1), ("D3", 1), ("A2", 1), ("C3", 1), ("B2", 2)]), 0.7)
    # A perfect challenge stage.
    write("Perfect.wav", tune(190,
        [("C6", 0.5), ("E6", 0.5), ("G6", 0.5), ("C7", 0.5), ("G6", 0.5), ("C7", 1.5)],
        [("C3", 1), ("G2", 1), ("C3", 2)], kind="square"), 0.6)
    # Game over: slow and falling.
    write("GameOver.wav", tune(96,
        [("A5", 1), ("G5", 1), ("F5", 1), ("E5", 1), ("D5", 1), ("C#5", 1), ("D5", 2)],
        [("D3", 2), ("A2", 2), ("Bb2", 2), ("D3", 2)], kind="tri"), 0.65)
    print("sounds written to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
