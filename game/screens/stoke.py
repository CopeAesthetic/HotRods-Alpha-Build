"""Stoke: the skill tree - three lanes, one point per level."""
from __future__ import annotations

import pygame

from .. import config as C
from .. import ui
from ..ui import font
from .. import util as U
from .base import Screen

LANES = (
    ("fish", "FISHING", (120, 190, 130),
     "+1.2% hit arc per rank", "Wider green arc on every pull."),
    ("smoke", "SMOKING", (150, 210, 236),
     "+6% chill fill per rank", "Every toke fills more of the bar."),
    ("vibes", "VIBES", (214, 150, 236),
     "+5% high length per rank", "The good mood lasts longer."),
)
MAX_RANK = 8


class StokeScreen(Screen):
    overlay = True

    def __init__(self, app):
        super().__init__(app)
        self.build()

    def build(self):
        self.buttons = []
        self.rect = self.panel_rect(1000, 560)
        r = self.rect
        self.add_close(r)
        for i, (key, title, col, eff, blurb) in enumerate(LANES):
            x = r.x + 30 + i * 318
            self.buttons.append(ui.Button(
                (x + 24, r.y + 386, 240, 40),
                "SPEND" if self.player.stoke[key] < MAX_RANK else "MAXED",
                style="primary" if self.player.stoke_pts > 0 else "dark",
                size=15,
                enabled=self.player.stoke_pts > 0 and self.player.stoke[key] < MAX_RANK,
                action=lambda k=key: self.spend(k)))

    def spend(self, key):
        ok, msg = self.player.spend_stoke(key)
        if ok:
            self.app.audio.play("buy", 0.8)
            self.app.toast(msg, C.GOLD)
            self.app.save()
        else:
            self.app.audio.play("error", 0.5)
            self.app.toast(msg, (240, 150, 120))
        self.build()

    def handle_event(self, event):
        if super().handle_event(event):
            return
        if event.type == pygame.KEYDOWN and event.key in (pygame.K_k, pygame.K_ESCAPE):
            self.app.pop()

    def draw(self, surf):
        p = self.player
        self.dim(surf, 170)
        r = self.rect
        self.draw_frame(surf, r, "STOKE", "level %d  ·  %d xp  ·  %d points"
                        % (p.level, p.xp, p.stoke_pts))
        for i, (key, title, col, eff, blurb) in enumerate(LANES):
            x = r.x + 30 + i * 318
            box = pygame.Rect(x, r.y + 120, 288, 340)
            U.panel(surf, box, (28, 32, 47), radius=14, border=(46, 52, 72))
            ui.txt(surf, title, (box.x + 24, box.y + 22), 20, True, col)
            rank = p.stoke[key]
            for j in range(MAX_RANK):
                px = box.x + 24 + j * 30
                py = box.y + 60
                filled = j < rank
                pygame.draw.circle(surf, col if filled else (44, 48, 62),
                                   (px + 9, py + 9), 9)
                pygame.draw.circle(surf, (18, 20, 30), (px + 9, py + 9), 9, 1)
            ui.txt(surf, "%d / %d" % (rank, MAX_RANK), (box.right - 24, box.y + 60),
                   13, True, C.TEXT_DIM, anchor="topright")
            ui.txt(surf, eff, (box.x + 24, box.y + 104), 13, True, (196, 202, 220))
            U.text_wrapped(surf, blurb, (box.x + 24, box.y + 128), ui.font(12),
                           C.TEXT_DIM, 240, line_gap=1, max_lines=3)
        for b in self.buttons:
            b.draw(surf, self.mouse)
        ui.txt(surf, "one stoke point per level  ·  K or ESC closes",
               (r.centerx, r.bottom - 34), 12, False, C.TEXT_DIM, anchor="midtop")
