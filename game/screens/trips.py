"""Trips: the save browser shown from the title screen."""
from __future__ import annotations

import json
import os

import pygame

from .. import config as C
from .. import data
from .. import ui
from .. import util as U
from .base import Screen, fmt_money


from ..app import slot_path


def slot_info(i):
    p = slot_path(i)
    if not os.path.exists(p):
        return None
    try:
        d = json.load(open(p))
    except Exception:
        return None
    have = d.get("species") or {}
    return {
        "level": 1 + sum(1 for t in (120, 320, 620, 1050, 1650, 2450, 3500, 4850, 6500)
                         if d.get("xp", 0) >= t),
        "cash": d.get("cash", 0), "vp": d.get("pts", 0),
        "spot": d.get("spot", 0), "species": len(have),
        "mins": int(d.get("played", 0)) // 60,
        "rawname": data.SPOTS[min(d.get("spot", 0), 3)]["name"],
        "raw": d, "path": p,
    }


class TripsScreen(Screen):
    overlay = True

    def __init__(self, app):
        super().__init__(app)
        self.confirm = 0
        self.build()

    def build(self):
        self.buttons = []
        self.rect = self.panel_rect(880, 520)
        r = self.rect
        self.add_close(r)
        for i in (1, 2, 3):
            info = slot_info(i)
            y = r.y + 128 + (i - 1) * 108
            if info:
                self.buttons.append(ui.Button(
                    (r.right - 340, y + 24, 140, 40), "BOARD", style="primary",
                    size=15, action=lambda s=i: self.board(s)))
                self.buttons.append(ui.Button(
                    (r.right - 190, y + 24, 150, 40),
                    "SURE?" if self.confirm == i else "SCRAP", style="danger",
                    size=15, action=lambda s=i: self.scrap(s)))
            else:
                self.buttons.append(ui.Button(
                    (r.right - 190, y + 24, 150, 40), "NEW TRIP", style="dark",
                    size=15, action=lambda s=i: self.new_trip(s)))

    def new_trip(self, i):
        self.app.start_slot(i)
        from .play import PlayScreen
        self.app.new_world(0)
        self.app.go(PlayScreen(self.app))

    def scrap(self, i):
        if self.confirm != i:
            self.confirm = i
            self.build()
            return
        try:
            os.remove(slot_path(i))
        except OSError:
            pass
        self.confirm = 0
        self.app.has_save = any(os.path.exists(slot_path(k)) for k in (1, 2, 3))
        self.build()

    def board(self, i):
        self.app.load_slot(i)
        from .play import PlayScreen
        self.app.new_world(self.player.spot)
        self.app.go(PlayScreen(self.app))

    def handle_event(self, event):
        if super().handle_event(event):
            return
        if event.type == pygame.KEYDOWN and event.key == pygame.K_ESCAPE:
            self.app.pop()

    def draw(self, surf):
        self.dim(surf, 175)
        r = self.rect
        self.draw_frame(surf, r, "TRIPS", "pick a save")
        for i in (1, 2, 3):
            info = slot_info(i)
            y = r.y + 128 + (i - 1) * 108
            box = pygame.Rect(r.x + 34, y, r.w - 68, 92)
            U.panel(surf, box, (28, 32, 47), radius=12, border=(46, 52, 72))
            ui.txt(surf, "TRIP %d" % i, (box.x + 20, box.y + 14), 15, True,
                   C.GOLD if info else C.TEXT_DIM)
            if not info:
                ui.txt(surf, "empty slot", (box.x + 20, box.y + 42), 14, False,
                       C.TEXT_DIM)
                continue
            ui.txt(surf, info["rawname"], (box.x + 20, box.y + 40), 19, True, C.CREAM)
            ui.txt(surf, "LV %d  ·  %s  ·  %d/40 fish  ·  %dm" % (
                info["level"], fmt_money(info["cash"]), info["species"],
                info["mins"]), (box.x + 20, box.y + 66), 12, False, C.TEXT_DIM)
        for b in self.buttons:
            b.draw(surf, self.mouse)
        ui.txt(surf, "ESC closes", (r.centerx, r.bottom - 30), 12, False,
               C.TEXT_DIM, anchor="midtop")
