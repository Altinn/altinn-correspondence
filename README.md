# altinn-correspondence




# Entity Framework 
Correspondence uses Entity Framework. 
When run locally, it applies migrations automaticly on startup 

## Adding migrations: 
To add a new migration you can run the following command: 

```
dotnet ef migrations add "MigrationName" --project ./src/Altinn.Correspondence.Persistence --startup-project ./src/Altinn.Correspondence.API
```
Database migrations is applied automaticly on startup when doing local development, but to update the database manually, run: 
```
dotnet ef database update
``` 
