"""Tackle shop: rods, lines, lures, coolers and travel."""
from __future__ import annotations

import pygame

from .. import config as C
from .. import data
from .. import ui
from .. import util as U
from ..util import font
from .base import Screen, fmt_money

TABS = [("rod", "RODS"), ("line", "LINES"), ("lure", "LURES"),
        ("cooler", "COOLERS"), ("smoke", "SMOKE")]


class ShopScreen(Screen):
    overlay = True

    def __init__(self, app, areas_only=False):
        super().__init__(app)
        self.areas_only = areas_only
        self.tab = "travel" if areas_only else "rod"
        self.build()

    # ------------------------------------------------------------------ ui
    def build(self):
        self.buttons = []
        self.rect = self.panel_rect(1180, 664)
        r = self.rect
        self.add_close(r)
        if self.areas_only:
            for i, row in enumerate(self.rows()):
                y = r.y + 148 + i * 76      # same rhythm as the row cards
                self.buttons.append(ui.Button((r.right - 200, y + 15, 168, 38),
                                              row["btn"], style=row.get("style", "dark"),
                                              size=15, enabled=row.get("enabled", True),
                                              action=row.get("action")))
            return
        self.buttons.append(ui.Button((r.x + 276, r.y + 26, 96, 34), "SELL",
                                      style="tab", size=13, action=self.open_sell))
        buy = ui.Button((r.x + 170, r.y + 26, 96, 34), "BUY", style="tab",
                        size=13, action=None)
        buy.selected = True
        self.buttons.append(buy)
        tw = 176
        for i, (key, label) in enumerate(TABS):
            b = ui.Button((r.x + 32 + i * (tw + 6), r.y + 92, tw, 38), label,
                          style="tab", size=15, action=lambda k=key: self.set_tab(k))
            b.selected = (self.tab == key)
            self.buttons.append(b)

        tight = self.tab == "smoke"
        pitch, rh = (58, 52) if tight else (76, 68)
        for i, row in enumerate(self.rows()):
            y = r.y + 148 + i * pitch
            btn = ui.Button((r.right - 200, y + (8 if tight else 15), 168,
                             34 if tight else 38), row["btn"],
                            style=row.get("style", "dark"), size=15,
                            enabled=row.get("enabled", True),
                            action=row.get("action"))
            self.buttons.append(btn)
            row["btn_rect"] = (r.right - 200, y + 15, 168, 38)

    def handle_event(self, event):
        if super().handle_event(event):
            return
        if event.type == pygame.KEYDOWN:
            if event.key in (pygame.K_ESCAPE, pygame.K_b):
                self.app.pop()

    def open_sell(self):
        from .dealer import DealerScreen
        self.app.pop()
        self.app.push(DealerScreen(self.app))

    def set_tab(self, key):
        self.tab = key
        self.build()

    # --------------------------------------------------------------- data
    def rows(self):
        p = self.player
        out = []
        if self.tab == "cooler":
            out.append(dict(
                title="Bag of Ice",
                desc="$%d - keeps the cooler cold for %d minutes." % (
                    data.ICE_COST, int(data.ICE_MINUTES)),
                extra="ice %.1f / %.0f min%s" % (
                    p.ice, p.ice_cap, "" if p.ice > 0 else "   -   fish are spoiling"),
                swatch=(150, 210, 236), btn="BUY  $%d" % data.ICE_COST,
                style="primary" if p.cash >= data.ICE_COST else "dark",
                enabled=(p.cash >= data.ICE_COST and p.ice < p.ice_cap - 0.01),
                action=self.do_ice))
        if self.tab in ("rod", "line", "lure", "cooler"):
            table = {"rod": data.RODS, "line": data.LINES, "lure": data.LURES,
                     "cooler": data.COOLERS}[self.tab]
            for item in table:
                if p.gear_locked(item["id"]):
                    out.append(dict(
                        title=item["name"], desc=item["desc"],
                        extra="unlock %s first" % data.SPOTS[data.GEAR_SPOT[item["id"]]]["name"],
                        swatch=item.get("color"), btn="LOCKED", style="dark",
                        enabled=False, locked=True, action=None))
                    continue
                state = p.item_state(self.tab, item["id"])
                if state == "buy":
                    btn = "BUY  %s" % fmt_money(item["cost"])
                    style = "primary" if p.cash >= item["cost"] else "dark"
                    enabled = p.cash >= item["cost"]
                elif state == "owned":
                    btn, style, enabled = "EQUIP", "dark", True
                else:
                    btn, style, enabled = "EQUIPPED", "dark", False
                out.append(dict(
                    title=item["name"],
                    desc=item["desc"],
                    equipped=(state == "equipped"),
                    extra=self._stats(self.tab, item),
                    swatch=item.get("color"),
                    btn=btn, style=style, enabled=enabled,
                    action=lambda k=self.tab, gid=item["id"]: self.do_buy(k, gid)))
        elif self.tab == "smoke":
            for item in data.SMOKERS:
                state = p.item_state("smoker", item["id"])
                if state == "buy":
                    btn = "BUY  %s" % fmt_money(item["cost"])
                    style = "primary" if p.cash >= item["cost"] else "dark"
                    enabled = p.cash >= item["cost"]
                elif state == "owned":
                    btn, style, enabled = "EQUIP", "dark", True
                else:
                    btn, style, enabled = "EQUIPPED", "dark", False
                if item["id"] in ("dab_rig", "hot_press") and not p.legends_complete():
                    btn, style, enabled = "LOCKED", "dark", False
                    extra = "unlock: legendaries %d/4, then buy" % p.legendaries_caught()
                else:
                    extra = self._stats("smoker", item)
                if item["id"] == "hot_press" and state != "buy" and p.legends_complete():
                    btn, style, enabled = "PRESS", "primary", True
                out.append(dict(
                    title=item["name"], desc=item["desc"],
                    extra=extra, swatch=item["color"],
                    btn=btn, style=style, enabled=enabled,
                    action=lambda gid=item["id"]: self.do_smoke(gid)))
        else:
            for i, sp in enumerate(data.SPOTS):
                unlocked = i in p.unlocked
                here = i == p.spot
                can, why = p.can_travel(i)
                if here:
                    btn, style, enabled = "YOU ARE HERE", "dark", False
                elif unlocked:
                    btn, style, enabled = "TRAVEL", "primary", True
                else:
                    btn = "UNLOCK  %s" % fmt_money(sp["need_cash"])
                    style = "primary" if can else "dark"
                    enabled = can
                if i > 0:
                    prev = i - 1
                    got = p.spot_found(prev)
                    tot = len(data.fish_for_spot(prev))
                    need = "Catch %d/%d in %s  ·  %s" % (
                        got, tot, data.SPOTS[prev]["name"].split()[-1],
                        fmt_money(sp["need_cash"]))
                else:
                    need = "Open from the start"
                if unlocked:
                    need = "Unlocked"
                info = "%d / %d species here" % (
                    sum(1 for s in data.fish_for_spot(i) if p.caught(s["id"])),
                    len(data.fish_for_spot(i)))
                hidden = not unlocked and not can
                out.append(dict(
                    title=sp["name"] if not hidden else "???",
                    desc=sp["blurb"] if not hidden else "An undiscovered water.",
                    extra=need + "   ·   " + info,
                    swatch=sp["tint"], btn=btn, style=style, enabled=enabled,
                    locked=not unlocked and not can,
                    action=lambda idx=i: self.do_travel(idx)))
        return out

    def _stats(self, kind, item):
        if kind == "rod":
            return "reel +%d%%   ·   power +%d%%   ·   +%d%% tolerance" % (
                round((item["reel"] - 1) * 100), round((item["power"] - 1) * 100),
                round(item["tol"] * 100))
        if kind == "line":
            return "snap grace %.2fs" % item["grace"]
        if kind == "lure":
            bits = []
            for rar, mult in item["bias"].items():
                bits.append("%s x%.1f" % (C.RARITY_LABEL[rar], mult))
            if item.get("night_bias"):
                bits.append("better at night")
            return "   ·   ".join(bits) if bits else "no particular opinions"
        if kind == "cooler":
            return "holds %d fish" % item["slots"]
        if kind == "smoker":
            bits = []
            if item["potency"]:
                bits.append("+%d potency" % item["potency"])
            if item.get("fill", 1.0) != 1.0:
                bits.append("fills +%d%%" % round((item["fill"] - 1.0) * 100))
            if item["dur"] != 1.0:
                bits.append("lasts x%.2f" % item["dur"])
            if item["luck"]:
                bits.append("luck +%.2f" % item["luck"])
            if item["conc"]:
                bits.append("smokes concentrate")
            return "   ·   ".join(bits) or "baseline"
        return ""

    # -------------------------------------------------------------- actions
    def do_buy(self, kind, gid):
        ok, msg = self.player.buy_or_equip(kind, gid)
        if ok:
            self.app.audio.play("buy", 0.8)
            self.app.toast("Equipped: " + msg if msg == "Equipped" else "Bought!", C.GOLD)
            self.app.save()
        else:
            self.app.audio.play("error", 0.5)
            self.app.toast(msg, (240, 150, 120))
        self.build()

    def do_ice(self):
        ok, msg = self.player.buy_ice()
        if ok:
            self.app.audio.play("buy", 0.8)
            self.app.toast(msg, (150, 210, 236))
            self.app.save()
        else:
            self.app.audio.play("error", 0.5)
            self.app.toast(msg, (240, 150, 120))
        self.build()

    def do_smoke(self, gid):
        p = self.player
        if gid == "hot_press" and gid in p.smokers:
            ok, msg = p.press_weed()
        else:
            ok, msg = p.buy_or_equip("smoker", gid)
        if ok:
            self.app.audio.play("buy", 0.8)
            self.app.toast(msg, C.GOLD)
            self.app.save()
        else:
            self.app.audio.play("error", 0.5)
            self.app.toast(msg, (240, 150, 120))
        self.build()

    def do_travel(self, i):
        ok, why = self.player.travel(i)
        if not ok:
            self.app.audio.play("error", 0.5)
            self.app.toast(why, (240, 150, 120))
            return
        self.app.audio.play("travel", 0.9)
        self.app.new_world(i)
        self.app.toast("Welcome to " + data.SPOTS[i]["name"], C.CREAM, 2.8)
        self.app.save()
        self.app.pop()

    # ----------------------------------------------------------------- draw
    def draw(self, surf):
        p = self.player
        self.dim(surf, 160)
        r = self.rect
        rows = self.rows()
        self.draw_frame(surf, r, "AREAS" if self.areas_only else "SHOP", None)
        # cash readout
        cash_r = ui.txt(surf, fmt_money(p.cash), (r.right - 96, r.y + 18), 28, True,
                        C.GOLD, anchor="topright")
        ui.coin(surf, (cash_r.left - 16, cash_r.centery), 11)


        tight = self.tab == "smoke"
        pitch, rh = (58, 52) if tight else (76, 68)
        y = r.y + 148
        for i, row in enumerate(rows):
            yy = y + i * pitch
            self._row(surf, (r.x + 32, yy, r.w - 64, rh), row, i)
        for b in self.buttons:
            b.draw(surf, self.mouse)

        # footer note
        note = {
            "rod": "Rods reel faster and hold tension better.",
            "line": "Lines give you more time before the line snaps.",
            "lure": "Lures change what bites. Equip one and cast.",
            "cooler": "Bigger cooler, longer trips. Sal buys whatever is inside.",
            "smoke": "Your smoking method sets potency and burn time. Hot press = concentrate.",
            "travel": "New water, new fish. The deep spots pay better.",
        }[self.tab]
        if not tight:
            ui.txt(surf, note, (r.x + 34, r.bottom - 40), 14, False, C.TEXT_DIM)

    def _row(self, surf, rect, row, index):
        rect = pygame.Rect(rect)
        hover = rect.collidepoint(self.mouse)
        locked = row.get("locked")
        bg = (34, 39, 56) if hover and not locked else (24, 27, 38) if locked else (28, 32, 47)
        pygame.draw.rect(surf, bg, rect, border_radius=12)
        pygame.draw.rect(surf, (46, 52, 72), rect, width=1, border_radius=12)
        row.get("swatch")
        if row.get("equipped"):
            pygame.draw.rect(surf, C.GOLD, (rect.x, rect.y + 8, 4, rect.h - 16),
                             border_radius=2)
        ui.txt(surf, row["title"], (rect.x + 22, rect.y + 12), 20, True,
               (128, 132, 148) if locked else C.CREAM)
        ui.txt(surf, U.truncate(row["desc"], font(13), 760), (rect.x + 22, rect.y + 40),
               13, False, (86, 90, 108) if locked else C.TEXT_DIM)
        if row.get("extra"):
            ui.txt(surf, row["extra"], (rect.right - 184, rect.y + 14), 13, True,
                   (150, 158, 180), anchor="topright")
