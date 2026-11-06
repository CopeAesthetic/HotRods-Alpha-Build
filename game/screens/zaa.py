"""The Zaa menu: pick your weed, pick your smoking method, press concentrate."""
from __future__ import annotations

import pygame

from .. import config as C
from .. import data
from .. import ui
from .. import util as U
from .base import Screen, fmt_money


class ZaaScreen(Screen):
    overlay = True

    def __init__(self, app):
        super().__init__(app)
        self.build()

    def build(self):
        self.buttons = []
        self.rect = self.panel_rect(820, 580)
        r = self.rect
        self.add_close(r)
        p = self.player

        strains = [s for s in data.STRAINS
                   if p.weed_count(s["id"]) or p.concentrate.get(s["id"])]
        for i, st in enumerate(strains):
            y = r.y + 112 + i * 52
            b = ui.Button((r.x + 28, y, 150, 40), st["name"], style="tab", size=13,
                          action=lambda sid=st["id"]: self.pick_strain(sid))
            b.selected = (p.strain == st["id"])
            self.buttons.append(b)
            if "hot_press" in p.smokers:
                self.buttons.append(ui.Button(
                    (r.x + 310, y + 4, 64, 32), "PRESS", style="primary", size=11,
                    enabled=p.weed_count(st["id"]) >= 4,
                    action=lambda sid=st["id"]: self.press(sid)))

        smokes = [s for s in data.SMOKERS if s["id"] in p.smokers]
        for i, sm in enumerate(smokes):
            y = r.y + 112 + i * 52
            b = ui.Button((r.x + 420, y, 220, 40), sm["name"], style="tab", size=13,
                          action=lambda sid=sm["id"]: self.pick_smoker(sid))
            b.selected = (p.smoker == sm["id"])
            self.buttons.append(b)

    def pick_strain(self, sid):
        p = self.player
        if p.weed_count(sid) > 0 or p.concentrate.get(sid, 0) > 0:
            p.strain = sid
            p.dab_mode = bool(p.smoker_data.get("conc"))
            self.app.save()
            self.build()

    def pick_flower(self, sid):
        self.player.strain = sid
        self.player.dab_mode = False
        self.app.save()
        self.build()

    def pick_dab(self, sid):
        self.player.strain = sid
        self.player.dab_mode = True
        self.app.save()
        self.build()

    def pick_smoker(self, sid):
        self.player.smoker = sid
        self.player.dab_mode = (sid == "dab_rig")
        self.app.save()
        self.build()

    def press(self, sid):
        ok, msg = self.player.press_strain(sid)
        if ok:
            self.app.audio.play("buy", 0.8)
            self.app.toast(msg, C.GOLD)
        else:
            self.app.audio.play("error", 0.5)
            self.app.toast(msg, (240, 150, 120))
        self.build()

    def handle_event(self, event):
        if super().handle_event(event):
            return
        if event.type == pygame.KEYDOWN and event.key in (pygame.K_z, pygame.K_ESCAPE):
            self.app.pop()

    def draw(self, surf):
        p = self.player
        self.dim(surf, 165)
        r = self.rect
        self.draw_frame(surf, r, "ZAA", None)
        for b in self.buttons:
            b.draw(surf, self.mouse)
        ui.txt(surf, "your stash", (r.x + 28, r.y + 86), 13, True, (168, 220, 160))
        ui.txt(surf, "how you smoke it", (r.x + 420, r.y + 86), 13, True, C.GOLD)

        strains = [s for s in data.STRAINS
                   if p.weed_count(s["id"]) or p.concentrate.get(s["id"])]
        if not strains:
            ui.txt(surf, "Nothing in the stash. Trade fish at the shop.", (r.x + 28, r.y + 120),
                   15, False, C.TEXT_DIM)
        for i, st in enumerate(strains):
            y = r.y + 112 + i * 52
            ui.txt(surf, "x%d g" % p.weed_count(st["id"]), (r.x + 380, y + 12), 13, True,
                   (200, 210, 190))
            c = p.concentrate.get(st["id"], 0)
            if c:
                ui.txt(surf, "%d dab" % c, (r.x + 380, y + 26), 11, False, (250, 210, 120))

        smokes = [s for s in data.SMOKERS if s["id"] in p.smokers]
        for i, sm in enumerate(smokes):
            y = r.y + 112 + i * 52
            ui.txt(surf, sm["desc"][:34], (r.x + 420, y + 42), 10, False, C.TEXT_DIM)
            if sm["id"] == p.smoker:
                ui.txt(surf, "IN USE", (r.x + 420, y - 14), 10, True, C.GOLD)

        ui.txt(surf, "Z or ESC closes", (r.centerx, r.bottom - 34), 12, False,
               C.TEXT_DIM, anchor="midtop")
