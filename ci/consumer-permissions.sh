#!/usr/bin/env bash
# Transfer only the generated CI workspace between the container and host runner.
set -euo pipefail

consumer_work=$(realpath -- "${1:?consumer workspace required}")
consumer_checkout=$(realpath -- "${GITHUB_WORKSPACE:?checkout required}")
if [[ "$consumer_work" != "$consumer_checkout"/artifacts/* ||
      ! -f "$consumer_work/run.json" || ! -f "$consumer_work/candidate/candidate.json" ]]; then
  echo 'Refusing ownership changes outside a generated artifacts workspace.' >&2
  exit 1
fi
case "${2:?owner required}" in
  container) consumer_owner=0:0 ;;
  runner) consumer_owner="$(id -u):$(id -g)" ;;
  *) echo 'Expected container or runner owner.' >&2; exit 1 ;;
esac

# Root is required by the pinned image's license setup. Git also checks ownership
# of our temporary distribution repositories. Never follow links out of this tree.
sudo chown -R -P --no-dereference "$consumer_owner" -- "$consumer_work"
