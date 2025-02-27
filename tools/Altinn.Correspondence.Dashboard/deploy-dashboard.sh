#!/bin/bash

# Pre-requisites
# Install gh (sudo apt install gh)
# Install az tools (sudo apt install az), and run az login then select the correct subscription

# Script to deploy Hangfire container app. 
# This script will create a new App Registration and Service Principal, and deploy the Dashboard container app.
# It will also configure authentication for the container app.
# This was otherwise not possible to do using bicep as we usually would, hence the script as it will run as a user who can make service principals

# Exit on error
set -e

# Default values
ENVIRONMENT_NAME=""

# Function to display usage
usage() {
    echo "Usage: $0 -e <environment-name>"
    echo "Example: $0 -e test"
    exit 1
}

# Parse command line arguments
while getopts "e:i:" opt; do
    case $opt in
        e) ENVIRONMENT_NAME="$OPTARG";;
        ?) usage;;
    esac
done

# Validate required parameters
if [ -z "$ENVIRONMENT_NAME" ]; then
    echo "Error: Missing required parameters"
    usage
fi

# Get the latest git tag
GIT_TAG=$(git describe --tags --abbrev=0)
if [ -z "$GIT_TAG" ]; then
    echo "Error: No git tag found"
    exit 1
fi

# Set constants
LOCATION="Norway East"
APP_NAME="altinn-corr-${ENVIRONMENT_NAME}-dashboard-app"
RESOURCE_GROUP="altinn-corr-${ENVIRONMENT_NAME}-rg"
DOCKER_IMAGE_NAME="ghcr.io/altinn/altinn-correspondence-dashboard:${GIT_TAG}"

echo "Building and pushing Hangfire Dashboard container..."
echo "Building Docker image..."
docker build -t $DOCKER_IMAGE_NAME -f Dockerfile ../..

# Login to GitHub packages
echo "Logging in to GitHub packages..."
gh auth login -w -s write:packages
gh auth setup-git
echo "Our username"
echo $(gh api user --jq '.login')
echo "Our password"
echo $(gh auth token)

docker login ghcr.io -u $(gh api user --jq '.login') -p $(gh auth token)

# Push the image
echo "Pushing image to GitHub packages..."
docker push $DOCKER_IMAGE_NAME

# Set the image parameter to the newly built image

echo "Creating Container App with authentication..."
echo "Using Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
echo "App Name: $APP_NAME"

# Create the App Registration
echo "Creating App Registration..."
APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId --output tsv)
echo "App Registration created with ID: $APP_ID"

# Create service principal for the app
echo "Creating Service Principal..."
az ad sp create --id $APP_ID

# Generate a secret for the app registration
echo "Generating client secret..."
CLIENT_SECRET=$(az ad app credential reset --id $APP_ID --query password --output tsv)

# Create Container Apps Environment if it doesn't exist
echo "Creating Container Apps Environment..."
az containerapp env create \
    --name "$ENVIRONMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION"

# Create the Container App
echo "Creating Container App..."
az containerapp create \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --environment "$ENVIRONMENT_NAME" \
    --image "$DOCKER_IMAGE_NAME" \
    --target-port 80 \
    --ingress external

# Configure authentication
echo "Configuring authentication..."
az containerapp auth microsoft update \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --client-id "$APP_ID" \
    --client-secret "$CLIENT_SECRET" \
    --allowed-audiences "api://$APP_ID"

echo "Container App deployment complete!"
echo "App Registration ID: $APP_ID"
echo "Please save these credentials securely."

