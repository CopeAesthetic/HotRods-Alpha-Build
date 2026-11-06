"""Screen base class and shared chrome."""
from __future__ import annotations

import pygame

from .. import config as C
from .. import ui
from .. import util as U


class Screen:
    overlay = False

    def __init__(self, app):
        self.app = app
        self.t = 0.0
        self.buttons = []
        self.mouse = (0, 0)

    @property
    def player(self):
        return self.app.player

    @property
    def world(self):
        return self.app.world

    @property
    def angler(self):
        return self.app.angler

    # lifecycle
    def on_enter(self):
        pass

    def on_exit(self):
        pass

    def handle_event(self, event):
        for b in self.buttons:
            if b.click(event, self.mouse):
                self.app.audio.play("ui", 0.6)
                if b.action:
                    b.action()
                return True
        return False

    def update(self, dt):
        self.t += dt
        self.mouse = self.app.mouse
        for b in self.buttons:
            b.update(self.mouse, dt)

    def draw(self, surf):
        pass

    # helpers
    def dim(self, surf, a=150):
        layer = pygame.Surface(surf.get_size(), pygame.SRCALPHA)
        layer.fill((6, 8, 16, a))
        surf.blit(layer, (0, 0))

    def panel_rect(self, w=1160, h=640):
        return pygame.Rect((C.W - w) // 2, (C.H - h) // 2, w, h)

    def draw_frame(self, surf, rect, title, subtitle=None, accent=C.CREAM):
        rect = pygame.Rect(rect)
        U.panel(surf, rect, C.PANEL_BG, radius=22, border=C.PANEL_EDGE,
                      border_w=2, alpha=246, shadow=True, shadow_alpha=120)
        # header rule
        pygame.draw.line(surf, (48, 54, 76), (rect.x + 34, rect.y + 78),
                         (rect.right - 34, rect.y + 78), 2)
        ui.txt(surf, title, (rect.x + 34, rect.y + 24), 38, True, accent,
               family="title")
        if subtitle:
            ui.txt(surf, subtitle, (rect.right - 78, rect.y + 38), 17, False,
                   C.TEXT_DIM, anchor="topright")
        return rect

    def add_close(self, rect, action=None):
        b = ui.Button((rect.right - 54, rect.y + 20, 34, 34), "X", style="dark",
                      size=17, action=action or self.app.pop)
        self.buttons.append(b)
        return b


def fmt_money(v):
    return "${:,}".format(int(v))


def fmt_time(seconds):
    m = int(seconds // 60)
    if m < 60:
        return "%dm" % m
    return "%dh %02dm" % (m // 60, m % 60)


def fmt_kg(kg):
    if kg >= 1:
        return "%.2f kg" % kg
    return "%d g" % int(round(kg * 1000))
