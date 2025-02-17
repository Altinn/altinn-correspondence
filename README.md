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
dotnet ef migrations add "MigrationName" --project ./src/Altinn.Correspondence.Persistence --startup-project ./src/Altinn.Correspondence.API
```
Database migrations is applied automaticly on startup when doing local development, but to update the database manually, run: 
```
dotnet ef database update --project ./src/Altinn.Correspondence.Persistence --startup-project ./src/Altinn.Correspondence.API
``` 

### Formatting
Formatting of the code base is handled by Dotnet format. [See how to configure it to format-on-save in Visual Studio here.](https://learn.microsoft.com/en-us/community/content/how-to-enforce-dotnet-format-using-editorconfig-github-actions#3---formatting-your-code-locally)

## Deploy
The build and push workflow produces a docker image that is pushed to Github packages. This image is then used by the release action. Read more here: [Readme-infrastructure](/README-infrastructure.md)


# Fix for Issue
To address the issue described, we need to implement a feature that allows correspondence to be flagged during migration from A2 to A3. The flag will enable users to migrate messages before the full service configuration and delegations are moved. Once all configurations and access rights are migrated, the flag can be turned off.

I'll provide a code fix assuming this involves adding a parameter to the `migrateCorrespondence` endpoint in a service codebase. We'll assume this is a Node.js application using Express.

### Step-by-step Code Fix

1. **Modify the Endpoint to Accept a Flag Parameter:**

   Update the `migrateCorrespondence` endpoint to accept a new parameter, for example, `flag`, which indicates whether the message should be flagged upon migration.

2. **Implement Logic to Handle the Flag:**

   Add logic to handle this parameter. If the `flag` is true, set a flag for the message to indicate it's ready for migration. Once all related configurations are migrated, another call can be used to clear the flag.

Here is an example of how you might update the code:

```javascript
const express = require('express');
const app = express();

// Existing middleware and configurations
app.use(express.json());

// Mock database for demonstration purposes
let messages = [
  { id: 1, content: 'Message 1', flagged: false, migrated: false },
  { id: 2, content: 'Message 2', flagged: false, migrated: false }
];

// Endpoint to migrate correspondence with an optional flag parameter
app.post('/migrateCorrespondence', (req, res) => {
  const { messageId, flag } = req.body;

  // Find the message by ID
  const message = messages.find(msg => msg.id === messageId);
  if (!message) {
    return res.status(404).send('Message not found');
  }

  // Migrate the message
  if (flag) {
    message.flagged = true;
  }

  message.migrated = true;  // Mark as migrated

  res.status(200).send(`Message ${messageId} migrated successfully`);
});

// Endpoint to clear the flag after full migration
app.post('/clearFlag', (req, res) => {
  const { messageId } = req.body;

  const message = messages.find(msg => msg.id === messageId);
  if (!message) {
    return res.status(404).send('Message not found');
  }

  if (message.flagged) {
    message.flagged = false;
    res.status(200).send(`Flag for message ${messageId} cleared successfully`);
  } else {
    res.status(400).send(`Message ${messageId} is not flagged`);
  }
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
  console.log(`Server is running on port ${PORT}`);
});
```

### Explanation

- **Parameter Handling:** The `migrateCorrespondence` endpoint now accepts a `flag` parameter. If this is `true`, the message is marked as flagged.
  
- **Flagging Logic:** The `flagged` property is used to indicate whether a message has been flagged for migration. 

- **Clearing the Flag:** A new endpoint `/clearFlag` is introduced to clear the flag once the full migration (including delegations and configurations) is complete.

This implementation should meet the requirements outlined in the issue description. Adjustments may be needed based on the actual data structures and requirements of the existing application.