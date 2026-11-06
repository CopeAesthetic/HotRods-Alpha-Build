"""The field journal - the collect-a-thon screen."""
from __future__ import annotations

import pygame

from .. import config as C
from .. import data

SHINY_OF = {sh["shiny_of"]: sh for sh in data.SHINY_FISH}
from .. import ui
from .. import util as U
from ..util import clamp, mix, font
from .base import Screen, fmt_kg


class JournalScreen(Screen):
    overlay = True

    def __init__(self, app):
        super().__init__(app)
        self.spot = app.player.spot
        self.build()

    def build(self):
        self.buttons = []
        self.rect = self.panel_rect(1180, 664)
        r = self.rect
        self.add_close(r)
        for i, sp in enumerate(data.SPOTS):
            label = sp["name"] if i in self.player.unlocked else "???"
            b = ui.Button((r.x + 34 + i * 222, r.y + 92, 206, 38), label,
                          style="tab", size=15, action=lambda i=i: self.set_spot(i))
            b.selected = (i == self.spot)
            b.enabled = (i in self.player.unlocked)
            self.buttons.append(b)
        if all(sp["id"] in self.player.species for sp in data.SPECIES):
            leg = ui.Button((r.x + 34 + 4 * 222, r.y + 92, 206, 38), "LEGENDS",
                            style="tab", size=15, action=lambda: self.set_spot(4))
            leg.selected = (self.spot == 4)
            self.buttons.append(leg)

    def legends_open(self):
        return all(sp["id"] in self.player.species for sp in data.SPECIES)

    def set_spot(self, i):
        self.spot = 0 if (i == 4 and not self.legends_open()) else i
        self.build()

    def handle_event(self, event):
        if super().handle_event(event):
            return
        if event.type == pygame.KEYDOWN:
            if event.key in (pygame.K_ESCAPE, pygame.K_j):
                self.app.pop()
            elif event.key == pygame.K_RIGHT:
                self.set_spot((self.spot + 1) % len(data.SPOTS))
            elif event.key == pygame.K_LEFT:
                self.set_spot((self.spot - 1) % len(data.SPOTS))

    def draw(self, surf):
        p = self.player
        self.dim(surf, 160)
        r = self.rect
        legends = self.spot == 4
        pool = (list(data.HIDDEN_FISH) + list(data.SHINY_FISH)) if legends \
                else data.fish_for_spot(self.spot)
        caught_here = sum(1 for s in pool if p.caught(s["id"]))
        fish_here = len(pool)
        self.draw_frame(surf, r, "FIELD JOURNAL",
                        "%d / %d species found" % (p.found_count, p.total_count))
        for b in self.buttons:
            b.draw(surf, self.mouse)

        if legends:
            blurb = ("The ones that are not supposed to exist. Each only bites on "
                     "the top strain of another water.")
        else:
            blurb = data.SPOTS[self.spot]["blurb"]
        ui.txt(surf, blurb, (r.x + 34, r.y + 138), 15, False, C.TEXT_DIM)

        fish = pool
        cw, ch = 262, 206
        gx, gy = r.x + 32, r.y + 170
        for i, sp in enumerate(fish):
            col, row = i % 4, i // 4
            x = gx + col * (cw + 16)
            y = gy + row * (ch + 14)
            self._card(surf, (x, y, cw, ch), sp)

        # footer
        fy = r.bottom - 56
        if legends:
            done = caught_here / float(fish_here)
        else:
            done = p.found_count / float(p.total_count)
        label = ("%d of %d legends found" % (caught_here, fish_here) if legends
                 else "%d of %d species discovered" % (p.found_count, p.total_count))
        ui.txt(surf, label, (r.x + 34, fy), 16, True, C.CREAM)
        U.bar(surf, (r.x + 34, fy + 26, 420, 10), done,
              mix((120, 190, 130), C.GOLD, done), radius=5)
        ui.txt(surf, "%d%%" % int(done * 100), (r.x + 470, fy + 20), 16, True, C.GOLD)
        ui.txt(surf, ("%d / %d in this water" % (caught_here, fish_here)) if not legends
               else "%d / %d in this journal" % (p.found_count, p.total_count),
               (r.right - 34, fy + 22), 15, False, C.TEXT_DIM, anchor="topright")

    def _card(self, surf, rect, sp):
        rect = pygame.Rect(rect)
        p = self.player
        rec = p.species.get(sp["id"])
        col = C.RARITY_COLORS[sp["rarity"]]
        bg = (29, 34, 50) if rec else (24, 28, 41)
        pygame.draw.rect(surf, bg, rect, border_radius=14)
        border = col if rec else (44, 49, 68)
        pygame.draw.rect(surf, border, rect, width=2 if rec else 1, border_radius=14)
        if rec:
            glow = pygame.Surface((rect.w, 4), pygame.SRCALPHA)
            glow.fill((col[0], col[1], col[2], 160))
            surf.blit(glow, (rect.x + 14, rect.y))

        ui.draw_fish(surf, (rect.x + 14, rect.y + 16, rect.w - 28, 104), sp,
                     ghost=not rec, dim=False)
        if rec:
            ui.txt(surf, U.truncate(sp["name"], font(18, True), rect.w - 28),
                   (rect.x + 14, rect.y + 120), 18, True, C.CREAM)
            ui.txt(surf, U.truncate(sp.get("sci", ""), font(10), rect.w - 28),
                   (rect.x + 14, rect.y + 142), 10, False, (118, 126, 146))
            ui.txt(surf, C.RARITY_LABEL[sp["rarity"]].upper(),
                   (rect.centerx, rect.y + 6), 9, True, col, anchor="midtop")
            ui.txt(surf, "x%d" % rec["count"], (rect.right - 14, rect.y + 158), 13,
                   False, C.TEXT_DIM, anchor="topright")
        else:
            ui.txt(surf, "???", (rect.x + 14, rect.y + 124), 20, True, (108, 114, 136))
            ui.txt(surf, "Uncaught", (rect.x + 14, rect.y + 150), 12, True,
                   (96, 102, 124))
        U.text_wrapped(surf, sp.get("hint", "Dab the hidden strain of this water."), (rect.x + 14, rect.y + 170),
                           font(12), (120, 126, 148), rect.w - 28, line_gap=1,
                           max_lines=2)
