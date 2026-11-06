"""The play screen: world, HUD, prompts, reel panel and catch card."""
from __future__ import annotations

import math
import random

import pygame

from .. import config as C
from .. import data
from .. import ui
from .. import util as U
from .. import world3d as W3D
from ..util import clamp, mix, font, pulse, ease_out_cubic, ease_out_back
from .base import Screen, fmt_money, fmt_kg, fmt_time

PHASES = [
    (5.5, 7.5, "dawn"),
    (7.5, 11.0, "morning"),
    (11.0, 15.0, "midday"),
    (15.0, 17.6, "afternoon"),
    (17.6, 20.0, "golden hour"),
    (20.0, 22.5, "evening"),
    (22.5, 29.5, "night"),
]


def day_phase(hour):
    hour %= 24.0
    for a, b, name in PHASES:
        hb = b if b > a else b + 24
        h = hour if hour >= a else hour + 24
        if a <= h < hb:
            return name
    return "night"


class PlayScreen(Screen):
    def __init__(self, app):
        super().__init__(app)
        self.card = None
        self.card_anim = 0.0
        self.card_t = 0.0
        self.toast = None
        self.toast_t = 0.0
        self.hint = ""
        self.hint_t = 0.0
        self.tips = 0.0
        self.prompt_t = 0.0
        self.bite_flash = 0.0
        self.chill_flash = 0.0
        self.pipe_t = 0.0
        self.pipe_fired = False
        self.menu_open = False
        self.reel_pulse = 0.0
        self.scene = pygame.Surface((W3D.INTERNAL_W, W3D.INTERNAL_H), 0, 32, U._OPAQUE_MASKS)
        self.scanlines = self._scanlines()
        self.top_scrim = self._gradient_strip(150, (6, 8, 16, 200))
        self.bottom_scrim = self._gradient_strip(260, (6, 8, 16, 170), flip=True)
        self.tip_text = data.TIPS[0]
        self.build()

    def build(self):
        self.buttons = []
        self._was_full = self.player.bag_full
        bx = C.W - 152
        self.buttons.append(ui.Button((bx, 108, 132, 30), "MENU", style="dark",
                                      size=14, radius=8, action=self.toggle_menu))
        if self.menu_open:
            items = (("JOURNAL", self.open_journal), ("SHOP", self.open_dealer),
                     ("AREAS", self.open_areas), ("PAUSE", self.open_pause))
            for i, (label, action) in enumerate(items):
                self.buttons.append(ui.Button(
                    (bx, 142 + i * 32, 132, 28), label, style="dark", size=13,
                    radius=6, action=lambda f=action: self.menu_pick(f)))

    def toggle_menu(self):
        self.menu_open = not self.menu_open
        self.build()

    def menu_pick(self, fn):
        self.menu_open = False
        self.build()
        fn()

    # ------------------------------------------------------------- helpers
    def _draw_level(self, surf):
        p = self.player
        lv = p.level
        lw = 190
        lx = C.W // 2 - lw // 2
        ui.txt(surf, "LV %d" % lv, (lx - 10, 12), 14, True, C.GOLD, anchor="topright")
        cur = p.LEVEL_XP[min(lv - 1, len(p.LEVEL_XP) - 1)]
        nxt = p.LEVEL_XP[min(lv, len(p.LEVEL_XP) - 1)]
        frac = 0.0 if nxt <= cur else min(1.0, (p.xp - cur) / float(nxt - cur))
        U.bar(surf, (lx, 16, lw, 6), frac, C.GOLD, radius=3)
        ui.txt(surf, "%d xp" % p.xp, (lx + lw // 2, 26), 9, False,
               (150, 156, 176), anchor="midtop")
        if getattr(self, "rank_popup", 0.0) > 0:
            a = min(1.0, self.rank_popup / 0.6)
            w, h = 360, 54
            bx, by = C.W // 2 - w // 2, 46
            U.panel(surf, (bx, by, w, h), (28, 32, 48), radius=10,
                    border=C.GOLD, alpha=int(235 * a))
            ui.txt(surf, "RANK UP  ·  LV %d" % lv, (C.W // 2, by + 8), 17, True,
                   C.GOLD, anchor="midtop")
            ui.txt(surf, "%d stoke point%s  ·  press K" % (
                p.stoke_pts, "" if p.stoke_pts == 1 else "s"),
                (C.W // 2, by + 30), 12, False, (200, 206, 226), anchor="midtop")

    def _smoker_icon(self, surf, x, y, sid):
        """Tiny pixel glyph for the smoking method."""
        if sid == "one_hitter":
            pygame.draw.rect(surf, (198, 172, 120), (x + 7, y + 2, 3, 13))
            pygame.draw.rect(surf, (240, 120, 80), (x + 7, y, 3, 3))
        elif sid == "papers":
            pygame.draw.polygon(surf, (236, 232, 220),
                                [(x + 8, y), (x + 3, y + 15), (x + 13, y + 15)])
        elif sid == "backwoods":
            pygame.draw.rect(surf, (120, 92, 62), (x + 5, y + 3, 6, 12))
            pygame.draw.circle(surf, (240, 120, 80), (x + 8, y + 2), 2)
        elif sid == "pipe":
            pygame.draw.circle(surf, (150, 196, 210), (x + 5, y + 5), 4)
            pygame.draw.line(surf, (150, 196, 210), (x + 8, y + 7),
                             (x + 14, y + 13), 2)
        elif sid == "beaker_bong":
            pygame.draw.polygon(surf, (140, 200, 180),
                                [(x + 3, y + 15), (x + 13, y + 15),
                                 (x + 11, y + 8), (x + 5, y + 8)])
            pygame.draw.rect(surf, (140, 200, 180), (x + 6, y + 1, 4, 7))
        elif sid == "heady_bong":
            pygame.draw.circle(surf, (210, 170, 240), (x + 8, y + 11), 5)
            pygame.draw.rect(surf, (210, 170, 240), (x + 6, y, 4, 7))
        elif sid == "dab_rig":
            pygame.draw.polygon(surf, (250, 210, 120),
                                [(x + 3, y + 15), (x + 12, y + 15),
                                 (x + 9, y + 7), (x + 6, y + 7)])
            pygame.draw.line(surf, (250, 210, 120), (x + 8, y + 7), (x + 14, y + 1), 2)
        else:
            pygame.draw.rect(surf, (226, 120, 96), (x + 2, y + 5, 12, 3))
            pygame.draw.rect(surf, (226, 120, 96), (x + 2, y + 10, 12, 3))

    def _scanlines(self):
        s = pygame.Surface((C.W, C.H), pygame.SRCALPHA)
        for y in range(0, C.H, 2):
            pygame.draw.line(s, (0, 0, 0, 18), (0, y), (C.W, y))
        return s

    def _gradient_strip(self, h, color, flip=False):
        s = pygame.Surface((C.W, h), pygame.SRCALPHA)
        for y in range(h):
            a = int(color[3] * (1 - y / float(h)) ** 1.4)
            pygame.draw.line(s, (color[0], color[1], color[2], a), (0, y), (C.W, y))
        if flip:
            s = pygame.transform.flip(s, False, True)
        return s

    def notify(self, text, color=C.CREAM, life=2.2):
        self.toast = (text, color)
        self.toast_t = life

    def say(self, text, life=3.4):
        self.hint = text
        self.hint_t = life

    # -------------------------------------------------------------- actions
    def open_journal(self):
        from .journal import JournalScreen
        self.app.push(JournalScreen(self.app))

    def open_shop(self):
        from .shop import ShopScreen
        self.app.push(ShopScreen(self.app))

    def open_stoke(self):
        from .stoke import StokeScreen
        self.app.push(StokeScreen(self.app))

    def open_zaa(self):
        from .zaa import ZaaScreen
        self.app.push(ZaaScreen(self.app))

    def open_areas(self):
        from .shop import ShopScreen
        self.app.push(ShopScreen(self.app, areas_only=True))

    def open_dealer(self):
        from .dealer import DealerScreen
        self.app.push(DealerScreen(self.app))

    def open_pause(self):
        from .overlays import PauseScreen
        self.app.push(PauseScreen(self.app))

    def toke(self):
        p = self.player
        if self.pipe_t > 0:
            return
        if p.couch > 0:
            self.app.toast("Too relaxed to smoke right now", (240, 200, 130), 2.0)
            return
        dabs = p.concentrate.get(p.strain, 0)
        rig = p.smoker_data.get("conc")
        blocked = None
        if rig:
            if not p.dab_mode:
                blocked = "Dab Rig only smokes dabs - pick DAB in ZAA (Z)"
            elif dabs <= 0:
                blocked = "No dabs of %s - press 4 g in ZAA (Z)" % p.strain_data["name"]
        elif p.dab_mode and dabs > 0:
            blocked = "Dabs need the Dab Rig equipped"
        elif p.weed_count() <= 0:
            blocked = "Out of %s - check the SHOP" % p.strain_data["name"]
        if blocked:
            self.app.audio.play("error", 0.7)
            self.app.toast(blocked, (240, 160, 120), 2.6)
            return
        self.pipe_total = float(p.smoker_data.get("ceremony", 5.0))
        self.pipe_t = self.pipe_total
        self.pipe_fired = False
        self.app.audio.play("toke", 0.8)
        self.say("%s - %ds ceremony." % (p.smoker_data["name"],
                                          int(self.pipe_total)))

    def _finish_toke(self):
        p = self.player
        res = p.toke()
        if res == "ok":
            self.app.audio.play("toke", 0.8)
            self.chill_flash = 1.0
            self.world.particles.smoke(289, 430, 10, (226, 232, 240), 1.0)
            self.app.toast("%s... ahh." % p.strain_data["name"].lower(),
                           (168, 224, 170), 1.8)
            if p.tokes == 1:
                self.say("Being high pulls rarer fish - and better fish buy better weed.")
        elif res == "couch":
            self.app.audio.play("cough", 0.8)
            self.app.audio.play("toke", 0.5)
            self.world.particles.smoke(289, 432, 16, (226, 232, 240), 1.4)
            self.app.toast("COUCH LOCK - sit tight", (240, 160, 120), 2.4)
            self.say("That was one too many. Give it a minute.")
        else:
            self.app.audio.play("error", 0.7)
            self.app.toast("Out of %s - check the SHOP" % p.strain_data["name"],
                           (240, 160, 120), 2.4)

    # --------------------------------------------------------------- input
    def handle_event(self, event):
        if self.card is not None:
            if event.type == pygame.KEYDOWN and event.key in (
                    pygame.K_RETURN, pygame.K_KP_ENTER, pygame.K_ESCAPE):
                self.dismiss_card()
            elif event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
                self.dismiss_card()
            return
        if super().handle_event(event):
            return
        if event.type == pygame.KEYDOWN:
            if self.pipe_t > 0 and event.key in (pygame.K_SPACE, pygame.K_e):
                return
            if event.key == pygame.K_SPACE and self.player.couch > 0:
                self.app.audio.play("error", 0.6)
                self.app.toast("Couch Locked - wait for the bar to drop to half",
                               (230, 140, 120), 2.4)
                return
            if event.key == pygame.K_SPACE:
                self.angler.press()
            elif event.key == pygame.K_e:
                if self.angler.state in ("reel", "hook", "bite"):
                    self.app.toast("Not while the fish is on!", (240, 190, 130), 1.6)
                else:
                    self.toke()
            elif event.key == pygame.K_j:
                self.open_journal()
            elif event.key == pygame.K_b:
                self.open_shop()
            elif event.key == pygame.K_k:
                self.open_stoke()
            elif event.key == pygame.K_z:
                self.open_zaa()
            elif event.key == pygame.K_a:
                self.open_areas()
            elif event.key == pygame.K_ESCAPE:
                self.open_pause()
        elif event.type == pygame.KEYUP and event.key == pygame.K_SPACE:
            self.angler.release()
        elif event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
            self.angler.press()
        elif event.type == pygame.MOUSEBUTTONUP and event.button == 1:
            self.angler.release()

    def dismiss_card(self):
        self.card = None
        self.angler.reset()

    # -------------------------------------------------------------- update
    def update(self, dt):
        if getattr(self.player, "pov", "third") == "free":
            keys = pygame.key.get_pressed()
            w = self.world
            if keys[pygame.K_LEFT]:
                w.free_yaw -= 1.8 * dt
            if keys[pygame.K_RIGHT]:
                w.free_yaw += 1.8 * dt
            if keys[pygame.K_UP]:
                w.free_pitch = min(0.95, w.free_pitch + 0.9 * dt)
            if keys[pygame.K_DOWN]:
                w.free_pitch = max(-0.35, w.free_pitch - 0.9 * dt)
        lv = self.player.level
        if lv > getattr(self, "_last_level", lv):
            self.rank_popup = 4.5
            self.app.audio.play("buy", 0.9)
        self._last_level = lv
        if getattr(self, "rank_popup", 0.0) > 0:
            self.rank_popup = max(0.0, self.rank_popup - dt)
        if self.player.bag and self.player.ice <= 0 and not getattr(self, "_ice_warned", False):
            self._ice_warned = True
            self.app.toast("Ice is gone - fish are spoiling", (230, 110, 100), 2.8)
        if self.player.ice > 0:
            self._ice_warned = False
        super().update(dt)
        self.prompt_t += dt
        self.bite_flash = max(0.0, self.bite_flash - dt)
        self.chill_flash = max(0.0, self.chill_flash - dt * 1.6)
        if self.pipe_t > 0:
            self.pipe_t = max(0.0, self.pipe_t - dt)
            done = self.pipe_total - self.pipe_t
            if not self.pipe_fired and done >= self.pipe_total * 0.6:
                self.pipe_fired = True
                self._finish_toke()
        if self.toast_t > 0:
            self.toast_t = max(0.0, self.toast_t - dt)
        if self.hint_t > 0:
            self.hint_t = max(0.0, self.hint_t - dt)

        if self.card is not None:
            self.card_anim = min(1.0, self.card_anim + dt * 6.0)
            self.card_t += dt
            return

        p = self.player
        p.update(dt)
        self.angler.update(dt)
        self.world.update(dt, p.hour, p.high)
        self._handle_events()
        if p.bag_full != self._was_full:
            self.build()

        # first-run coaching
        if p.casts == 0 and not p.seen_intro:
            self.hint = "Hold SPACE to cast. Fill the cooler, then open the SHOP."
            p.seen_intro = True
            self.hint_t = 6.0

        if self.angler.state == "wait":
            self.tips += dt
        else:
            self.tips = 0.0

    def _handle_events(self):
        p = self.player
        for ev in self.angler.drain_events():
            if ev == "cast":
                self.app.audio.play("cast", 0.8)
            elif ev == "plop":
                self.app.audio.play("plop", 0.9)
                if p.casts == 1:
                    self.say("Wait for a real dip in the bobber, then hit SPACE fast.")
            elif ev == "nibble":
                self.app.audio.play("nibble", 0.6)
            elif ev == "bite":
                self.app.audio.play("bite", 1.0)
                self.bite_flash = 1.0
            elif ev == "hook":
                self.app.audio.play("hook", 1.0)
                self.app.audio.play("plop", 0.5)
                if p.catches == 0:
                    self.say("SPACE when the marker hits the green arc.")
            elif ev == "thrash":
                self.app.audio.play("reel", 0.35)
            elif ev == "snap":
                self.app.audio.play("snap", 1.0)
                self.app.toast("SNAP - the line gave out", (240, 130, 110), 2.6)
                self.world.particles.smoke(
                    *self.world.bobber_pos(self.angler.depth)[:2], 6, (200, 220, 230))
                self.angler.reset()
            elif ev == "escape":
                self.app.audio.play("fail", 0.8)
                self.app.toast("It got away...", (220, 190, 160), 2.2)
                self.angler.reset()
            elif ev == "landed":
                self._on_landed()
            elif ev == "bagfull":
                self.app.audio.play("error", 0.6)
                self.app.toast("Cooler's full - take them to Sal", (240, 190, 120), 2.4)
            elif ev == "locked":
                self.app.audio.play("error", 0.6)
                self.app.toast("Too relaxed to cast right now", (240, 200, 130), 2.0)

    def _on_landed(self):
        res = self.angler.result
        if not res:
            return
        sp = res["fish"]
        p = self.player
        info = p.add_catch(sp["id"], res["kg"], res["cm"])
        rarity = sp["rarity"]
        if rarity in ("legendary", "mythic"):
            self.app.audio.play("legend", 1.0)
        else:
            self.app.audio.play("catch", 0.9)
        self.world.particles.sparkle(*self.world.bobber_pos(self.angler.depth)[:2],
                                     14 if rarity != "common" else 6,
                                     C.RARITY_COLORS[rarity])
        self.card = dict(sp=sp, kg=res["kg"], cm=res["cm"], new=info["new"],
                         value=info["value"], bonus=info["bonus"], rarity=rarity,
                         vp=C.RARITY_POINTS[rarity],
                         kept=info["kept"], capacity=info["capacity"], anim=0.0)
        self.card_anim = 0.0
        self.card_t = 0.0
        self.app.save()

    # ---------------------------------------------------------------- draw
    def draw(self, surf):
        w = self.world
        p = self.player
        a = self.angler
        mode = "idle"
        if a.state in ("fly", "bite", "reel"):
            mode = a.state
        scene = self.scene
        fly = (a.fly_t / C.FLY_TIME) if a.state == "fly" else 0.0
        w.pov = getattr(self.player, "pov", "third")
        w.pipe_raise = 1.0 if self.pipe_t > 0 else 0.0
        w.draw(scene, mode, a.depth, 0.35 if a.state == "reel" else 0.0, fly)
        if a.state == "bite":
            self._draw_bite_marker(scene)

        # low-res buffer upscaled with a breathing zoom while high
        high = p.high
        zoom = 1.0 + 0.010 * high * math.sin(self.t * 1.35)
        size = (int(C.W * zoom) + 2, int(C.H * zoom) + 2)
        big = pygame.transform.scale(scene, size)
        ox = math.sin(self.t * 0.55) * 8.0 * high
        oy = math.cos(self.t * 0.72) * 4.0 * high
        surf.blit(big, ((C.W - size[0]) // 2 + int(ox), (C.H - size[1]) // 2 + int(oy)))
        U.grain(surf, 7, 1)
        wash = pygame.Surface((C.W, C.H), pygame.SRCALPHA)
        wash.fill((250, 214, 168, 10))
        surf.blit(wash, (0, 0))

        self._hud(surf)
        if a.state == "charge":
            self._power_bar(surf)
        if a.state == "reel":
            self._reel_panel(surf)
        if self.card is None:
            self._prompt(surf)
        if p.couch > 0:
            self._couch(surf)
        if self.card is not None:
            self._catch_card(surf)

    # ------------------------------------------------------------------ hud
    def _hud(self, surf):
        p = self.player
        surf.blit(self.top_scrim, (0, 0))
        sp = p.spot_data
        ui.chip(surf, "AREA %d / %d" % (p.spot + 1, len(data.SPOTS)), (26, 18),
                C.GOLD, 12, bg_alpha=30)
        ui.txt(surf, sp["name"], (26, 44), 30, True, C.CREAM, family="title",
               shadow=(8, 10, 18))

        # currency
        cx = C.W - 26
        r1 = ui.txt(surf, fmt_money(p.cash), (cx, 16), 26, True, C.GOLD,
                    anchor="topright")
        ui.coin(surf, (r1.left - 16, r1.centery), 11)
        full = p.bag_full
        ice_frac = min(1.0, p.ice / max(0.001, p.ice_cap))
        U.bar(surf, (cx - 96, 64, 92, 4), ice_frac,
              (190, 70, 66) if ice_frac <= 0.2 else (150, 210, 236), radius=2)
        ui.txt(surf, "%dm" % int(p.ice + 0.5), (cx - 116, 58), 9, False,
               (190, 70, 66) if ice_frac <= 0.2 else (150, 210, 236), anchor="topright")
        r2 = ui.txt(surf, "COOLER %d / %d" % (len(p.bag), p.bag_size), (cx, 50), 14,
                    True, (246, 170, 120) if full else (200, 190, 168), anchor="topright")
        ui.fish_icon(surf, (r2.left - 16, r2.centery), 16,
                     (246, 170, 120) if full else (200, 190, 168))
        r3 = ui.txt(surf, "%d VP" % p.pts, (cx, 70), 14, False, (150, 216, 160),
                    anchor="topright")
        ui.leaf(surf, (r3.left - 14, r3.centery), 8, (150, 216, 160))
        ui.txt(surf, "%d / %d species" % (p.found_count, p.total_count),
               (cx, 90), 13, False, C.TEXT_DIM, anchor="topright")

        # chill meter
        px, py = 18, C.H - 90
        U.panel(surf, (px, py, 288, 74), C.PANEL_BG, radius=14, border=C.PANEL_EDGE,
                alpha=196, shadow=True, shadow_alpha=60)
        ui.leaf(surf, (px + 20, py + 22), 9, (140, 200, 132))
        state = ("Sober" if p.chill <= 1.0 else
                 "Chill" if p.chill < 10 else "High" if p.chill < 30 else
                 "Baked" if p.chill < 50 else "Blazed" if p.chill < 70 else
                 "Stoned" if p.chill < 85 else "Zonked")
        if p.couch > 0:
            state = "Couch Locked"
        ui.txt(surf, state, (px + 34, py + 12), 12, True,
               (230, 140, 120) if p.couch > 0 else C.TEXT_DIM)
        grams = p.weed_count()
        if grams or p.concentrate.get(p.strain):
            ui.txt(surf, p.strain_data["name"], (px + 272, py + 12), 12, True,
                   mix((160, 220, 160), (210, 150, 240), p.high),
                   anchor="topright")
        else:
            ui.txt(surf, "dry", (px + 272, py + 12), 12, False, C.TEXT_DIM,
                   anchor="topright")
        ui.chill_meter(surf, (px + 34, py + 32, 238, 11), p, self.t)
        if p.chill <= 1.0:
            band = pygame.Surface((238, 11), pygame.SRCALPHA)
            band.fill((10, 12, 18, 190))
            surf.blit(band, (px + 34, py + 32))
            ui.txt(surf, "SOBER", (px + 153, py + 31), 9, True, (150, 170, 156),
                   anchor="midtop")
        dabs = p.concentrate.get(p.strain, 0) if p.smoker_data.get("conc") else 0
        if grams or dabs:
            line = "%s  ·  x%d g" % (p.smoker_data["name"], grams)
            if dabs:
                line += "  ·  %d g dab" % dabs
            self._smoker_icon(surf, px + 10, py + 42, p.smoker)
            ui.txt(surf, line + "  ·  Z ZAA", (px + 36, py + 48), 12, False,
                   (140, 150, 168))
        else:
            ui.txt(surf, "%s  ·  out  ·  Z ZAA" % p.smoker_data["name"],
                   (px + 34, py + 48), 12, False, (200, 150, 130))

        if self.menu_open:
            U.panel(surf, (C.W - 158, 138, 144, 4 * 32 + 10), C.PANEL_BG, radius=10,
                    border=C.PANEL_EDGE, alpha=236, shadow=True, shadow_alpha=50)
        self._draw_level(surf)
        for b in self.buttons:
            b.draw(surf, self.mouse)

        if self.player.couch <= 0 and self.player.high > 0.05:
            a = int(90 * pulse(self.t, 2.6) * self.player.high)
            if a > 4:
                U.circle_alpha(surf, (38, C.H - 40), 30, (255, 214, 150), a)

    def _power_bar(self, surf):
        a = self.angler
        w, h = 380, 18
        x, y = (C.W - w) // 2, C.H - 96
        U.panel(surf, (x - 24, y - 40, w + 48, 84), C.PANEL_BG, radius=14,
                border=C.PANEL_EDGE, alpha=220)
        ui.txt(surf, "CAST POWER", (x, y - 28), 14, True, C.CREAM_DIM)
        ui.txt(surf, "%d%%" % int(a.power * 100), (x + w, y - 28), 16, True, C.GOLD,
               anchor="topright")
        U.bar(surf, (x, y, w, h), 1.0, (28, 32, 46), radius=9)
        frac = a.power
        col = mix(mix((120, 200, 130), C.GOLD, frac * 1.4), (236, 120, 96), max(0.0, frac * 1.6 - 0.6))
        U.bar(surf, (x, y, w, h), frac, col, bg=(0, 0, 0, 0), radius=9)
        pygame.draw.rect(surf, C.CREAM, (int(x + w * frac) - 2, y - 4, 4, h + 8),
                         border_radius=2)
        ui.txt(surf, "release to cast  ·  further = deeper water", (C.W // 2, y + 30),
               14, False, C.TEXT_DIM, anchor="midtop")

    def _reel_panel(self, surf):
        a = self.angler
        rect = pygame.Rect(296, 552, 688, 150)
        U.panel(surf, rect, C.PANEL_BG, radius=18, border=C.PANEL_EDGE, alpha=232)
        pad = 30
        # fish silhouette
        if a.fish:
            ui.draw_fish(surf, (rect.x + 18, rect.y + 46, 96, 54), a.fish, ghost=True)
        ui.txt(surf, "SOMETHING'S FIGHTING", (rect.x + pad + 90, rect.y + 20), 14, True,
               C.TEXT_DIM)
        # stamina
        bx = rect.x + pad + 90
        bw = rect.right - pad - bx
        U.bar(surf, (bx, rect.y + 40, bw, 10), a.progress,
              mix((120, 190, 120), C.GOLD, a.progress), radius=5)
        ui.txt(surf, "%d%%" % int(a.progress * 100), (rect.right - pad, rect.y + 20),
               14, True, C.CREAM_DIM, anchor="topright")

        # sweep track: hit the green arc
        tx, ty, tw, th = rect.x + pad, rect.y + 76, rect.w - pad * 2, 26
        # rope track
        pygame.draw.rect(surf, (70, 56, 38), (tx, ty, tw, th), border_radius=th // 2)
        for i in range(-th, tw, 9):
            pygame.draw.line(surf, (104, 84, 56), (tx + i, ty + th),
                             (tx + i + th, ty), 3)
        pygame.draw.rect(surf, (146, 122, 84), (tx, ty, tw, th), width=2,
                         border_radius=th // 2)
        half = a.zone_w * 0.5
        lo = int(tx + tw * (a.zone - half))
        hi = int(tx + tw * (a.zone + half))
        red = pygame.Surface((tw, th), pygame.SRCALPHA)
        pygame.draw.rect(red, (150, 54, 50, 90), (0, 0, tw, th),
                         border_radius=th // 2)
        surf.blit(red, (tx, ty))
        layer = pygame.Surface((max(6, hi - lo), th), pygame.SRCALPHA)
        pygame.draw.rect(layer, (108, 200, 130, 150), (0, 0, max(6, hi - lo), th),
                         border_radius=6)
        surf.blit(layer, (lo, ty))
        bw = int(tw * a.zone_w * 0.09)
        pygame.draw.rect(surf, (110, 170, 250), (int(tx + tw * a.zone) - bw, ty,
                                                 bw * 2, th), border_radius=4)
        a.sweep_drawn = a.sweep
        nx = int(tx + tw * a.sweep)
        pygame.draw.rect(surf, (10, 12, 20), (nx - 4, ty - 6, 8, th + 12),
                         border_radius=3)
        pygame.draw.rect(surf, (250, 250, 250), (nx - 2, ty - 4, 4, th + 8),
                         border_radius=2)
        ui.txt(surf, "%d / %d" % (a.hits, a.needed), (rect.right - pad, ty + 34), 13,
               True, C.GOLD, anchor="topright")

        hint = "SPACE when the marker crosses the green arc  ·  %d pulls to land" % a.needed
        ui.txt(surf, hint, (rect.centerx, rect.bottom - 26), 14, False, C.CREAM_DIM,
               anchor="midtop")
        if a.tension > 0.9:
            ui.txt(surf, "TOO TIGHT!", (rect.right - pad, ty - 26), 15, True,
                   (250, 120, 106), anchor="topright")

    def _prompt(self, surf):
        a = self.angler
        p = self.player
        cx, cy = C.W // 2, C.H - 128
        if a.state == "bite":
            txt = "SPACE  NOW"
        elif a.state == "idle" and p.bag_full and p.couch <= 0:
            txt = "COOLER FULL  ·  SHOP"
        elif self.hint_t > 0:
            txt = self.hint
        elif a.state == "idle" and p.couch <= 0:
            txt = "SPACE  cast"
        elif a.state == "wait" and self.tips > 3.0:
            txt = self.tip_text
        else:
            return
        a_ = 255
        if self.hint_t > 0:
            a_ = int(255 * min(1.0, self.hint_t))
        ui.txt(surf, txt, (cx, cy), 20, True if a.state == "bite" else False,
               C.CREAM if a.state != "bite" else (255, 232, 150), "midtop",
               alpha=a_, shadow=(10, 12, 22))

    def _couch(self, surf):
        a = int(120 + 60 * pulse(self.t, 1.6))
        ui.txt(surf, "COUCH LOCK", (C.W // 2, C.H // 2 - 40), 44, True, (248, 200, 140),
               "center", family="title")
        ui.txt(surf, "the dock is very comfortable right now", (C.W // 2, C.H // 2 + 12),
               18, False, (228, 210, 180), "center", alpha=a)

    def _draw_bite_marker(self, surf):
        x, y, s = self.world.bobber_pos(self.angler.depth)
        t = self.angler.bite_t
        pop = ease_out_back(min(1.0, t * 6.0))
        r = int(18 * pop)
        if r < 2:
            return
        yy = y - 46 * pop
        pygame.draw.circle(surf, (250, 236, 170), (int(x), int(yy)), r)
        pygame.draw.circle(surf, (60, 48, 24), (int(x), int(yy)), r, 3)
        ui.txt(surf, "!", (x, yy + 1), int(26 * pop) + 4, True, (54, 42, 20), "center")

    # ----------------------------------------------------------- catch card
    def _catch_card(self, surf):
        card = self.card
        sp = card["sp"]
        rar = card["rarity"]
        rcol = C.RARITY_COLORS[rar]
        self.dim(surf, 150)
        w, h = 780, 396
        e = ease_out_back(clamp(self.card_anim))
        scale = 0.92 + 0.08 * e
        alpha = int(255 * clamp(self.card_anim * 2))

        cw, chh = int(w * scale), int(h * scale)
        cs = pygame.Surface((w, h), pygame.SRCALPHA)
        pygame.draw.rect(cs, (20, 24, 37, 252), (0, 0, w, h), border_radius=22)
        pygame.draw.rect(cs, (54, 60, 84, 255), (0, 0, w, h), width=2, border_radius=22)
        # rarity glow strip
        glow = pygame.Surface((w, 6), pygame.SRCALPHA)
        glow.fill((rcol[0], rcol[1], rcol[2], 200))
        cs.blit(glow, (18, 0))

        # art panel
        art = pygame.Rect(28, 40, 330, 250)
        pygame.draw.rect(cs, (28, 33, 49, 255), art, border_radius=16)
        U.soft_glow(cs, art.center, 150, rcol, 44, steps=8)
        ui.draw_fish(cs, (art.x + 18, art.y + 60, art.w - 36, 130), sp)
        ui.chip(cs, C.RARITY_LABEL[rar], (art.centerx, art.bottom - 30), rcol, 13,
                anchor="center")

        tx = 388
        if card["new"]:
            0.6 + 0.4 * math.sin(self.card_t * 6)
            ui.chip(cs, "NEW SPECIES", (tx, 40), (250, 208, 120), 14, bg_alpha=50)
        ui.txt(cs, sp["name"], (tx, 72), 36, True, C.CREAM, family="title")
        ui.txt(cs, sp.get("sci", ""), (tx, 116), 13, False, (132, 140, 162))
        ui.txt(cs, "%d cm  ·  $%g/kg" % (int(round(card["cm"])), sp["usd_kg"]),
               (tx, 136), 18, False, (196, 200, 214))
        ui.txt(cs, "COOLER  %d / %d" % (card.get("kept", 1), card.get("capacity", 8)),
               (tx, 162), 14, True, (190, 196, 212))
        ui.coin(cs, (tx + 9, 196), 10)
        ui.txt(cs, "worth %s  ·  %d VP" % (fmt_money(card["value"]), card.get("vp", 0)),
               (tx + 24, 184), 19, True, C.GOLD)
        if card["bonus"]:
            ui.txt(cs, "+%s finder's fee" % fmt_money(card["bonus"]), (tx, 210), 13,
                   False, (196, 180, 140))
        U.text_wrapped(cs, sp["flavor"], (tx, 236), font(15), (176, 180, 196),
                             340, line_gap=4)
        ui.txt(cs, "ENTER or click to continue", (w // 2, h - 30), 15, False,
               (132, 138, 158), "midtop")
        scaled = pygame.transform.smoothscale(cs, (cw, chh))
        if alpha < 255:
            scaled.set_alpha(alpha)
        surf.blit(scaled, ((C.W - cw) // 2, (C.H - chh) // 2 + int((1 - e) * 18)))
