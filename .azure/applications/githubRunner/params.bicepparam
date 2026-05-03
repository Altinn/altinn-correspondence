using './main.bicep'

param namePrefix = readEnvironmentVariable('NAME_PREFIX')
param location = 'norwayeast'
param environment = readEnvironmentVariable('ENVIRONMENT')

// secrets
param sourceKeyVaultName = readEnvironmentVariable('KEY_VAULT_NAME')
param keyVaultUrl = readEnvironmentVariable('KEY_VAULT_URL')

// GitHub runner settings
param githubUrl = readEnvironmentVariable('GITHUB_URL')
param runnerImage = readEnvironmentVariable('GITHUB_RUNNER_IMAGE', 'ghcr.io/altinn/altinn-correspondence-github-runner:latest')
