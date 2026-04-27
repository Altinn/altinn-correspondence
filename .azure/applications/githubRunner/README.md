# GitHub Runner Image

This folder contains a container image for self-hosted GitHub Actions runners in Azure Container Apps.

The image is built on Ubuntu and installs the official GitHub Actions runner binaries.  
At startup, it:

1. Requests a temporary registration token from GitHub using `GITHUB_TOKEN`.
2. Registers itself as an ephemeral runner for one repository (`GITHUB_URL`).
3. Executes one job and then exits.
4. Removes its runner registration on shutdown.

## Required GitHub Repository Secrets

The `manage-github-runners.yml` workflow expects these repository secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_NAME_PREFIX`
- `AZURE_ENVIRONMENT`
- `AZURE_ENVIRONMENT_KEY_VAULT_NAME`

The Azure Key Vault referenced by `AZURE_ENVIRONMENT_KEY_VAULT_NAME` must also contain:

- `github-runner-token` (GitHub PAT/app token with permissions to manage self-hosted runners)
