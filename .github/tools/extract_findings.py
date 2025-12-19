import requests
import sys
import json
import re

#Added functions from polarislib.py to avoid import issues (now we dont need polarislib.py)
def getresp(session, api, params=None, headers=None):
    if params == None:
        params = {}
    if headers == None:
        headers = {}
    response = session.get(api, params=params, headers=headers)
    if (response.status_code >= 300):
        print("ERROR: GET failed: ", api)
        print("Response: ", response)
        try: 
            print(response.json())
        except: 
            print("No json data returned")
        sys.exit(1)
    return(response.json())

def getNextAndFirst(links):
    nextlink = None
    firstlink = None
    for l in links:
        if l['rel'] == 'next':
            nextlink = l['href']
        if l['rel'] == 'first':
            firstlink = l['href']
    return nextlink, firstlink

def fixAuthUrl(url, nextUrl):
    regex = re.escape(url) + r'/users(.*)'
    match = re.match(regex, nextUrl)
    if match is None: 
        return(nextUrl)
    fixedUrl =  url + "/api/auth/users" + match.group(1)
    return fixedUrl

def apigetitems(session, url, endpoint, params=None, headers=None):
    if params == None:
        params = {}
    api = url+endpoint
    json = getresp(session, api, params, headers)
    
    data = json['_items']
    nextpage, firstpage = getNextAndFirst(json['_links'])
    while nextpage:
        if nextpage == firstpage:
            
            return(data)
        nextpage = fixAuthUrl(url, nextpage)
        json = getresp(session, nextpage)
       
        data.extend(json['_items'])
        nextpage, firstpage = getNextAndFirst(json['_links'])
    return(data)

def getIssues(session, url, pid, params=None):
    if params is None:
        params = {}
    params['projectId'] = pid
    params['_includeIssueProperties'] = 'true'
    params['_includeType'] = 'true'
    params['_includeTriageProperties'] = 'true'
    params['_includeOccurrenceProperties'] = 'true'
    params['_includeContext'] = 'true' 
    resp = apigetitems(session, url, "/api/findings/issues", params)
    try:
        return(resp)
    except:
        print(f"ERROR: No issues found")
        sys.exit(1)

def createSession(url, token):
    headers = {'API-TOKEN': token}
    s = requests.Session()
    s.headers.update(headers)
    return s

def fetch_projects(session, url, portfolio_id, limit=100):
    endpoint = f"/api/portfolios/{portfolio_id}/projects?_limit={limit}"
    headers = {
        "accept": "application/vnd.polaris.portfolios.projects-1+json"
    }
    response = session.get(url + endpoint, headers=headers)
    if response.status_code != 200:
        print("Failed to fetch projects:", response.status_code)
        print(response.text)
        sys.exit(1)
    return response.json()


def main():
    if len(sys.argv) < 4:
        print("Usage: python extract_findings.py <polaris_url> <api_token> <portfolio_id> [project_id]")
        sys.exit(1)

    url = sys.argv[1]
    token = sys.argv[2]
    portfolio_id = sys.argv[3]

    session = createSession(url, token)
    projects_data = fetch_projects(session, url, portfolio_id)
    projects = projects_data.get('_items', [])
    if not projects:
        print("No projects found.")
        sys.exit(1)

    if len(sys.argv) > 4:
        project_id = sys.argv[4]
    else:
        project_id = projects[0].get('id')

    project_ids = [proj.get('id') for proj in projects]
    if project_id not in project_ids:
        print(f"Invalid project_id {project_id}. Not found in available projects.")
        sys.exit(1)
    selected_proj = next(proj for proj in projects if proj.get('id') == project_id)
    project_name = selected_proj.get('name')
    
    # Extract application ID for building issue links
    application_id = selected_proj.get('application', {}).get('id')
    if not application_id:
        print(f"Error: No application ID found for project '{project_name}'. Please check the project Id input")
        sys.exit(1) 

    # Remove old output files if they exist
    import os
    json_path = "issues_output.json"
    sarif_path = "polaris_issues.sarif"
    for path in [json_path, sarif_path]:
        if os.path.exists(path):
            os.remove(path)

    # Fetch issues from the selected project
    issues = getIssues(session, url, project_id, None)
    print(f"\nFound {len(issues)} issues for project '{project_name}':")
    with open(json_path, "w") as f:
        json.dump(issues, f, indent=2)
    print(f"Issues written to {json_path}")

    # Build SARIF file in the requested format
    sarif = {
        "version": "2.1.0",
        "runs": [{
            "tool": {
                "driver": {
                    "name": "DAST-Scanner",
                    "rules": []
                }
            },
            "artifacts": [],
            "results": []
        }]
    }

    rule_id_map = {}  # Map rule_id to ruleIndex
    artifact_map = {}  # Map file_path to artifact index
    rules = sarif["runs"][0]["tool"]["driver"]["rules"]
    artifacts = sarif["runs"][0]["artifacts"]
    results = sarif["runs"][0]["results"]

    for issue in issues:
        # Skip dismissed issues
        triage_props = issue.get("triageProperties", [])
        is_dismissed = False
        for prop in triage_props:
            if prop.get("key") == "is-dismissed" and prop.get("value") is True:
                is_dismissed = True
                break
        if is_dismissed:
            continue

        # Skip informational severity and extract needed properties
        occurrence_props = issue.get("occurrenceProperties", [])
        is_informational = False
        severity = None
        cwe = None
        overall_score = None
        for prop in occurrence_props:
            if prop.get("key") == "severity":
                severity = str(prop.get("value", ""))
                if severity.lower() == "informational":
                    is_informational = True
            elif prop.get("key") == "cwe":
                cwe = prop.get("value")
            elif prop.get("key") == "overall-score":
                overall_score = prop.get("value")
        if is_informational:
            continue

        # Use issue ID as rule id, but include CWE in rule name if present
        rule_id = str(issue.get("id", "PolarisIssueID"))[:255]
        issue_type = issue.get("type", {})
        base_rule_name = issue_type.get("altName", "Polaris Issue")
        rule_name = f"{base_rule_name} ({cwe})" if cwe else base_rule_name
        description = None
        localized = issue_type.get("_localized", {})
        if isinstance(localized, dict):
            other_details = localized.get("otherDetails", [])
            if isinstance(other_details, list):
                for detail in other_details:
                    if detail.get("key") == "description":
                        description = detail.get("value")
                        break

        # Use a human-readable message
        message = issue.get("message")
        if not message:
            message = rule_name

        location = issue.get("location", {})
        file_path = location.get("filePath", "POLARIS")
        line = location.get("line", 1)
        logical_name = issue.get("function", None) or issue.get("logicalLocation", None)

        # Add artifact if not already present
        if file_path not in artifact_map:
            artifact_index = len(artifacts)
            artifact_map[file_path] = artifact_index
            artifacts.append({
                "location": {
                    "uri": file_path,
                    "uriBaseId": "SRCROOT"
                },
                "sourceLanguage": "python"
            })
        else:
            artifact_index = artifact_map[file_path]

        # Add rule if not already present
        if rule_id not in rule_id_map:
            rule_index = len(rules)
            rule_id_map[rule_id] = rule_index
            
            # Build direct link to the specific issue in Polaris
            issue_url = f"https://eu.polaris.blackduck.com/portfolio/portfolios/{portfolio_id}/portfolio-items/{application_id}/projects/{project_id}/issues/{rule_id}?filter=triage%3Astatus%3Dnot-dismissed%2Cto-be-fixed"
            
            rule_entry = {
                "id": rule_id,
                "name": rule_name,
                "shortDescription": {
                    "text": rule_name
                },
                "fullDescription": {
                    "text": (description if description else rule_name)
                },
                "helpUri": issue_url,
                "help": {
                    "text": "Detailed explanation of the issue.",
                    "markdown": f"[View issue details in Polaris]({issue_url}) \n {(description if description else rule_name)}"
                }
            }
            if overall_score is not None:
                rule_entry["properties"] = {"security-severity": str(overall_score)}
            rules.append(rule_entry)
        else:
            rule_index = rule_id_map[rule_id]

       

        result = {
            "ruleId": rule_id,
            "ruleIndex": rule_index,
            "message": {
                "text": message
            },
            "locations": [{
                "physicalLocation": {
                    "artifactLocation": {
                        "uri": file_path,
                        "uriBaseId": "SRCROOT",
                        "index": artifact_index
                    },
                    "region": {
                        "startLine": line
                    }
                }
            }],
        
        }
        if logical_name:
            result["locations"][0]["logicalLocations"] = [{"fullyQualifiedName": logical_name}]
        results.append(result)
    with open(sarif_path, "w") as f:
        json.dump(sarif, f, indent=2)
    print("SARIF file written to polaris_issues.sarif")

if __name__ == "__main__":
    main()
