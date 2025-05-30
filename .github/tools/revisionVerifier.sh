if [ -z "$1" ]; then
  echo "Usage: $0 <revision-name>"
  exit 1
fi

if [ -z "$2" ]; then
  echo "Usage: $0 <resource-group-name>"
  exit 1
fi

if [ -z "$3" ]; then
  echo "Usage: $0 <container-app-name>"
  exit 1
fi

revision_name="$1"
resource_group="$2"
container_app_name="$3"
query_filter="{name:name, runningState:properties.runningState, healthState:properties.healthState}"

verify_revision() {
  local json_output
  
  # Fetch app revision
  json_output=$(az containerapp revision show -g "$resource_group" -n "$container_app_name" --revision "$revision_name" --query "$query_filter" 2>/dev/null)
  
  health_state=$(echo $json_output | jq -r '.healthState')
  running_state=$(echo $json_output | jq -r '.runningState')

  echo "Revision $revision_name status:"
  echo "-----------------------------"
  echo "Health state: $health_state"
  echo "Running state: $running_state"
  echo " "
  
  # Check health and running status
  if [[ $health_state == "Healthy" && ($running_state == "Running" || $running_state == "RunningAtMaxScale") ]]; then
    return 0  # OK!
  else
    if [[ $running_state == "Failed" ]]; then
      echo "Revision $revision_name failed. Exiting script."
      exit 1
    fi 
    return 1  # Not OK!
  fi
}

attempt=1

# Loop until verified (GitHub action will do a timeout)
while true; do
  if verify_revision; then
    echo "Revision $revision_name is healthy and running"
    break
  else
    echo "Attempt $attempt: Waiting for revision $revision_name ..."
    sleep 10 # Sleep for 10 seconds
    attempt=$((attempt+1))
    if [[ $attempt -gt 25 ]]; then
      echo "Revision $revision_name did not start in time. Exiting script."
      exit 1
    fi
  fi
done