#!/usr/bin/env bash
# Export only the reviewed licensing client; never launch an Editor or activate here.
set -euo pipefail

consumer_work=$(realpath -- "${1:?consumer workspace required}")
consumer_checkout=$(realpath -- "${GITHUB_WORKSPACE:?checkout required}")
consumer_scripts=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
if [[ "$consumer_work" != "$consumer_checkout"/artifacts/* ||
      ! -f "$consumer_work/run.json" || ! -f "$consumer_work/candidate/candidate.json" ]]; then
  echo 'Refusing licensing preparation outside a generated artifacts workspace.' >&2
  exit 1
fi
python3 - "$consumer_work/run.json" <<'PYPROFILE'
import json, sys
state = json.load(open(sys.argv[1]))
assert (state['profile'], state['unity'], state['buildTarget']) == ('2021-legacy', '2021.3.0f1', 'StandaloneLinux64'), 'Unsupported licensing profile'
PYPROFILE

if [[ -e "$consumer_work/licensing" || -L "$consumer_work/licensing" ]]; then
  python3 "$consumer_scripts/consumer-project.py" verify-licensing --work "$consumer_work"
  exit 0
fi

consumer_image='unityci/editor@sha256:7b001255b94971fa056d67966eed2191dd892cf23d948233f87e711b615a9913'
consumer_name="consumer-licensing-$(python3 -c 'import uuid; print(uuid.uuid4().hex)')"
consumer_remove_image=false
if ! timeout 60s docker image inspect "$consumer_image" >/dev/null 2>&1; then
  consumer_remove_image=true
fi
consumer_cleanup() {
  consumer_result=$?
  trap - EXIT
  # This unique container was created only to copy files and was never started.
  timeout 60s docker rm "$consumer_name" >/dev/null 2>&1 || true
  if [[ "$consumer_remove_image" == true ]]; then
    timeout 120s docker image rm "$consumer_image" >/dev/null 2>&1 || true
  fi
  exit "$consumer_result"
}
trap consumer_cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

timeout 600s docker pull --quiet --platform linux/amd64 "$consumer_image"
timeout 60s docker create --name "$consumer_name" --platform linux/amd64 \
  --network none --entrypoint /bin/bash "$consumer_image" >/dev/null
mkdir -- "$consumer_work/licensing"
timeout 120s docker cp "$consumer_name:/opt/unity/Editor/Data/Resources/Licensing/Client" "$consumer_work/licensing/Client"
python3 "$consumer_scripts/consumer-project.py" verify-licensing --work "$consumer_work"
