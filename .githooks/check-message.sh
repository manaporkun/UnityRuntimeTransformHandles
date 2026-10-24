#!/bin/sh
# Single source of truth for the commit-message policy, shared by the local
# commit-msg hook and .github/workflows/commit-hygiene.yml.
#
# Usage: check-message.sh <file-with-message>
# Exit status: 0 clean, 1 policy violation, 2 bad invocation.
#
# The rules reject attribution to AI assistants and tools, because that surfaces as a
# co-author and pollutes the repository's contributor list. "cursor" is only matched in
# attribution contexts: it is an ordinary Unity API word (Cursor.lockState, mouse cursor).
set -eu

file=${1:-}
if [ -z "$file" ] || [ ! -f "$file" ]; then
  echo "check-message.sh: expected a commit message file as the first argument" >&2
  exit 2
fi

# Escape hatch for the repository owner when replaying old history on purpose.
if [ "${SKIP_COMMIT_HYGIENE:-0}" = "1" ]; then
  exit 0
fi

trailer='co-authored-by:.*(claude|cursor|codex|copilot|gemini|anthropic|openai)'
signature='(generated|made|written|authored) (with|by) .*(claude|cursor|codex|copilot)'
signature="$signature|claude\.(ai|com)|anthropic\.com|openai\.com|chatgpt\.com|copilot\.github|cursor\.com|cursoragent"
tools='claude|codex|copilot|windsurf|bugbot|anthropic|openai|chatgpt'

status=0

if grep -Eiq "$trailer|$signature" "$file"; then
  echo "Commit message rejected: it carries an AI-assistant/tool attribution line." >&2
  grep -Ein "$trailer|$signature" "$file" | sed 's/^/  offending: /' >&2
  status=1
elif grep -Eiq "$tools" "$file"; then
  echo "Commit message rejected: it names an AI assistant or tool." >&2
  grep -Ein "$tools" "$file" | sed 's/^/  offending: /' >&2
  status=1
fi

if [ "$status" -ne 0 ]; then
  cat >&2 <<'EOF'

Keep commit messages about the change itself: no "generated with" footers, no
Co-Authored-By trailers naming a tool, no tool names in the subject or body.
Rewrite the message and commit again.
EOF
fi

exit "$status"
