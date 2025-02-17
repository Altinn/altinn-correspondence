#!/bin/bash

# Parameters
DISPLAY_NAME="$1"

if [ -z "$DISPLAY_NAME" ]; then
    echo "Error: Display name is required" >&2
    exit 1
fi

# Get tenant ID
tenant_id=$(az account show --query tenantId -o tsv)
if [ -z "$tenant_id" ]; then
    echo "Error: Could not get tenant ID" >&2
    exit 1
fi

# Check if app registration exists
existing_app=$(az rest --method GET \
    --url "https://graph.microsoft.com/v1.0/applications?\$filter=displayName eq '$DISPLAY_NAME'" \
    --query "value[0]" -o json)

if [ "$existing_app" != "null" ]; then
    # App exists - update it
    app_id=$(echo $existing_app | jq -r '.appId')
    app_object_id=$(echo $existing_app | jq -r '.id')
    
    # Update the application
    az rest --method PATCH \
        --url "https://graph.microsoft.com/v1.0/applications/$app_object_id" \
        --headers "Content-Type=application/json" \
        --body '{
            "signInAudience": "AzureADMyOrg",
            "web": {
                "implicitGrantSettings": {
                    "enableIdTokenIssuance": true
                }
            }
        }'
else
    # Create new app
    echo "Creating new app registration"
    
    app_info=$(az rest --method POST \
        --url "https://graph.microsoft.com/v1.0/applications" \
        --headers "Content-Type=application/json" \
        --body "{
            \"displayName\": \"$DISPLAY_NAME\",
            \"signInAudience\": \"AzureADMyOrg\",
            \"web\": {
                \"implicitGrantSettings\": {
                    \"enableIdTokenIssuance\": true
                }
            }
        }")
    
    app_id=$(echo $app_info | jq -r '.appId')
fi

# Check if service principal exists
existing_sp=$(az rest --method GET \
    --url "https://graph.microsoft.com/v1.0/servicePrincipals?\$filter=appId eq '$app_id'" \
    --query "value[0]" -o json)

if [ "$existing_sp" == "null" ]; then
    az rest --method POST \
        --url "https://graph.microsoft.com/v1.0/servicePrincipals" \
        --headers "Content-Type=application/json" \
        --body "{
            \"appId\": \"$app_id\"
        }"
fi

# Output both app ID and tenant ID in a format easy to parse
echo "EVENT_GRID_CLIENT_ID=$app_id"
echo "EVENT_GRID_TENANT_ID=$tenant_id"