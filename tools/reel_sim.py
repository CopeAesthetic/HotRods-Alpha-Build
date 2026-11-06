"""Headless balance test for the reeling minigame."""
import os, sys, random
os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
pygame.init(); pygame.display.set_mode((10, 10))
from game import data
from game.player import Player
from game.world import World
from game.fishing import Angler

DT = 1 / 60.0

def run(diff, rod_id, line_id, skill=1.0, tries=200):
    p = Player(); p.rod = rod_id; p.line = line_id
    w = World(0)
    a = Angler(p, w)
    results = {"win": 0, "snap": 0, "timeout": 0}
    times = []
    for _ in range(tries):
        rd = data.RODS_BY_ID[rod_id]
        a._hard_reset()
        a.fish = {"diff": diff, "rarity": "rare", "shape": "torpedo", "id": "sim"}
        a.depth = 0.6
        a.state = "reel"
        a.band_half = max(0.055, 0.145 - 0.075 * diff + rd["tol"] * 0.30)
        a.tension = 0.42
        a.progress = 0.0
        a.over_t = 0.0
        a.wander_t = random.uniform(0.5, 2.0)
        a.band = 0.5
        a.band_target = 0.5
        t = 0.0
        lag = [0.0] * 8          # 130ms of reaction lag
        frame = 0
        while t < 30.0:
            lag.append(a.tension)
            seen = lag.pop(0)
            if frame % 3 == 0:   # decisions only every 50ms
                target = a.band + random.uniform(-0.03, 0.03) * (2 - skill)
                a.holding = seen < target - 0.015
            frame += 1
            a.update(DT)
            t += DT
            if a.state == "result":
                break
        if a.state != "result":
            results["timeout"] += 1
        elif a.result and a.result.get("fail") == "snap":
            results["snap"] += 1
        else:
            results["win"] += 1
            times.append(t)
    n = float(tries)
    return (
        "diff %.2f  rod %-6s line %-5s | win %5.1f%%  snap %5.1f%%  timeout %4.1f%%  t=%.1fs"
        % (diff, rod_id, line_id, 100 * results["win"] / n, 100 * results["snap"] / n,
           100 * results["timeout"] / n, (sum(times) / len(times)) if times else 0.0)
    )

if __name__ == "__main__":
    for diff in (0.12, 0.20, 0.34, 0.54, 0.66, 0.78, 0.90):
        print(run(diff, "bamboo", "light"))
    print()
    for rod, line in (("bamboo", "light"), ("fiber", "light"), ("carbon", "braid"),
                      ("moon", "ghost")):
        print(run(0.80, rod, line))
