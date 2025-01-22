#!/bin/bash

# Script to upload migration files to Azure Storage
# Usage: ./upload-migration-files.sh <storage_account> <container_name> <migration_bundle_path> <appsettings_path>

set -euo pipefail

if [ "$#" -ne 4 ]; then
    echo "Error: Missing required parameters"
    echo "Usage: $0 <storage_account> <container_name> <migration_bundle_path> <appsettings_path>"
    exit 1
fi

STORAGE_ACCOUNT="$1"
CONTAINER_NAME="$2"
MIGRATION_BUNDLE_PATH="$3"
APPSETTINGS_PATH="$4"

# Function to check if file exists
check_file() {
    if [ ! -f "$1" ]; then
        echo "Error: File not found: $1"
        exit 1
    fi
}

# Function to upload a file with retries
upload_file() {
    local source="$1"
    local filename=$(basename "$source")
    local max_retries=3
    local retry_count=0
    local wait_time=5

    while [ $retry_count -lt $max_retries ]; do
        if az storage blob upload \
            --account-name "$STORAGE_ACCOUNT" \
            --auth-mode login \
            --container-name "$CONTAINER_NAME" \
            --file "$source" \
            --name "$filename" \
            --overwrite true; then
            echo "Successfully uploaded: $filename"
            return 0
        else
            retry_count=$((retry_count + 1))
            if [ $retry_count -lt $max_retries ]; then
                echo "Upload failed for $filename. Retrying in $wait_time seconds... (Attempt $retry_count of $max_retries)"
                sleep $wait_time
                wait_time=$((wait_time * 2))
            else
                echo "Error: Failed to upload $filename after $max_retries attempts"
                return 1
            fi
        fi
    done
}

# Main execution
echo "Starting migration files upload..."

# Check if files exist
check_file "$MIGRATION_BUNDLE_PATH"
check_file "$APPSETTINGS_PATH"

# Create container if it doesn't exist
echo "Ensuring container exists..."
az storage container create \
    --account-name "$STORAGE_ACCOUNT" \
    --auth-mode login \
    --name "$CONTAINER_NAME" \
    --fail-on-exist false || {
    echo "Error: Failed to create/verify container"
    exit 1
}

# Upload files
echo "Uploading migration bundle..."
upload_file "$MIGRATION_BUNDLE_PATH" || exit 1

echo "Uploading appsettings..."
upload_file "$APPSETTINGS_PATH" || exit 1

echo "Upload completed successfully"