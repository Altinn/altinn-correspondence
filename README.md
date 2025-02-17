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

# Fix for Issue
To address the issue described in the GitHub issue, we need to modify the `migrateCorrespondence` endpoint to include a new parameter that allows messages to be flagged as eligible for migration from A2 to A3, even before the service configurations and delegations on the message/service are migrated.

Here's a step-by-step guide on how to implement this:

1. **Understand the Requirement**: 
   - Add a new flag parameter to the `migrateCorrespondence` function that indicates whether a message can be migrated to A3 prior to the migration of service configurations and delegations.
   - Once all configurations and permissions are migrated, the flag can be turned off.

2. **Update the Function Signature**:
   - Modify the signature of the `migrateCorrespondence` function to accept this new parameter. Let's assume the new parameter is `canMigrateEarly`.

3. **Implement Logic to Handle the Parameter**:
   - Within the `migrateCorrespondence` function, implement logic to handle this parameter. The function should check this parameter to decide if the message can be made available immediately for use in A3.

4. **Apply Changes in Relevant Areas**:
   - Ensure that appropriate business and migration logic respects this flag. This might involve changes to how messages are registered or checked within the A3 system.
   - Ensure that any integration or dependent code also respects this new parameter.

Below is a hypothetical implementation of these changes in a code snippet where `migrateCorrespondence` might be located:

```python
def migrateCorrespondence(message_id, migrate_early=False):
    """
    Migrates a message from A2 to A3. The parameter migrate_early indicates whether
    the message should be made available immediately in A3, even if service configurations
    and delegations haven't been migrated yet.

    :param message_id: The ID of the message to be migrated.
    :param migrate_early: Boolean indicating if the message can be migrated early.
    """

    try:
        # Fetch the message from the A2 system
        message = fetch_message_from_a2(message_id)

        # Check if early migration is permitted and requested
        if migrate_early:
            # Perform necessary steps to register the message in A3 without waiting
            register_message_in_a3(message, early_migration=True)

        # Proceed with normal migration logic for service configurations and delegations
        migrate_service_configurations(message)
        migrate_delegations(message)

        # If early migration was used, ensure further steps finalizes the migration
        # If any errors occurred, handle them appropriately
        if migrate_early:
            finalize_migration_for_early_message(message)

        # Signal success or further actions needed
        print(f"Migration complete for message {message_id}. Early migration: {migrate_early}")

    except Exception as e:
        print(f"Error during migration of message {message_id}: {e}")
        # Add error handling logic as necessary

def fetch_message_from_a2(message_id):
    # Dummy function to simulate fetching a message from A2
    pass

def register_message_in_a3(message, early_migration=False):
    # Dummy function to register a message in A3, possibly in an incomplete state
    pass

def migrate_service_configurations(message):
    # Dummy function to simulate migrating service configurations
    pass

def migrate_delegations(message):
    # Dummy function to simulate migrating delegations
    pass

def finalize_migration_for_early_message(message):
    # Dummy function to finalize the migration once configurations and delegations are ready
    pass

# Example usage
migrateCorrespondence("some_message_id", migrate_early=True)
```

### Key Points:
- **Parameter Addition**: The `migrateCorrespondence` function now accepts a boolean parameter `migrate_early` that controls early migration.
- **Logic Adjustment**: Decisions within the function check this parameter and adjust the migration process accordingly.
- **Placeholder Functions**: These demonstrate where actual logic would be implemented, such as fetching messages, registering them early, migrating configurations, delegations, and finalizing the migration.
- **Error Handling**: Basic exception handling is shown here; in a real-world scenario, you would include more robust error handling and logging.

Make sure to adapt this pseudocode to fit the specific requirements and structure of the actual codebase, including database operations, existing migration logic, and any associated services.

# Fix for Issue
To address the issue described, we need to add functionality to the `migrateCorrespondence` endpoint to support flagging correspondence for migration from A2 to A3. This involves several steps:

1. **Understand the Requirement:**
   - The goal is to flag correspondence for migration before updating the service configuration and delegations.
   - Once all delegations and accesses are migrated, the flag should be turned off.
   - We need to add a parameter to the `migrateCorrespondence` endpoint to handle this flagging mechanism.

2. **Plan the Changes:**
   - Add a boolean parameter (e.g., `flagForMigration`) to `migrateCorrespondence` endpoint.
   - Modify the backend logic to accommodate the flag and ensure correspondence is marked correctly.
   - Update any related tests to cover the new functionality.
   - Ensure backward compatibility by making the new parameter optional.

3. **Implementation Steps:**

Here's a sample code snippet to illustrate how you can implement these changes. This assumes a REST API implemented using a common language like Java with Spring Boot, just as an example:

```java
@RestController
@RequestMapping("/api")
public class CorrespondenceController {

    @Autowired
    private CorrespondenceService correspondenceService;

    @PostMapping("/migrateCorrespondence")
    public ResponseEntity<?> migrateCorrespondence(@RequestBody MigrationRequest request) {
        boolean success = correspondenceService.migrateCorrespondence(
                request.getMessageId(), request.getFlagForMigration());

        if (success) {
            return ResponseEntity.ok("Migration processed successfully.");
        } else {
            return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR)
                    .body("Failed to process migration.");
        }
    }
}

@Service
public class CorrespondenceService {

    @Autowired
    private CorrespondenceRepository correspondenceRepository;

    public boolean migrateCorrespondence(String messageId, boolean flagForMigration) {
        Optional<Correspondence> correspondenceOpt = correspondenceRepository.findById(messageId);
        
        if (!correspondenceOpt.isPresent()) {
            return false;
        }

        Correspondence correspondence = correspondenceOpt.get();
        
        if (flagForMigration) {
            correspondence.setFlaggedForMigration(true);
        }

        // Logic to migrate the correspondence service attributes
        // Assuming migrateAttributes() is a function handling migrations
        if (migrateAttributes(correspondence)) {
            correspondenceRepository.save(correspondence);
            return true;
        }

        return false;
    }

    private boolean migrateAttributes(Correspondence correspondence) {
        // Migration logic goes here
        // ...
        return true;
    }
}

// Correspondence Entity
@Entity
public class Correspondence {

    @Id
    private String messageId;
    private boolean flaggedForMigration;

    // Getters and setters
    public String getMessageId() {
        return messageId;
    }

    public void setMessageId(String messageId) {
        this.messageId = messageId;
    }

    public boolean isFlaggedForMigration() {
        return flaggedForMigration;
    }

    public void setFlaggedForMigration(boolean flaggedForMigration) {
        this.flaggedForMigration = flaggedForMigration;
    }
}

// Correspondence Repository
public interface CorrespondenceRepository extends JpaRepository<Correspondence, String> {
    // Custom query methods if necessary
}
```

4. **Testing:**
   - Write unit tests to verify that messages flagged for migration are correctly processed and flagged.
   - Test the endpoint with and without the new parameter to ensure backward compatibility.
   - Consider edge cases, such as what happens if a message ID does not exist.

This example assumes a typical Spring Boot setup for a REST API. Depending on your specific environment (e.g., language, framework), the implementation may vary but will follow similar principles.
