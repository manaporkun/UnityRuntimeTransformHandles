#!/usr/bin/env bash
# Build the derived minimum Editor image: the pinned 2021.3.0f1 image with only
# its bundled Licensing/Client replaced by the reviewed 1.18.1 tree exported by
# consumer-licensing.sh. The Editor always launches the client at that fixed
# path, and the bundled 1.9 client cannot consume a current entitlement license
# (measured in CI: the seat activates, then the Editor rejects it). Prints the
# local image tag on stdout; build output goes to stderr.
set -euo pipefail

consumer_work=$(realpath -- "${1:?consumer workspace required}")
consumer_base="${2:?base image required}"
consumer_checkout=$(realpath -- "${GITHUB_WORKSPACE:?checkout required}")
consumer_scripts=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
consumer_pinned_base='unityci/editor@sha256:6ca652f117ddbec52602ae3079df3cefc9fd2a4ec1282e07c8fbd638ef102eb6'
consumer_tag='consumer-minimum-editor:2021.3.0f1-client1.18.1'
if [[ "$consumer_work" != "$consumer_checkout"/artifacts/* ||
      "$consumer_base" != "$consumer_pinned_base" ||
      ! -f "$consumer_work/run.json" || ! -d "$consumer_work/licensing/Client" ]]; then
  echo 'Refusing derived-image build outside a generated artifacts workspace or from an unpinned base.' >&2
  exit 1
fi
python3 - "$consumer_work/run.json" <<'PYPROFILE'
import json, sys
state = json.load(open(sys.argv[1]))
assert (state['profile'], state['unity'], state['buildTarget']) == ('2021-legacy', '2021.3.0f1', 'StandaloneLinux64'), 'Unsupported derived-image profile'
PYPROFILE
python3 "$consumer_scripts/consumer-project.py" verify-licensing --work "$consumer_work" >&2

if timeout 60s docker image inspect "$consumer_tag" >/dev/null 2>&1; then
  printf '%s\n' "$consumer_tag"
  exit 0
fi

timeout 1200s docker build --platform linux/amd64 --tag "$consumer_tag" \
  --file - "$consumer_work/licensing" >&2 <<EOF
FROM $consumer_pinned_base
RUN rm -rf /opt/unity/Editor/Data/Resources/Licensing/Client
COPY Client/ /opt/unity/Editor/Data/Resources/Licensing/Client/
RUN chmod 755 /opt/unity/Editor/Data/Resources/Licensing/Client/Unity.Licensing.Client
EOF
printf '%s\n' "$consumer_tag"
