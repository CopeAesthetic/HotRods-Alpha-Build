"""Render a contact sheet of every fish (visual QA)."""
import os
import sys

os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import pygame
pygame.init()
pygame.display.set_mode((100, 100))
from game import data, ui, config as C

COLS, ROWS = 4, 8
CW, CH = 320, 130
W, H = CW * COLS, CH * ROWS
surf = pygame.Surface((W, H))
surf.fill((24, 28, 42))
fnt = ui.font(20, True)
for i, sp in enumerate(data.SPECIES):
    col, row = i % COLS, i // COLS
    x, y = col * CW, row * CH
    pygame.draw.rect(surf, (30, 36, 54), (x + 2, y + 2, CW - 4, CH - 4), border_radius=10)
    ui.draw_fish(surf, (x + 10, y + 10, 170, 80), sp)
    ui.txt(surf, sp["name"], (x + 186, y + 16), 17, True)
    ui.chip(surf, C.RARITY_LABEL[sp["rarity"]], (x + 186, y + 44),
            C.RARITY_COLORS[sp["rarity"]], 12)
    ui.txt(surf, sp["shape"] + " / " + sp["pattern"], (x + 186, y + 76), 13, False,
           C.TEXT_DIM)
    ui.txt(surf, sp["id"], (x + 186, y + 96), 12, False, (110, 116, 138))
pygame.image.save(surf, "/tmp/fishsheet.png")
print("saved", W, H)
