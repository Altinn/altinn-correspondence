# altinn-correspondence

## Postman
<a id="postman"></a>

Example requests using postman can be found in [altinn-correspondence-v1.json](/altinn-correspondence-v1.json). 


## Entity Framework 
Correspondence uses Entity Framework. 
When run locally, it applies migrations automaticly on startup 

## Local Development

The start.ps1 script runs all neccassary commands to run the project. 

Installing Dotnet 8.0 is a pre-requisite.

## Adding migrations: 
To add a new migration you can run the following command: 

```
dotnet ef migrations add "MigrationName" --project ./src/Altinn.Correspondence.Persistence --startup-project ./src/Altinn.Correspondence.API
```
Database migrations is applied automaticly on startup when doing local development, but to update the database manually, run: 
```
dotnet ef database update
``` 
