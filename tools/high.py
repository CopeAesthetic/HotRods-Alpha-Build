import os, sys
os.environ.setdefault("SDL_VIDEODRIVER", "dummy"); os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import pygame
import game.player as player_mod
player_mod.SAVE_PATH = '/tmp/qa_save.json'
import os as _os
if _os.path.exists('/tmp/qa_save.json'): _os.remove('/tmp/qa_save.json')
from game.app import App
from game.screens.play import PlayScreen
app = App(); app.audio.set_muted(True)
app.has_save = False
from game.player import Player
app.player = Player()
app.new_world(0)
play = PlayScreen(app); app.stack[-1] = play
app.player.chill = 88.0
app.player.hour = 18.9
app.player.weed = {"homegrown": 4}
for i in range(90):
    play.update(1/60.0)
app.draw(); pygame.image.save(app.canvas, "/tmp/shot_high.png")
print("high shot ok, chill", app.player.chill)
