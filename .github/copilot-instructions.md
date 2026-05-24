# Copilot Instructions

## Code style

- Do not add comments that describe the current investigation, debugging session, or reasoning
  process. Comments should explain *what* code does or *why* a design decision was made, not
  document an ongoing investigation.
- Do not include "issue #N", "triangulate", "hypothesis", or similar investigation language in
  inline code comments or block comments inside method bodies. Such context belongs in the pull
  request description, not in the source code.

## Documentation and tests

- Always update documentation when public APIs change.
- Always update `README.md` when notable behavior or features are added or modified.
- Always add or update tests for behavior changes.
