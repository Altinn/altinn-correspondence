#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${GITHUB_URL:-}" ]]; then
  echo "GITHUB_URL is required."
  exit 1
fi

if [[ -z "${GITHUB_TOKEN:-}" ]]; then
  echo "GITHUB_TOKEN is required."
  exit 1
fi

if [[ "${GITHUB_URL}" != https://github.com/* ]]; then
  echo "GITHUB_URL must start with https://github.com/."
  exit 1
fi

repo_path="${GITHUB_URL#https://github.com/}"
repo_path="${repo_path%/}"
IFS='/' read -r -a parts <<< "${repo_path}"
owner=""
repo=""
if [[ ${#parts[@]} -eq 2 ]]; then
  owner="${parts[0]}"
  repo="${parts[1]}"
fi

if [[ ${#parts[@]} -ne 2 || -z "${owner}" || -z "${repo}" ]]; then
  echo "GITHUB_URL must be in the format https://github.com/<owner>/<repo>."
  exit 1
fi

runner_name="runner-$(hostname)-${RANDOM}"
work_folder="_work"

api_url="https://api.github.com/repos/${owner}/${repo}/actions/runners"
common_headers=(
  -H "Accept: application/vnd.github+json"
  -H "Authorization: Bearer ${GITHUB_TOKEN}"
  -H "X-GitHub-Api-Version: 2022-11-28"
)

echo "Requesting registration token for ${owner}/${repo}..."
registration_token="$(
  curl -fsSL -X POST "${common_headers[@]}" "${api_url}/registration-token" | jq -r '.token'
)"

if [[ -z "${registration_token}" || "${registration_token}" == "null" ]]; then
  echo "Failed to fetch runner registration token."
  exit 1
fi

remove_runner() {
  set +e
  echo "Removing runner registration..."
  remove_token="$(curl -fsSL -X POST "${common_headers[@]}" "${api_url}/remove-token" | jq -r '.token')"
  if [[ -n "${remove_token}" && "${remove_token}" != "null" ]]; then
    ./config.sh remove --unattended --token "${remove_token}" >/dev/null 2>&1
  fi
}

trap remove_runner EXIT INT TERM

./config.sh \
  --unattended \
  --replace \
  --ephemeral \
  --name "${runner_name}" \
  --work "${work_folder}" \
  --url "${GITHUB_URL}" \
  --token "${registration_token}"

echo "Runner ${runner_name} configured. Waiting for a single job..."
./run.sh
