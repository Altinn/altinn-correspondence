## Correspondence Performance Test

This performance test directory focuses on evaluating the GET and POST endpoints of the `correspondence` API. The test files associated with this performance test are `create-correspondence.js`, `create-and-upload-corespondence.js`, `get-correspondence.js`, and `get-correspondence-overview.js`. These files are designed to measure the performance and scalability of the API endpoints under different scenarios. By running these tests, you can gain insights into the system's response time, throughput, and resource utilization. Use the instructions below to execute the performance test and analyze the results.

### Prerequisites

* Either
  * [Grafana K6](https://k6.io/) must be installed and `k6` available in `PATH` 
  * or Docker (available av `docker` in `PATH`)
* Powershell or Bash (should work on any platform supported by K6)

### Test Files
The test files associated with this performance test are 
- `create-correspondence.js`
- `create-and-upload-correspondence.js`
- `get-correspondence.js`
- `get-correspondence-overview.js`

## Test description
### Create correspndence
For each iteration
1. Get service and serviceowner and random enduser
2. Post correspondence json 

### Create and upload correspndence
For each iteration
1. Get service and serviceowner and random enduser
2. Post correspondence form-data with content

### Get correspondence
For each iteration:
1. get a random enduser with token
2. get correspondences for the enduser
3. Get the details of the first correspondence in the list from 2. 
4. Get the reference to dialog in dialogporten from 3.
5. Ask dialogporten for dialogdetails, get token from response
6. Use token from 5. when asking for /content for each correspondence from 2

### Get correspondence overview
For each iteration:
1. get a random enduser with token
2. get correspondences for the enduser
3. Get the details of every correspondence in the list from 2.



### Run Test
To run the performance test, follow the instructions below:

#### From CLI
1. Navigate to the following directory:
```shell
cd Test/Altinn.Correspondence.LoadTests/correspondence
```
2. Run the test using the following command. Replace `<test-file>`, `<(test|staging|yt01)>`, `<vus>`, and `<duration>` with the desired values:
```shell
TOKEN_GENERATOR_USERNAME=<username> TOKEN_GENERATOR_PASSWORD=<passwd> \
k6 run <test-file> -e API_VERSION=v1 \
-e API_ENVIRONMENT=<(test|staging|yt01)> \
--vus=<vus> --duration=<duration>
```
3. Refer to the k6 documentation for more information on usage.

#### From GitHub Actions
To run the performance test using GitHub Actions, follow these steps:
1. Go to the [GitHub Actions](https://github.com/Altinn/altinn-correspondence/actions/workflows/test-performance.yml) page.
2. Select "Run workflow" and fill in the required parameters.
3. Tag the performance test with a descriptive name.

#### GitHub Action with act
To run the performance test locally using GitHub Actions and act, perform the following steps:
1. [Install act](https://nektosact.com/installation/).
2. Navigate to the root of the repository.
3. Create a `.secrets` file that matches the GitHub secrets used. Example:
```file
TOKEN_GENERATOR_USERNAME:<username>
TOKEN_GENERATOR_PASSWORD:<passwd>
```
    Replace `<username>` and `<passwd>`, same as for generating tokens above.
##### IMPORTANT: Ensure this file is added to .gitignore to prevent accidental commits of sensitive information. Never commit actual credentials to version control.
4. Run `act` using the command below. Replace `<path-to-testscript>`, `<vus>`, `<duration>` and `<(personal|enterprise|both)>` with the desired values:
```shell
act workflow_dispatch -j k6-performance -s GITHUB_TOKEN=`gh auth token` \
--container-architecture linux/amd64 --artifact-server-path $HOME/.act \ 
--input vus=<vus> --input duration=<duration> \ 
--input testSuitePath=<path-to-testscript> 
```

Example of command:
```shell
act workflow_dispatch -j k6-performance -s GITHUB_TOKEN=`gh auth token` \
--container-architecture linux/amd64 --artifact-server-path $HOME/.act \ 
--input vus=10 --input duration=5m \ 
--input testSuitePath=Test/Altinn.Correspondence.LoadTests/correspondence/get-correspondence.js
```

### Test Results
The test results can be found in the [GitHub Actions](https://github.com/Altinn/altinn-correspondence/actions/workflows/test-performance.yml) run log, grafana and in App Insights.
