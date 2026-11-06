"""Pause menu and the how-to-play screen."""
from __future__ import annotations

import pygame

from .. import config as C
from .. import data
from .. import ui
from .. import util as U
from ..util import font
from .base import Screen, fmt_money, fmt_time, fmt_kg


class PauseScreen(Screen):
    overlay = True

    def __init__(self, app):
        super().__init__(app)
        self.confirm_reset = 0.0
        self.build()

    def build(self):
        self.buttons = []
        r = self.panel_rect(560, 620)
        self.rect = r
        y = r.y + 266
        items = [
            ("RESUME", "primary", self.app.pop),
            ("JOURNAL", "dark", self.journal),
            ("POV: " + self.player_pov_label(), "dark", self.cycle_pov),
            ("HOW TO PLAY", "dark", self.help),
        ]
        for label, style, action in items:
            self.buttons.append(ui.Button((r.x + 40, y, r.w - 80, 44), label,
                                          style=style, size=17, action=action))
            y += 50
        self.buttons.append(ui.Button(
            (r.x + 40, r.bottom - 92, 180, 34),
            "SOUND: " + ("OFF" if self.app.audio.muted else "ON"), style="dark",
            size=14, action=self.toggle_mute))
        self.buttons.append(ui.Button(
            (r.right - 220, r.bottom - 92, 180, 34), "SAVE & QUIT",
            style="danger", size=14, action=self.quit))

    def player_pov_label(self):
        return {"third": "THIRD PERSON", "first": "FIRST PERSON",
                "free": "FREECAM"}.get(self.world.pov, "THIRD PERSON")

    def cycle_pov(self):
        order = ("third", "first", "free")
        cur = self.world.pov
        self.world.pov = order[(order.index(cur) + 1) % 3]
        self.app.player.pov = self.world.pov
        self.app.save()
        self.build()

    def journal(self):
        self.app.pop()
        if hasattr(self.app.stack[-1], "open_journal"):
            self.app.stack[-1].open_journal()

    def shop(self):
        self.app.pop()
        if hasattr(self.app.stack[-1], "open_shop"):
            self.app.stack[-1].open_shop()

    def help(self):
        self.app.push(HelpScreen(self.app))

    def quit(self):
        if self.confirm_reset > 0:
            return
        self.app.to_title()

    def toggle_mute(self):
        self.app.player.muted = self.app.audio.toggle_mute()
        self.app.save()
        self.build()

    def reset_save(self):
        if self.confirm_reset <= 0:
            self.confirm_reset = 5.0
            self.build()
            return
        self.app.player.delete_save()
        self.app.player = type(self.app.player)()
        self.app.audio.set_muted(False)
        self.app.has_save = False
        self.app.new_world(0)
        self.app.to_title()

    def handle_event(self, event):
        if super().handle_event(event):
            return
        if event.type == pygame.KEYDOWN and event.key == pygame.K_ESCAPE:
            self.app.pop()

    def update(self, dt):
        super().update(dt)
        if self.confirm_reset > 0:
            self.confirm_reset = max(0.0, self.confirm_reset - dt)
            if self.confirm_reset == 0:
                self.build()

    def draw(self, surf):
        self.dim(surf, 170)
        r = self.rect
        p = self.player
        self.draw_frame(surf, r, "PAUSED", None)

        # stat grid
        stats = [
            ("TIME AFLOAT", fmt_time(p.played)),
            ("CASTS", str(p.casts)),
            ("FISH LANDED", str(p.catches)),
            ("SPECIES", "%d / %d" % (p.found_count, p.total_count)),
            ("VIBE POINTS", str(p.pts)),
            ("COOLER", "%d / %d" % (len(p.bag), p.bag_size)),
        ]
        for i, (label, value) in enumerate(stats):
            col, row = i % 2, i // 2
            x = r.x + 42 + col * 250
            y = r.y + 98 + row * 42
            ui.txt(surf, label, (x, y), 12, True, C.TEXT_DIM)
            ui.txt(surf, value, (x, y + 15), 20, True, C.CREAM)
        if p.best_catch:
            kg, sid = p.best_catch
            sp = data.species(sid)
            ui.txt(surf, "BEST CATCH", (r.x + 42, r.y + 222), 12, True, C.TEXT_DIM)
            ui.txt(surf, "%s  ·  %s" % (sp["name"], fmt_kg(kg)), (r.x + 42, r.y + 237),
                   15, True, C.RARITY_COLORS[sp["rarity"]])

        for b in self.buttons:
            b.draw(surf, self.mouse)


class HelpScreen(Screen):
    overlay = True

    def __init__(self, app):
        super().__init__(app)
        self.build()

    def build(self):
        self.buttons = []
        r = self.panel_rect(980, 632)
        self.rect = r
        self.buttons.append(ui.Button((r.centerx - 100, r.bottom - 68, 200, 44),
                                      "GOT IT", style="primary", size=18,
                                      action=self.app.pop))

    def handle_event(self, event):
        if super().handle_event(event):
            return
        if event.type == pygame.KEYDOWN and event.key in (pygame.K_ESCAPE,
                                                          pygame.K_RETURN,
                                                          pygame.K_SPACE):
            self.app.pop()

    def draw(self, surf):
        self.dim(surf, 175)
        r = self.rect
        self.draw_frame(surf, r, "HOW TO PLAY", "Hot Rods")
        for b in self.buttons:
            b.draw(surf, self.mouse)

        lx, ly = r.x + 40, r.y + 106
        ui.txt(surf, "THE LOOP", (lx, ly), 15, True, C.GOLD)
        steps = [
            ("CAST", "Hold SPACE to build power, release to cast. Deeper casts "
                     "find better fish; shallow water is commons."),
            ("BITE", "A nibble is a fake-out. A real bite dunks the float - tap "
                     "SPACE then to strike."),
            ("REEL", "A marker sweeps the rope. SPACE as it crosses: blue centre "
                     "adds 3, green adds 1, red takes 2 - drop below zero and "
                     "the fish is gone. Harder fish need more pulls."),
            ("WEED", "E tokes. Sober you only catch commons; better strains let "
                     "rarer fish bite at all. Z opens ZAA to pick a strain."),
            ("ICE", "The cooler needs ice - $15 buys 5 minutes. Without it fish "
                    "spoil and sell for less."),
            ("SHOP", "B opens BUY and SELL. Trade fish for VP to unlock jars; "
                     "cash buys gear."),
        ]
        y = ly + 26
        for tag, body in steps:
            ui.chip(surf, tag, (lx, y), C.CREAM, 11, bg_alpha=26)
            h = U.text_wrapped(surf, body, (lx + 82, y + 2), font(14),
                               (198, 202, 216), 328, line_gap=3)
            y += max(h, 30) + 12

        keys = [("SPACE", "cast / pull"), ("E", "smoke"), ("Z", "zaa"),
                ("K", "stoke"), ("B", "shop"), ("A", "areas"), ("J", "journal"),
                ("ESC", "pause")]
        kx = r.centerx + 122
        ky = r.bottom - 102
        for i, (k, label) in enumerate(keys):
            col, row = i % 2, i // 2
            x = kx + col * 138
            yy = ky + row * 24
            w = ui.keycap(surf, (x, yy), k, 11)
            ui.txt(surf, label, (x + w + 6, yy + 2), 11, False, (172, 176, 192))

        # right column
        rx = r.x + 520
        ui.txt(surf, "THE CHILL", (rx, ly), 15, True, (168, 220, 160))
        chill = [
            "So what you smoke decides what bites. Sober: commons only. "
            "Bluegill Bud adds uncommons; it takes a top jar to reach legendaries.",
            "Every strain has a band - higher jars stop giving you the junk. "
            "Read the jar rows in ZAA to see what each one opens.",
            "The pond's Sunfish Skunk on a Backwoods, smoked to a full bar, "
            "arms one guaranteed legendary.",
            "Press 4 g of any strain into a dab with the Hot Press, then smoke it "
            "in the Dab Rig: potency 7 and every fish in the game is on the line.",
            "K opens STOKE - one point per level, split across fishing, smoking "
            "and vibes.",
            "Some fish only bite at night, some only high. The journal hints at "
            "who wants what, and the LEGENDS tab hides the rest.",
        ]
        y = ly + 26
        for line in chill:
            pygame.draw.circle(surf, (150, 200, 140), (rx + 4, y + 8), 3)
            h = U.text_wrapped(surf, line, (rx + 18, y), font(14), (198, 202, 216),
                               410, line_gap=3)
            y += h + 10

