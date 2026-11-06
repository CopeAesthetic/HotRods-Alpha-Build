.PHONY: check test run
check:
	./.venv/bin/ruff check game main.py tools tests
	./.venv/bin/mypy game

test:
	SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy ./.venv/bin/python tests/screens.py
	SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy ./.venv/bin/python tests/test_flow.py
	SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy ./.venv/bin/python tests/test_soak.py

run:
	./run.sh
