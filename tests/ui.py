"""Page-object helpers: find and press UI controls by their label.

Index-based clicking is what made the old smoke test go stale every time a
tab moved; these helpers survive layout changes.
"""
from __future__ import annotations

import pygame


def find(screen, text):
    """First button whose label contains `text` (case-insensitive)."""
    text = text.lower()
    for b in getattr(screen, "buttons", []):
        if text in str(b.label).lower():
            return b
    raise AssertionError("no button matching %r in %s (have: %s)"
                         % (text, type(screen).__name__,
                            [b.label for b in getattr(screen, "buttons", [])]))


def click(app, screen, text):
    b = find(screen, text)
    assert b.enabled, "button %r is disabled" % b.label
    screen.mouse = b.rect.center
    screen.handle_event(pygame.event.Event(pygame.MOUSEBUTTONDOWN, button=1,
                                           pos=b.rect.center))


def press(app, screen, key):
    screen.handle_event(pygame.event.Event(pygame.KEYDOWN, key=key, unicode=""))
