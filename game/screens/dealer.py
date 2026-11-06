"""Sal's shack: sell fish for cash, trade them for Vibe Points, buy weed."""
from __future__ import annotations

import math

import pygame

from .. import config as C
from .. import data
from .. import ui
from .. import util as U
from ..util import clamp, mix, font, pulse
from .base import Screen, fmt_kg, fmt_money

GRID_X = 30
GRID_Y = 150
CARD_W, CARD_H = 176, 96
CARD_GX, CARD_GY = 10, 8
COLS = 4

JAR_X = 800
JAR_Y = 150
JAR_W = 370
JAR_H = 104
JAR_GAP = 6

TIER_WORD = {0: "common", 1: "uncommon", 2: "rare", 3: "legendary", 4: "mythic"}


class DealerScreen(Screen):
    overlay = True

    def __init__(self, app):
        super().__init__(app)
        self.mode = "cash"
        self.jar_area = app.player.spot if app.player.spot in app.player.unlocked else 0
        self.say = ""
        self.say_t = 0.0
        self.sell_anim = 0.0
        self.build()

    # ------------------------------------------------------------------ ui
    def build(self):
        self.buttons = []
        self.rect = self.panel_rect(1200, 680)
        r = self.rect
        self.add_close(r)
        # shop tabs, top-left under the title
        self.buttons.append(ui.Button((r.x + 170, r.y + 26, 96, 34), "BUY",
                                      style="tab", size=13, action=self.open_buy))
        sell = ui.Button((r.x + 276, r.y + 26, 96, 34), "SELL", style="tab",
                         size=13, action=None)
        sell.selected = True
        self.buttons.append(sell)
        # mode tabs
        for i, (key, label) in enumerate((("cash", "CASH"), ("pts", "VP"))):
            b = ui.Button((r.x + 560 + i * 106, r.y + 112, 100, 32), label,
                          style="tab", size=14, action=lambda k=key: self.set_mode(k))
            b.selected = (self.mode == key)
            self.buttons.append(b)
        # bulk action
        label, style, enabled = self._bulk_button()
        self.buttons.append(ui.Button((r.x + 30, r.y + 566, 300, 38), label,
                                      style=style, size=15, enabled=enabled,
                                      action=self.bulk))
        # area tabs
        for i in range(4):
            label = (data.SPOTS[i]["name"].split()[-1].upper()
                     if i in self.player.unlocked else "???")
            b = ui.Button((r.x + JAR_X + i * 92, r.y + 108, 86, 30), label,
                          style="tab", size=11, action=lambda a=i: self.set_area(a))
            b.selected = (self.jar_area == i)
            b.enabled = (i in self.player.unlocked)
            self.buttons.append(b)
        # jars
        for i, st in enumerate(self.jar_rows()):
            y = r.y + JAR_Y + i * (JAR_H + JAR_GAP)
            buy = self._jar_button(st)
            self.buttons.append(ui.Button((r.x + JAR_X + JAR_W - 224, y + JAR_H - 34,
                                           138, 28), buy[0], style=buy[1], size=13,
                                          enabled=buy[2],
                                          action=lambda sid=st["id"]: self.buy(sid)))
            use = self._use_button(st)
            if use:
                self.buttons.append(ui.Button((r.x + JAR_X + JAR_W - 80, y + JAR_H - 34,
                                               74, 28), use[0], style=use[1], size=12,
                                              enabled=use[2],
                                              action=lambda sid=st["id"]: self.use(sid)))

    def _bulk_button(self):
        n = len(self.player.bag)
        if n == 0:
            return ("COOLER EMPTY", "dark", False)
        if self.mode == "cash":
            return ("SELL ALL  ·  %s" % fmt_money(self.player.bag_value), "primary", True)
        return ("TRADE ALL  ·  %d VP" % self.player.bag_points, "primary", True)

    def _jar_button(self, st):
        p = self.player
        if not p.jar_available(st["id"]):
            return ("LOCKED", "dark", False)
        if p.pts < st["pts"]:
            return ("%d VP" % st["pts"], "dark", False)
        return ("TRADE %d VP" % st["pts"], "primary", True)

    def _use_button(self, st):
        p = self.player
        if p.weed_count(st["id"]) <= 0:
            return None
        if p.strain == st["id"]:
            return ("IN USE", "dark", False)
        return ("USE", "dark", True)

    def jar_rows(self):
        p = self.player
        rows = data.STRAINS_BY_AREA[self.jar_area]
        return [s for s in rows
                if not s.get("hidden") or s["id"] in p.hidden_unlocked()]

    def open_buy(self):
        from .shop import ShopScreen
        self.app.pop()
        self.app.push(ShopScreen(self.app))

    def set_area(self, i):
        self.jar_area = i
        self.build()

    def set_mode(self, key):
        self.mode = key
        self.build()

    # -------------------------------------------------------------- actions
    def process(self, index):
        p = self.player
        if not (0 <= index < len(p.bag)):
            return
        sp = data.species(p.bag[index]["id"])
        if self.mode == "cash":
            value = p.sell_fish(index)
            self.app.audio.play("buy", 0.7)
            self.say_now("$%d. Don't spend it all on lures." % value)
        else:
            pts = p.trade_fish(index)
            self.app.audio.play("buy", 0.8)
            self.say_now("%d VP for the %s. Good fish." % (pts, sp["name"]))
        self.app.save()
        self.build()

    def bulk(self):
        p = self.player
        if not p.bag:
            return
        if self.mode == "cash":
            n, total = p.sell_all()
            self.app.audio.play("buy", 0.9)
            self.say_now("%d fish, %s. Pleasure." % (n, fmt_money(total)))
        else:
            n, pts = p.trade_all()
            self.app.audio.play("unlock", 0.6)
            self.say_now("%d fish, %d VP. Now we're talkin'." % (n, pts))
        self.app.save()
        self.build()

    def buy(self, sid):
        p = self.player
        ok, msg = p.buy_weed(sid)
        if ok:
            self.app.audio.play("buy", 0.9)
            self.say_now("One gram of %s. Go easy." % data.STRAINS_BY_ID[sid]["name"])
        else:
            self.app.audio.play("error", 0.5)
            self.say_now(msg)
        self.app.save()
        self.build()

    def use(self, sid):
        ok, msg = self.player.set_strain(sid)
        if ok:
            self.app.audio.play("ui", 0.7)
            self.say_now("Loaded you up with %s." % data.STRAINS_BY_ID[sid]["name"])
        self.build()

    def say_now(self, text):
        self.say = text
        self.say_t = 5.0

    # --------------------------------------------------------------- input
    def handle_event(self, event):
        if super().handle_event(event):
            return
        if event.type == pygame.KEYDOWN:
            if event.key in (pygame.K_ESCAPE, pygame.K_d):
                self.app.pop()
        elif event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
            for i, rect in enumerate(self.slots()):
                if rect.collidepoint(self.mouse) and i < len(self.player.bag):
                    self.process(i)
                    return

    def update(self, dt):
        super().update(dt)
        if self.say_t > 0:
            self.say_t = max(0.0, self.say_t - dt)
            if self.say_t == 0:
                self.say = ""

    # ---------------------------------------------------------------- draw
    def slots(self):
        r = self.rect
        out = []
        for i in range(self.player.bag_size):
            col, row = i % COLS, i // COLS
            out.append(pygame.Rect(r.x + GRID_X + col * (CARD_W + CARD_GX),
                                   r.y + GRID_Y + row * (CARD_H + CARD_GY),
                                   CARD_W, CARD_H))
        return out

    def draw(self, surf):
        p = self.player
        self.dim(surf, 168)
        r = self.rect
        self.draw_frame(surf, r, "SHOP", None)
        ui.txt(surf, "fish in  ·  smoke out", (r.right - 78, r.y + 40), 15, False,
               C.TEXT_DIM, anchor="topright")

        # ---- cooler header
        ui.txt(surf, "YOUR COOLER", (r.x + 30, r.y + 112), 15, True, C.GOLD)
        ui.txt(surf, "%d / %d" % (len(p.bag), p.bag_size), (r.x + 148, r.y + 112), 15,
               True, C.CREAM)
        ui.txt(surf, "click a fish to %s it" % ("sell" if self.mode == "cash" else "trade"),
               (r.x + 210, r.y + 114), 13, False, C.TEXT_DIM)

        # the counter
        pygame.draw.rect(surf, (24, 28, 40), (r.x + 20, r.y + 140, 760, 424),
                         border_radius=16)
        pygame.draw.rect(surf, (40, 46, 64), (r.x + 20, r.y + 140, 760, 424), width=1,
                         border_radius=16)

        for i, rect in enumerate(self.slots()):
            if i < len(p.bag):
                self._card(surf, rect, p.bag[i], i)
            else:
                self._empty(surf, rect)

        # ---- jars
        ui.txt(surf, "SAL'S JARS", (r.x + JAR_X, r.y + 86), 15, True, (168, 220, 160))
        pts_r = ui.txt(surf, "%d VP" % p.pts, (r.right - 34, r.y + 82), 20, True, C.GOLD,
                       anchor="topright")
        ui.txt(surf, "your vibe points", (pts_r.left - 10, r.y + 90), 11, False,
               C.TEXT_DIM, anchor="topright")

        for i, st in enumerate(self.jar_rows()):
            y = r.y + JAR_Y + i * (JAR_H + JAR_GAP)
            self._jar(surf, (r.x + JAR_X, y, JAR_W, JAR_H), st)

        # ---- Sal
        self._sal(surf)
        for b in self.buttons:
            b.draw(surf, self.mouse)

    def _card(self, surf, rect, entry, index):
        sp = data.species(entry["id"])
        col = C.RARITY_COLORS[sp["rarity"]]
        hot = rect.collidepoint(self.mouse)
        bg = (38, 44, 62) if hot else (28, 33, 48)
        pygame.draw.rect(surf, bg, rect, border_radius=12)
        pygame.draw.rect(surf, col if hot else (48, 54, 74), rect,
                         width=2 if hot else 1, border_radius=12)
        ui.draw_fish(surf, (rect.x + 8, rect.y + 2, 160, 44), sp)
        ui.txt(surf, U.truncate(sp["name"], font(12, True), 158),
               (rect.x + 9, rect.y + 48), 12, True, C.CREAM)
        ui.txt(surf, C.RARITY_LABEL[sp["rarity"]].upper(), (rect.centerx, rect.y + 3),
               9, True, C.RARITY_COLORS[sp["rarity"]], anchor="midtop")
        pygame.draw.circle(surf, col, (rect.x + 10, rect.y + 80), 3)
        vp = C.RARITY_POINTS[sp["rarity"]]
        if self.mode == "cash":
            main, sub = fmt_money(entry["value"]), "%d VP" % vp
            mcol, scol = C.GOLD, (150, 220, 160)
        else:
            main, sub = "%d VP" % vp, fmt_money(entry["value"])
            mcol, scol = (150, 220, 160), C.GOLD
        ui.txt(surf, main, (rect.right - 10, rect.y + 52), 14, True, mcol,
               anchor="topright")
        ui.txt(surf, sub, (rect.right - 10, rect.y + 70), 10, False, scol,
               anchor="topright")

    def _empty(self, surf, rect):
        layer = pygame.Surface(rect.size, pygame.SRCALPHA)
        pygame.draw.rect(layer, (255, 255, 255, 10), layer.get_rect(), border_radius=12)
        pygame.draw.rect(layer, (60, 66, 88, 60), layer.get_rect(), width=1,
                         border_radius=12)
        surf.blit(layer, rect.topleft)

    def _jar(self, surf, rect, st):
        rect = pygame.Rect(rect)
        p = self.player
        col = st["color"]
        avail = p.jar_available(st["id"])
        stock = p.weed_count(st["id"])
        equipped = p.strain == st["id"]
        pygame.draw.rect(surf, (30, 35, 51) if avail else (24, 27, 39), rect,
                         border_radius=12)
        pygame.draw.rect(surf, C.GOLD if equipped else (48, 54, 74), rect,
                         width=2 if equipped else 1, border_radius=12)
        # little jar
        jx, jy = rect.x + 26, rect.y + 36
        pygame.draw.rect(surf, (60, 66, 84), (jx - 11, jy - 16, 22, 30), border_radius=4)
        pygame.draw.rect(surf, col if avail else (70, 76, 90), (jx - 8, jy - 6, 16, 17),
                         border_radius=3)
        pygame.draw.rect(surf, (120, 128, 148), (jx - 6, jy - 20, 12, 5), border_radius=2)

        ui.txt(surf, U.truncate(st["name"], font(16, True), 210), (rect.x + 52, rect.y + 8),
               16, True, C.CREAM if avail else (128, 132, 148))
        ui.txt(surf, "%d VP" % st["pts"], (rect.right - 12, rect.y + 9), 14, True,
               C.GOLD if p.pts >= st["pts"] and avail else C.TEXT_DIM, anchor="topright")
        ui.txt(surf, "x%d" % stock, (rect.right - 12, rect.y + 26), 11, True,
               (150, 200, 150) if stock else (96, 102, 120), anchor="topright")
        ui.txt(surf, "POTENCY %d/7" % st["potency"], (rect.x + 52, rect.y + 33), 10,
               True, (196, 176, 120) if avail else (120, 112, 92))
        if avail:
            ui.txt(surf, U.truncate(
                "luck +%.2f  ·  lasts x%.1f  ·  wobble x%.2f"
                % (st["luck"], st["duration"], st["wobble"]), font(11), 300),
                (rect.x + 52, rect.y + 48), 11, False, (132, 140, 162))

        else:
            if st["area"] not in p.unlocked:
                ui.txt(surf, "Unlock this water first", (rect.x + 52, rect.y + 48),
                       11, True, (188, 148, 120))
            else:
                need = TIER_WORD.get(st["tier"], "rare")
                ui.txt(surf, "Needs a %s fish (Sal remembers)" % need,
                       (rect.x + 52, rect.y + 48), 11, True, (188, 148, 120))

    def _sal(self, surf):
        r = self.rect
        y = r.y + 616
        self._portrait(surf, (r.x + 30, y), 58)
        p = self.player
        if self.say_t > 0:
            line = self.say
        elif not p.bag and p.pts == 0:
            line = "Cooler's empty, kid. Go fill it."
        elif not p.bag:
            line = "You've got %d VP burnin' a hole in my ledger." % p.pts
        else:
            best = p.bag_best_tier()
            if best >= 4:
                line = "Is that... no. Don't tell me. Take whatever you want."
            elif best >= 3:
                line = "Now THAT'S a fish. I'll open the good jar for that."
            elif best >= 2:
                line = "A rare one! You're learnin'."
            elif best >= 1:
                line = "Decent little fish. I can work with these."
            else:
                line = "Minnows. Great. I'll take 'em off your hands."
        ui.txt(surf, "SAL", (r.x + 100, y + 4), 12, True, C.GOLD)
        ui.txt(surf, "\u201c%s\u201d" % line, (r.x + 100, y + 22), 17, False, C.CREAM)

    def _portrait(self, surf, pos, size=58):
        x, y = pos
        cx, cy = x + size // 2, y + size // 2
        pygame.draw.circle(surf, (28, 40, 40), (cx, cy), size // 2)
        pygame.draw.circle(surf, (58, 76, 70), (cx, cy), size // 2, 2)
        # face
        pygame.draw.circle(surf, (188, 142, 104), (cx, cy + 3), 17)
        # beanie
        pygame.draw.ellipse(surf, (110, 74, 60), (cx - 18, cy - 20, 36, 20))
        pygame.draw.rect(surf, (96, 64, 52), (cx - 18, cy - 8, 36, 6), border_radius=2)
        # shades
        pygame.draw.rect(surf, (26, 28, 34), (cx - 14, cy - 1, 12, 7), border_radius=2)
        pygame.draw.rect(surf, (26, 28, 34), (cx + 2, cy - 1, 12, 7), border_radius=2)
        pygame.draw.line(surf, (26, 28, 34), (cx - 2, cy + 1), (cx + 2, cy + 1), 2)
        # beard + smile
        pygame.draw.arc(surf, (120, 90, 68), (cx - 8, cy + 6, 16, 10), math.pi, math.tau, 2)
        pygame.draw.arc(surf, (90, 60, 46), (cx - 9, cy + 2, 18, 14), 0.2, math.pi - 0.2, 2)
        # joint
        jx, jy = cx + 12, cy + 12
        pygame.draw.line(surf, (244, 240, 228), (jx, jy), (jx + 12, jy - 8), 2)
        U.circle_alpha(surf, (jx + 13, jy - 9), 5, (255, 190, 110), 120)
        pygame.draw.circle(surf, (255, 214, 150), (jx + 13, jy - 9), 2)
