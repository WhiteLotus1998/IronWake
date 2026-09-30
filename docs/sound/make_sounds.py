#!/usr/bin/env python3
"""Writes the Godot client's sound set (issue 516) under src/Ironwake.Godot/assets/sound/.

Every clip is synthesised here from sines and seeded noise, so the set is the project's own and
dedicated CC0 (LICENSES). The output is the same bytes on every run: the noise is a fixed linear
congruential generator, not the random module. 16-bit mono PCM at 22050 Hz, one RIFF file per clip.

Run from the repository root: python3 docs/sound/make_sounds.py
"""
import math
import os
import struct

RATE = 22050
OUT = os.path.join("src", "Ironwake.Godot", "assets", "sound")


class Noise:
    """White noise in -1..1 from a 32-bit LCG seeded per clip."""

    def __init__(self, seed):
        self.state = seed & 0xFFFFFFFF

    def next(self):
        self.state = (1664525 * self.state + 1013904223) & 0xFFFFFFFF
        return self.state / 2147483648.0 - 1.0


def lowpass(samples, cutoff):
    """A one-pole low-pass at cutoff Hz."""
    a = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    out, y = [], 0.0
    for s in samples:
        y += a * (s - y)
        out.append(y)
    return out


def length(seconds):
    return int(seconds * RATE)


def hit():
    """A body blow: a low sine thump under a short, dark burst of noise."""
    n = length(0.22)
    noise = Noise(11)
    burst = lowpass([noise.next() for _ in range(n)], 900)
    out = []
    for i in range(n):
        t = i / RATE
        thump = math.sin(2 * math.pi * (95 + 60 * math.exp(-t * 30)) * t) * math.exp(-t * 16)
        out.append(0.8 * thump + 1.6 * burst[i] * math.exp(-t * 38))
    return out


def miss():
    """A swing through air: noise swept up and down in brightness, soft at both ends."""
    n = length(0.28)
    noise = Noise(23)
    raw = [noise.next() for _ in range(n)]
    out, y = [], 0.0
    for i in range(n):
        p = i / n
        cutoff = 400 + 2600 * math.sin(math.pi * p)
        a = 1 - math.exp(-2 * math.pi * cutoff / RATE)
        y += a * (raw[i] - y)
        out.append(0.55 * y * math.sin(math.pi * p) ** 1.5)
    return out


def crit():
    """The hit, then steel ringing: inharmonic partials that outlast the blow."""
    body = hit()
    n = length(0.6)
    out = []
    for i in range(n):
        t = i / RATE
        ring = sum(amp * math.sin(2 * math.pi * f * t) for f, amp in ((1240, 0.30), (1873, 0.18), (2731, 0.10)))
        ring *= math.exp(-t * 7) * min(1.0, t * 400)
        out.append((body[i] if i < len(body) else 0.0) * 1.1 + ring)
    return out


def fall():
    """A unit going down: a falling tone and a heavy, late thud."""
    n = length(0.8)
    noise = Noise(37)
    dust = lowpass([noise.next() for _ in range(n)], 300)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / RATE
        f = 190 * math.exp(-t * 2.2) + 45
        phase += 2 * math.pi * f / RATE
        tone = 0.5 * math.sin(phase) * math.exp(-t * 3.2)
        thud_t = t - 0.32
        thud = 0.0
        if thud_t > 0:
            thud = math.sin(2 * math.pi * 62 * thud_t) * math.exp(-thud_t * 14) + 2.2 * dust[i] * math.exp(-thud_t * 18)
        out.append(tone + 0.7 * thud)
    return out


def click():
    """A dry tick for a UI press."""
    n = length(0.05)
    noise = Noise(41)
    out = []
    for i in range(n):
        t = i / RATE
        out.append((0.5 * math.sin(2 * math.pi * 1900 * t) + 0.3 * noise.next()) * math.exp(-t * 160))
    return out


def ambient():
    """The bed: wind over open ground, brown noise breathing on two slow cycles, faded at both ends so it loops quietly."""
    seconds = 12.0
    n = length(seconds)
    noise = Noise(53)
    out, brown = [], 0.0
    low = 0.0
    a = 1 - math.exp(-2 * math.pi * 520 / RATE)
    for i in range(n):
        t = i / RATE
        brown = 0.985 * brown + 0.06 * noise.next()
        low += a * (brown - low)
        swell = 0.55 + 0.3 * math.sin(2 * math.pi * t / 6.0) + 0.15 * math.sin(2 * math.pi * t / 4.0 + 1.3)
        edge = min(1.0, t / 1.5, (seconds - t) / 1.5)
        out.append(low * swell * edge)
    return out


def write(name, samples, peak):
    """Normalises to peak (0..1 of full scale) and writes 16-bit mono PCM."""
    top = max(abs(s) for s in samples) or 1.0
    data = b"".join(struct.pack("<h", int(round(s / top * peak * 32767))) for s in samples)
    header = b"RIFF" + struct.pack("<I", 36 + len(data)) + b"WAVE"
    fmt = b"fmt " + struct.pack("<IHHIIHH", 16, 1, 1, RATE, RATE * 2, 2, 16)
    with open(os.path.join(OUT, name), "wb") as f:
        f.write(header + fmt + b"data" + struct.pack("<I", len(data)) + data)
    print(f"{name} {len(samples) / RATE:.2f} s")


def main():
    os.makedirs(OUT, exist_ok=True)
    write("hit.wav", hit(), 0.8)
    write("miss.wav", miss(), 0.5)
    write("crit.wav", crit(), 0.9)
    write("fall.wav", fall(), 0.85)
    write("click.wav", click(), 0.4)
    write("ambient.wav", ambient(), 0.35)


if __name__ == "__main__":
    main()
