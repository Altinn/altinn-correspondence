# Altinn Correspondence - Meldingstjenesten

[![CI/CD](https://github.com/Altinn/altinn-correspondence/actions/workflows/ci-cd.yaml/badge.svg)](https://github.com/Altinn/altinn-correspondence/actions/workflows/ci-cd.yaml)

## Local Development
The start.ps1 script runs all neccassary commands to run the project. If you want to run the commands seperate, you can follow the steps below: 

The services required to support local development are run using docker compose:
```docker compose up -d```

To support features like hot reload etc, the app itself is run directly. Either in IDE like Visual Studio or by running:
```dotnet watch --project ./src/Altinn.Correspondence.API/Altinn.Correspondence.API.csproj```

### Adding migrations: 
To add a new migration you can run the following command: 

```
dotnet ef migrations add "MigIgnoreReservation" --project ./src/Altinn.Correspondence.Persistence --startup-project ./src/Altinn.Correspondence.API
```
Database migrations is applied automaticly on startup when doing local development, but to update the database manually, run: 
```
dotnet ef database update --project ./src/Altinn.Correspondence.Persistence --startup-project ./src/Altinn.Correspondence.API
``` 

### Formatting
Formatting of the code base is handled by Dotnet format. [See how to configure it to format-on-save in Visual Studio here.](https://learn.microsoft.com/en-us/community/content/how-to-enforce-dotnet-format-using-editorconfig-github-actions#3---formatting-your-code-locally)

## Deploy
The build and push workflow produces a docker image that is pushed to Github packages. This image is then used by the release action. Read more here: [Readme-infrastructure](/README-infrastructure.md)
