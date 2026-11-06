"""Procedural audio. Every sound in the game is synthesised at startup.

If the mixer cannot be initialised (headless machines, no audio device) every
method becomes a no-op so the game still runs.
"""
from __future__ import annotations

import math

import numpy as np
import pygame

SR = 44100


def _t(dur, sr=SR):
    return np.arange(int(sr * dur), dtype=np.float64) / sr


def _env(n, attack=0.005, release=0.06, sr=SR):
    e = np.ones(n)
    a = max(1, int(attack * sr))
    r = max(1, int(release * sr))
    if a < n:
        e[:a] = np.linspace(0.0, 1.0, a)
    if r < n:
        e[-r:] *= np.linspace(1.0, 0.0, r)
    return e


def tone(freq, dur, wave="sine", vol=0.5, attack=0.005, release=0.06, sweep=None,
         sr=SR):
    t = _t(dur, sr)
    if sweep is None:
        phase = 2 * np.pi * freq * t
    else:
        phase = 2 * np.pi * np.cumsum(np.linspace(freq, sweep, len(t))) / sr
    if wave == "sine":
        w = np.sin(phase)
    elif wave == "tri":
        w = 2.0 / np.pi * np.arcsin(np.sin(phase))
    elif wave == "sq":
        w = np.sign(np.sin(phase))
    elif wave == "saw":
        w = 2.0 * ((phase / (2 * np.pi)) % 1.0) - 1.0
    else:
        w = np.sin(phase)
    return w * _env(len(t), attack, release, sr) * vol


def _box(x, k):
    """Fast box-filter (moving average) in O(n)."""
    k = max(1, int(k))
    if k <= 1:
        return x
    c = np.cumsum(np.insert(x, 0, 0.0))
    y = (c[k:] - c[:-k]) / k
    return np.pad(y, (0, max(0, len(x) - len(y))), mode="edge")[:len(x)]


def noise(dur, vol=0.35, attack=0.003, release=0.1, smooth=0, sr=SR, seed=None):
    rng = np.random.default_rng(seed)
    n = rng.uniform(-1.0, 1.0, int(sr * dur))
    for _ in range(smooth):
        n = _box(n, 24)
    return n * _env(len(n), attack, release, sr) * vol


def arp(freqs, step=0.09, wave="tri", vol=0.3, sr=SR, tail=0.35):
    total = step * len(freqs) + tail
    out = np.zeros(int(sr * total))
    for i, f in enumerate(freqs):
        s = tone(f, step + tail, wave=wave, vol=vol, attack=0.004,
                 release=step + tail * 0.9, sr=sr)
        start = int(i * step * sr)
        seg = s[:len(out) - start]
        out[start:start + len(seg)] += seg
    return out


def _sum(*arrs):
    n = max(len(a) for a in arrs)
    out = np.zeros(n)
    for a in arrs:
        out[:len(a)] += a
    return out


def _norm(x, peak=0.9):
    m = float(np.max(np.abs(x))) or 1.0
    return x / m * peak


def _loopify(x, fade=0.25, sr=SR):
    f = int(fade * sr)
    if f * 2 >= len(x):
        return x
    out = x[:-f].copy()
    ramp = np.linspace(0.0, 1.0, f)
    out[:f] = x[:f] * ramp + x[-f:] * (1.0 - ramp)
    return out


class Audio:
    def __init__(self):
        self.ok = False
        self.muted = False
        self.master = 0.85
        self.sounds = {}
        self.music = None
        self.waves = None
        self.music_channel = None
        self.waves_channel = None
        self._chirp_timer = 4.0
        if not pygame.mixer.get_init():
            try:
                pygame.mixer.init(SR, -16, 2, 512)
            except pygame.error:
                return
        self.ok = True
        try:
            self._build()
        except Exception as exc:  # audio is optional polish
            print("audio disabled:", exc)
            self.ok = False

    # ------------------------------------------------------------------ build
    def _mk(self, *arrs):
        arr = np.clip(np.nan_to_num(_sum(*arrs)), -1.0, 1.0)
        data = (arr * 32767.0).astype(np.int16)
        channels = pygame.mixer.get_init()[2] if pygame.mixer.get_init() else 2
        if channels > 1:
            data = np.repeat(data[:, None], channels, axis=1)
        return pygame.sndarray.make_sound(np.ascontiguousarray(data))

    def _build(self):
        sr = SR
        mk = self._mk
        snd = self.sounds
        snd["ui"] = mk(arp([760, 620], step=0.035, wave="sq", vol=0.10, sr=sr, tail=0.03))
        snd["cast"] = mk(noise(0.34, 0.30, attack=0.05, release=0.26, smooth=1, sr=sr),
                         tone(520, 0.32, "sine", 0.10, 0.05, 0.25, sweep=170, sr=sr))
        snd["plop"] = mk(tone(430, 0.22, "sine", 0.42, 0.002, 0.2, sweep=95, sr=sr),
                         noise(0.10, 0.20, attack=0.002, release=0.09, smooth=2, sr=sr))
        snd["nibble"] = mk(tone(880, 0.05, "sine", 0.14, 0.002, 0.045, sr=sr))
        snd["bite"] = mk(arp([760, 1080], step=0.055, wave="tri", vol=0.22, sr=sr, tail=0.08),
                         tone(1500, 0.05, "sine", 0.08, 0.001, 0.05, sr=sr))
        snd["hook"] = mk(tone(300, 0.16, "tri", 0.28, 0.002, 0.13, sweep=980, sr=sr))
        snd["reel"] = mk(noise(0.045, 0.16, attack=0.001, release=0.04, smooth=1, sr=sr),
                         tone(1600, 0.03, "sq", 0.03, 0.001, 0.03, sr=sr))
        snd["snap"] = mk(noise(0.30, 0.5, attack=0.001, release=0.28, smooth=0, sr=sr),
                         tone(300, 0.30, "saw", 0.18, 0.001, 0.28, sweep=55, sr=sr))
        snd["catch"] = mk(arp([523, 659, 784, 1047], step=0.075, wave="tri", vol=0.26,
                              sr=sr, tail=0.45))
        snd["legend"] = mk(arp([523, 659, 784, 988, 1319], step=0.08, wave="tri", vol=0.26,
                               sr=sr, tail=0.9),
                           noise(0.9, 0.05, 0.05, 0.8, smooth=3, sr=sr))
        snd["fail"] = mk(tone(240, 0.34, "tri", 0.26, 0.004, 0.32, sweep=105, sr=sr))
        snd["toke"] = mk(noise(0.75, 0.22, attack=0.30, release=0.20, smooth=2, sr=sr),
                         tone(120, 0.6, "sine", 0.05, 0.2, 0.3, sr=sr))
        snd["exhale"] = mk(noise(0.7, 0.18, attack=0.02, release=0.62, smooth=3, sr=sr))
        snd["cough"] = mk(arp([180, 140, 120], step=0.10, wave="saw", vol=0.10, sr=sr, tail=0.2))
        snd["buy"] = mk(arp([880, 1320], step=0.07, wave="sine", vol=0.18, sr=sr, tail=0.2))
        snd["error"] = mk(tone(190, 0.14, "sq", 0.10, 0.002, 0.12, sr=sr))
        snd["unlock"] = mk(arp([392, 523, 659, 784, 1047], step=0.085, wave="sine",
                               vol=0.22, sr=sr, tail=0.8))
        snd["travel"] = mk(noise(0.5, 0.16, attack=0.2, release=0.28, smooth=3, sr=sr),
                           tone(180, 0.5, "sine", 0.06, 0.2, 0.3, sweep=320, sr=sr))
        snd["chirp"] = mk(arp([2200, 2700, 2400], step=0.045, wave="sine", vol=0.05, sr=sr,
                              tail=0.02))
        snd["cricket"] = mk(arp([3600, 3600, 3600], step=0.035, wave="sine", vol=0.035,
                                sr=sr, tail=0.01))

        # ---- ambient loops (kept intentionally quiet) --------------------
        n = int(sr * 12.0)
        t = np.arange(n) / sr
        pad = np.zeros(n)
        voices = [(110.0, 0.55), (130.81, 0.34), (164.81, 0.30), (196.0, 0.24), (220.0, 0.14)]
        for i, (f, amp) in enumerate(voices):
            lfo = 0.65 + 0.35 * np.sin(2 * np.pi * t / (6.0 + i) + i * 1.7)
            pad += amp * lfo * (np.sin(2 * np.pi * f * t) + 0.22 * np.sin(4 * np.pi * f * t))
        pad = _norm(pad, 0.5) * 0.5

        wn = np.random.default_rng(7).uniform(-1, 1, n)
        for _ in range(4):
            wn = _box(wn, 40)
        swell = 0.45 + 0.55 * (0.5 + 0.5 * np.sin(2 * np.pi * t / 6.0 - 0.6))
        waves = _norm(wn * swell, 0.5)
        self.music = mk(_loopify(pad * 0.55, 0.4, sr))
        self.waves = mk(_loopify(waves * 0.55, 0.4, sr))
        pygame.mixer.set_reserved(2)
        self.music_channel = pygame.mixer.Channel(0)
        self.waves_channel = pygame.mixer.Channel(1)

    # ------------------------------------------------------------------ api
    def start_ambience(self):
        if not self.ok or self.music is None:
            return
        self.music_channel.play(self.music, loops=-1)
        self.waves_channel.play(self.waves, loops=-1)
        self.music_channel.set_volume(0.22 * self.master)
        self.waves_channel.set_volume(0.20 * self.master)

    def play(self, name, vol=1.0):
        if not self.ok or self.muted:
            return
        snd = self.sounds.get(name)
        if snd is None:
            return
        ch = pygame.mixer.find_channel(True)
        if ch is None:
            return
        ch.set_volume(max(0.0, min(1.0, vol * self.master)))
        ch.play(snd)

    def ambient_tick(self, dt, night):
        if not self.ok or self.muted:
            return
        self._chirp_timer -= dt
        if self._chirp_timer <= 0:
            self._chirp_timer = 5.0 + (np.random.random() * 12.0)
            self.play("cricket" if night else "chirp", 0.55)

    def set_muted(self, muted):
        self.muted = muted
        if not self.ok:
            return
        if self.music_channel:
            self.music_channel.set_volume(0.0 if muted else 0.22 * self.master)
        if self.waves_channel:
            self.waves_channel.set_volume(0.0 if muted else 0.20 * self.master)

    def toggle_mute(self):
        self.set_muted(not self.muted)
        return self.muted
