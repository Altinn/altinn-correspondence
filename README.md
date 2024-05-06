# altinn-correspondence




# Entity Framework 
Correspondence uses Entity Framework. 
When run locally, it applies migrations automaticly on startup 

## Adding migrations manually: 
To add a new migration you can run the following command: 

```
dotnet ef migrations add "MigrationName" --project ./src/Altinn.Correspondence.Persistence --startup-project ./src/Altinn.Correspondence.API
```
To update the database, run: 
```
dotnet ef database update
``` 
