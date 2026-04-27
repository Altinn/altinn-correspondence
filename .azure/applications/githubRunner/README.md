# GitHub Runner Image

This folder contains a container image for self-hosted GitHub Actions runners in Azure Container Apps.

The image is built on Ubuntu and installs the official GitHub Actions runner binaries.  
At startup, it:

1. Requests a temporary registration token from GitHub using `GITHUB_TOKEN`.
2. Registers itself as an ephemeral runner for one repository (`GITHUB_URL`).
3. Executes one job and then exits.
4. Removes its runner registration on shutdown.

## Required Runtime Environment Variables

- `GITHUB_URL` (example: `https://github.com/Altinn/altinn-correspondence`)
- `GITHUB_TOKEN` (PAT/app token with permissions to manage self-hosted runners)

## Optional Runtime Environment Variables

- `RUNNER_NAME_PREFIX` (default: `aca-runner`)
- `LABELS` (default: `containerapps`)
- `RUNNER_WORKDIR` (default: `_work`)

## Build And Push (example)

```bash
docker build -f .azure/applications/githubRunner/Dockerfile -t ghcr.io/altinn/altinn-correspondence-github-runner:latest .azure/applications/githubRunner
docker push ghcr.io/altinn/altinn-correspondence-github-runner:latest
```

Use the pushed image value in `GITHUB_RUNNER_IMAGE` for your Bicep deployment.
