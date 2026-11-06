#!/usr/bin/env bash
# Play Catch & Chill. Creates a local venv on first run.
set -e
cd "$(dirname "$0")"
if [ ! -x .venv/bin/python ]; then
  echo "Creating virtualenv..."
  python3 -m venv .venv
  ./.venv/bin/python -m pip install --quiet --upgrade pip
  ./.venv/bin/python -m pip install --quiet -r requirements.txt
fi
exec ./.venv/bin/python main.py "$@"
