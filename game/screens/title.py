"""Title screen."""
from __future__ import annotations

import os

import math

import pygame

from .. import config as C
from .. import ui
from .. import util as U
from .. import world3d as W3D
from .base import Screen, fmt_time


class TitleScreen(Screen):
    def __init__(self, app):
        super().__init__(app)
        self.hour = 18.3
        self.enter = 0.0
        self.confirm_new = 0.0
        self._scrim = None
        self.scene = pygame.Surface((W3D.INTERNAL_W, W3D.INTERNAL_H), 0, 32, U._OPAQUE_MASKS)
        self.scanlines = self._scanlines()
        self.build()

    # ------------------------------------------------------------------ ui
    def build(self):
        self.buttons = []
        y = 414
        if self.app.has_save:
            self.buttons.append(ui.Button(
                (96, y, 336, 52), "TRIPS", style="primary", size=23,
                action=self.open_trips))
            y += 62
            self.buttons.append(ui.Button(
                (96, y, 336, 46), self.new_label(), style="dark", size=19,
                action=self.new_game))
        else:
            self.buttons.append(ui.Button(
                (96, y, 336, 52), "START FISHING", style="primary", size=23,
                action=self.resume))
        y += 62
        self.buttons.append(ui.Button((96, y, 336, 46), "HOW TO PLAY", style="dark",
                                      size=18, action=self.help))
        y += 56
        self.buttons.append(ui.Button((96, y, 336, 46), "QUIT", style="dark", size=18,
                                      action=self.app.quit))

    def open_trips(self):
        from .trips import TripsScreen
        self.app.push(TripsScreen(self.app))

    def new_label(self):
        if self.confirm_new > 0:
            return "REALLY? CLICK AGAIN"
        return "NEW GAME"

    def resume(self):
        from .play import PlayScreen
        self.app.new_world(self.player.spot)
        self.app.go(PlayScreen(self.app))

    def new_game(self):
        from ..app import slot_path
        if all(os.path.exists(slot_path(i)) for i in (1, 2, 3)):
            self.app.toast("All three trips are full - scrap one", C.GOLD, 2.6)
            self.open_trips()
            return
        if self.confirm_new <= 0:
            self.confirm_new = 4.0
            self.build()
            return
        self.app.new_game()

    def help(self):
        from .overlays import HelpScreen
        self.app.push(HelpScreen(self.app))

    # -------------------------------------------------------------- events
    def handle_event(self, event):
        super().handle_event(event)
        if event.type == pygame.KEYDOWN and event.key in (pygame.K_RETURN, pygame.K_SPACE):
            self.resume()

    def on_enter(self):
        self.build()
        self.enter = 0.0

    def update(self, dt):
        self.enter += dt
        if self.confirm_new > 0:
            self.confirm_new = max(0.0, self.confirm_new - dt)
            if self.confirm_new == 0:
                self.build()
        super().update(dt)
        w = self.world
        w.update(dt, self.hour, 0.0)
        w.rod_angle = -0.58 + math.sin(self.t * 0.4) * 0.02
        w.rod_bend = 0.12

    # ---------------------------------------------------------------- draw
    def draw(self, surf):
        w = self.world
        w.draw(self.scene, "idle", 0.62, 0.0)
        surf.blit(pygame.transform.scale(self.scene, (C.W, C.H)), (0, 0))
        U.grain(surf, 7, 2)

        if self._scrim is None:
            self._scrim = self._make_scrim()
        surf.blit(self._scrim, (0, 0))

        # title block, with a tiny parallax
        mx, my = self.app.mouse
        px = (mx - C.W / 2) * 0.012
        py = (my - C.H / 2) * 0.008
        x0 = 90 + px
        y0 = 96 + py

        ui.txt(surf, "HOT", (x0, y0), 94, True, C.CREAM, family="title",
               shadow=(8, 10, 20))
        ui.txt(surf, "RODS", (x0, y0 + 92), 94, True, C.CREAM, family="title",
               shadow=(8, 10, 20))
        ui.txt(surf, "a stoned fishing collect-a-thon", (x0 + 8, y0 + 214), 26, False,
               (240, 214, 168))
        pygame.draw.line(surf, C.GOLD_DEEP, (x0 + 8, y0 + 258), (x0 + 372, y0 + 258), 3)
        ui.txt(surf, "36 species  ·  4 waters  ·  1 very relaxed angler",
               (x0 + 8, y0 + 270), 16, False, (200, 192, 174))

        for b in self.buttons:
            b.draw(surf, self.mouse)

        # footer
        ui.txt(surf, "M  mute      F11  fullscreen", (96, C.H - 42), 15, False,
               (176, 172, 160), alpha=200)
        if not self.app.audio.muted and self.app.audio.ok:
            pass
        ui.txt(surf, "pygame · v1.0", (C.W - 32, C.H - 42), 15, False,
               (176, 172, 160), anchor="topright", alpha=200)

    def _scanlines(self):
        s = pygame.Surface((C.W, C.H), pygame.SRCALPHA)
        for y in range(0, C.H, 2):
            pygame.draw.line(s, (0, 0, 0, 18), (0, y), (C.W, y))
        return s

    def _make_scrim(self):
        s = pygame.Surface((C.W, C.H), pygame.SRCALPHA)
        for x in range(0, 820):
            a = int(210 * (1 - x / 820.0) ** 1.5)
            pygame.draw.line(s, (7, 9, 18, a), (x, 0), (x, C.H))
        veil = pygame.Surface((C.W, C.H), pygame.SRCALPHA)
        veil.fill((6, 8, 16, 26))
        s.blit(veil, (0, 0))
        return s
