#!/bin/bash

# Parameters
DISPLAY_NAME="$1"

# Get tenant ID
tenant_id=$(az account show --query tenantId -o tsv)

if [ -z "$tenant_id" ]; then
    echo "Error: Could not get tenant ID" >&2
    exit 1
fi

# Check if app registration exists
existing_app=$(az ad app list --display-name "$DISPLAY_NAME" --query "[0]" -o json)

if [ "$existing_app" != "null" ]; then
    # App exists - update it
    app_id=$(echo $existing_app | jq -r '.appId')
    app_object_id=$(echo $existing_app | jq -r '.id')
    
    az ad app update \
        --id "$app_object_id" \
        --sign-in-audience "AzureADMyOrg" \
        --enable-id-token-issuance true
else
    # Create new app
    
    app_info=$(az ad app create \
        --display-name "$DISPLAY_NAME" \
        --sign-in-audience "AzureADMyOrg" \
        --enable-id-token-issuance true)
    
    app_id=$(echo $app_info | jq -r '.appId')
fi

# Check if service principal exists
existing_sp=$(az ad sp list --filter "appId eq '$app_id'" --query "[0]" -o json)

if [ "$existing_sp" == "null" ]; then
    az ad sp create --id "$app_id"
fi

# Output both app ID and tenant ID in a format easy to parse
echo "EVENT_GRID_CLIENT_ID=$app_id"
echo "EVENT_GRID_TENANT_ID=$tenant_id"