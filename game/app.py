"""Application shell: window, screen stack, main loop, save handling."""
from __future__ import annotations

import os
import sys

import pygame

from . import config as C
from . import ui
from .audio import Audio
from .console import Console
from .fishing import Angler
from .player import SAVE_PATH, Player


def slot_path(i):
    return SAVE_PATH if i == 1 else SAVE_PATH.replace(".json", "%d.json" % i)
from .screens.base import Screen
from .screens.title import TitleScreen
from .world3d import World


# A 32-bit surface with no alpha channel. The cocoa display surface DOES have an
# alpha channel, so a converted canvas would have one too - and pygame would then
# treat every blit as alpha-blended, dropping most of the frame.
_OPAQUE_MASKS = (0xFF0000, 0xFF00, 0xFF, 0)


class App:
    def __init__(self):
        pygame.mixer.pre_init(44100, -16, 2, 512)
        pygame.init()
        pygame.display.set_caption(C.CAPTION)
        self.screen = pygame.display.set_mode((C.W, C.H), pygame.RESIZABLE)
        # 24-bit on purpose: the cocoa display surface has an alpha channel, and a
        # canvas with alpha masks makes blits drop alpha-blended content.
        self.canvas = pygame.Surface((C.W, C.H), 0, 32, _OPAQUE_MASKS)
        self.clock = pygame.time.Clock()
        self.running = True
        self.time = 0.0
        self.mouse = (0, 0)
        self.view = (0, 0, 1.0)
        self.fullscreen = False
        self.fade = 0.0
        self.fade_max = 0.26
        self.fade_surf = None
        self.toasts = []

        self.debug = {"show": False, "instacatch": False, "nolinesnap": False,
                      "wire": False}
        self.console = Console(self)
        self.audio = Audio()
        self.player = Player.load()
        self.audio.set_muted(self.player.muted)
        self.audio.start_ambience()

        self.world = World(self.player.spot)
        self.angler = Angler(self.player, self.world, self.debug)
        self.has_save = os.path.exists(SAVE_PATH)

        self.stack = []
        self.push(TitleScreen(self))

    # ------------------------------------------------------------- screens
    def push(self, screen):
        if self.stack:
            self.stack[-1].on_exit()
        self.stack.append(screen)
        screen.on_enter()

    def pop(self):
        if len(self.stack) <= 1:
            return
        self.stack.pop().on_exit()
        self.stack[-1].on_enter()

    def replace(self, screen):
        while self.stack:
            self.stack.pop().on_exit()
        self.push(screen)

    def go(self, screen, fade=True):
        if fade:
            self.fade_surf = self.canvas.copy()
            self.fade = 1.0
        self.replace(screen)

    # ---------------------------------------------------------------- game
    def save(self):
        self.player.save()
        self.has_save = any(os.path.exists(slot_path(i)) for i in (1, 2, 3))

    def new_world(self, spot=None):
        if spot is not None:
            self.player.spot = spot
        self.world = World(self.player.spot)
        self.angler = Angler(self.player, self.world, self.debug)

    def resume(self):
        from .screens.play import PlayScreen
        self.new_world(self.player.spot)
        self.go(PlayScreen(self))

    def load_slot(self, i):
        from .player import Player
        self.player = Player.load(slot_path(i))
        self.has_save = True

    def start_slot(self, i):
        from .player import Player
        self.player = Player()
        self.player.path = slot_path(i)
        self.player.save()
        self.has_save = True

    def new_game(self):
        free = next((i for i in (1, 2, 3) if not os.path.exists(slot_path(i))), None)
        if free is None:
            return False
        self.start_slot(free)
        self.audio.set_muted(self.player.muted)
        self.resume()
        return True

    def to_title(self):
        from .screens.title import TitleScreen
        self.save()
        self.go(TitleScreen(self))

    # --------------------------------------------------------------- toast
    def toast(self, text, color=C.CREAM, life=2.6):
        self.toasts.append([text, color, life, life])
        if len(self.toasts) > 4:
            self.toasts.pop(0)

    # --------------------------------------------------------------- input
    def to_virtual(self, pos):
        ox, oy, scale = self.view
        return ((pos[0] - ox) / scale, (pos[1] - oy) / scale)

    def toggle_fullscreen(self):
        self.fullscreen = not self.fullscreen
        if self.fullscreen:
            self.screen = pygame.display.set_mode((0, 0), pygame.FULLSCREEN)
        else:
            self.screen = pygame.display.set_mode((C.W, C.H), pygame.RESIZABLE)

    def quit(self):
        self.save()
        self.running = False

    # ---------------------------------------------------------------- loop
    def run(self):
        while self.running:
            dt = min(0.05, self.clock.tick(C.FPS) / 1000.0)
            self.time += dt
            for event in pygame.event.get():
                if event.type == pygame.KEYDOWN and event.key == pygame.K_BACKQUOTE:
                    self.console.toggle()
                    continue
                if self.console.open:
                    self.console.handle_event(event)
                    continue
                if event.type == pygame.QUIT:
                    self.quit()
                elif event.type == pygame.VIDEORESIZE and not self.fullscreen:
                    self.screen = pygame.display.set_mode((max(640, event.w),
                                                           max(400, event.h)),
                                                          pygame.RESIZABLE)
                elif event.type == pygame.KEYDOWN and event.key == pygame.K_F11:
                    self.toggle_fullscreen()
                elif event.type == pygame.KEYDOWN and event.key == pygame.K_m:
                    self.player.muted = self.audio.toggle_mute()
                else:
                    self.mouse = self.to_virtual(pygame.mouse.get_pos())
                    self.stack[-1].handle_event(event)
            self.mouse = self.to_virtual(pygame.mouse.get_pos())
            if not self.running:
                break
            self.stack[-1].update(dt)
            self.console.update(dt)
            self.update_toasts(dt)
            if self.fade > 0.0:
                self.fade = max(0.0, self.fade - dt)
                if self.fade == 0.0:
                    self.fade_surf = None
            self.draw()
        pygame.quit()

    def update_toasts(self, dt):
        live = []
        for t in self.toasts:
            t[2] -= dt
            if t[2] > 0:
                live.append(t)
        self.toasts = live

    def draw(self):
        top = self.stack[-1]
        if top.overlay and len(self.stack) > 1:
            self.stack[-2].draw(self.canvas)
        top.draw(self.canvas)
        self.draw_toasts(self.canvas)
        if self.debug["show"]:
            self.draw_debug()
        self.console.draw(self.canvas)
        self.present()

    def draw_toasts(self, surf):
        y = 122
        for i, (text, color, life, total) in enumerate(reversed(self.toasts)):
            t = life / total
            a = 255 if t > 0.25 else int(255 * t / 0.25)
            rise = int((1.0 - min(1.0, (total - life) * 5)) * -14)
            fnt = ui.font(24, True)
            w = fnt.size(text)[0] + 44
            rect = pygame.Rect((C.W - w) // 2, y + i * 44 + rise, w, 38)
            layer = pygame.Surface((w, 38), pygame.SRCALPHA)
            pygame.draw.rect(layer, (16, 19, 30, int(a * 0.88)), (0, 0, w, 38),
                             border_radius=19)
            pygame.draw.rect(layer, (color[0], color[1], color[2], a), (0, 0, w, 38),
                             width=2, border_radius=19)
            surf.blit(layer, rect.topleft)
            ui.txt(surf, text, rect.center, 22, True, color, "center", alpha=a)

    def draw_debug(self):
        c = self.canvas
        p = self.player
        a = getattr(self, "angler", None)
        lines = [
            "fps %4.0f   scr %dx%d" % (self.clock.get_fps(), C.W, C.H),
            "spot %d  %s   hour %.1f" % (p.spot, p.spot_data["name"], p.hour),
            "cash %d  pts %d  bag %d/%d  weed %s" % (
                p.cash, p.pts, len(p.bag), p.bag_size, p.weed or "-"),
            "found %d/%d  tokes %d  casts %d" % (p.found_count, p.total_count,
                                                 p.tokes, p.casts),
        ]
        if a is not None:
            lines.append("angler %-9s tension %.2f band %.2f prog %.2f" % (
                a.state, getattr(a, "tension", 0.0), getattr(a, "band", 0.0),
                getattr(a, "progress", 0.0)))
        lines.append("polys %d  flags %s" % (
            len(getattr(self.world.scene, "polys", [])),
            "".join(k[0] for k, v in self.debug.items() if v) or "-"))
        w = 430
        h = 18 * len(lines) + 14
        layer = pygame.Surface((w, h), pygame.SRCALPHA)
        layer.fill((4, 10, 6, 210))
        pygame.draw.rect(layer, (50, 110, 60), layer.get_rect(), width=1)
        c.blit(layer, (12, 12))
        for i, line in enumerate(lines):
            ui.txt(c, line, (22, 19 + i * 18), 13, False, (150, 230, 160),
                   family="mono", shadow=None)

    def present(self):
        sw, sh = self.screen.get_size()
        scale = min(sw / float(C.W), sh / float(C.H))
        dw, dh = int(C.W * scale), int(C.H * scale)
        ox, oy = (sw - dw) // 2, (sh - dh) // 2
        self.view = (ox, oy, scale)
        self.screen.fill((0, 0, 0))
        if self.fade > 0 and self.fade_surf is not None:
            snap = self.fade_surf
            snap.set_alpha(int(255 * (self.fade / self.fade_max)))
            self.canvas.blit(snap, (0, 0))
        if (dw, dh) == (C.W, C.H):
            out = self.canvas
        else:
            out = pygame.transform.smoothscale(self.canvas, (dw, dh))
        self.screen.blit(out, (ox, oy))
        pygame.display.flip()


def main():
    app = App()
    app.run()


if __name__ == "__main__":
    main()
