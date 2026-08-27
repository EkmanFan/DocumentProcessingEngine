# Local Manager custody root

This directory is reserved for local manual and end-to-end Manager custody
evidence. Original PDF, EPUB and derived custody artifacts are intentionally
ignored by Git and must never be committed.

Automated tests use isolated temporary directories instead of this location.
Manual composition should use distinct `sources/` and `results/` subdirectories
so source custody and encoded result custody remain operationally separate.
