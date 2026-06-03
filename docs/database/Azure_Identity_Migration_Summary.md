# Azure.Identity SDK Migration - Summary

## Date: 2024-06-02
## Change: Replaced Azure CLI process spawning with Azure.Identity SDK

---

## ✅ Changes Made

### 1. Updated Package References
**File:** `tools/Altinn.Correspondence.DialogActivityExporter/Altinn.Correspondence.DialogActivityExporter.csproj`

**Added:**
```xml
<PackageReference Include="Azure.Identity" Version="1.13.1" />
```

### 2. Refactored TryBuildAzureConnectionAsync Method
**File:** `tools/Altinn.Correspondence.DialogActivityExporter/Program.cs`

**Before:** Spawned `az` CLI process to get access token
**After:** Uses `DefaultAzureCredential` from Azure.Identity SDK

**Benefits:**
- ✅ No longer requires Azure CLI to be installed
- ✅ Supports multiple authentication methods automatically
- ✅ Better error handling with `AuthenticationFailedException`
- ✅ More secure (no shell process spawning)
- ✅ Works in Visual Studio and VS Code
- ✅ Supports Managed Identity for Azure workloads

---

## 🔐 Supported Authentication Methods

`DefaultAzureCredential` automatically tries these methods in order:

1. **EnvironmentCredential** - Service principal via environment variables
2. **WorkloadIdentityCredential** - Kubernetes workload identity
3. **ManagedIdentityCredential** - Azure VM, App Service, Container Apps
4. **SharedTokenCacheCredential** - Cached credentials
5. **VisualStudioCredential** - Visual Studio signed-in user
6. **VisualStudioCodeCredential** - VS Code Azure Account extension
7. **AzureCliCredential** - Azure CLI (`az login`)
8. **AzurePowerShellCredential** - Azure PowerShell (`Connect-AzAccount`)
9. **AzureDeveloperCliCredential** - Azure Developer CLI (`azd`)
10. ~~InteractiveBrowserCredential~~ - Disabled (not suitable for CLI tools)

---

## 📝 Code Changes

### Before (Process Spawning):
```csharp
var tokenProcess = new System.Diagnostics.Process
{
    StartInfo = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "az",
        Arguments = "account get-access-token --resource-type oss-rdbms --query accessToken -o tsv",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    }
};

tokenProcess.Start();
var token = await tokenProcess.StandardOutput.ReadToEndAsync();
var error = await tokenProcess.StandardError.ReadToEndAsync();
await tokenProcess.WaitForExitAsync();
```

### After (Azure.Identity SDK):
```csharp
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    ExcludeInteractiveBrowserCredential = true // Don't popup browser in CLI tool
});

var tokenRequestContext = new TokenRequestContext(
    scopes: new[] { "https://ossrdbms-aad.database.windows.net/.default" }
);

var tokenResult = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
var token = tokenResult.Token;
```

---

## 🔍 Error Handling Improvements

### Before:
- Generic `Exception` catch
- Error message from stderr could be cryptic
- Required Azure CLI to be in PATH

### After:
- Specific `AuthenticationFailedException` catch
- Clear error messages about authentication methods
- Works without Azure CLI if other credentials available

**Example Error Message:**
```
Azure authentication failed. Make sure you're logged in via Azure CLI (az login), 
Visual Studio, or VS Code.
```

---

## 📖 Documentation Updates

### Files Updated:
1. **Testing_Guide.md**
   - Added Azure.Identity authentication methods section
   - Updated troubleshooting with multiple auth options
   - Clarified Visual Studio and VS Code authentication

2. **Quick_Test_Reference.md**
   - Updated troubleshooting section
   - Added Visual Studio and VS Code options

3. **test-export.ps1**
   - Updated help comments to mention multiple auth methods
   - Clarified Azure AD parameter description

4. **Program.cs**
   - Updated help text: "requires Azure CLI" → "Azure CLI, Visual Studio, VS Code, or other Azure credentials"
   - Updated comment: "Azure CLI" → "Azure Identity"

---

## 🎯 User Impact

### Developers Now Have More Options:

**Before:** Had to install Azure CLI and run `az login`

**After:** Can authenticate using:
- Azure CLI (`az login`) - still works!
- Visual Studio (Tools → Options → Azure Service Authentication)
- VS Code (Azure Account extension)
- Environment variables (for automation)
- Managed Identity (when running in Azure)

### Example Scenarios:

**Scenario 1: Developer using Visual Studio**
```
1. Tools → Options → Azure Service Authentication → Sign in
2. Run: .\test-export.ps1
3. ✅ Works! No Azure CLI needed
```

**Scenario 2: Developer using VS Code**
```
1. Install Azure Account extension
2. Ctrl+Shift+P → "Azure: Sign In"
3. Run: .\test-export.ps1
4. ✅ Works! No Azure CLI needed
```

**Scenario 3: CI/CD Pipeline**
```
1. Set environment variables for service principal
2. Run: dotnet run -- --issue all --azure-ad ...
3. ✅ Works! Uses EnvironmentCredential
```

**Scenario 4: Azure VM/App Service**
```
1. Enable Managed Identity on Azure resource
2. Run: dotnet run -- --issue all --azure-ad ...
3. ✅ Works! Uses ManagedIdentityCredential
```

---

## ✅ Benefits Summary

| Aspect | Before | After |
|--------|--------|-------|
| **Dependency** | Requires Azure CLI | Works with multiple auth methods |
| **Security** | Shell process spawning | Native SDK (no shell) |
| **Error Messages** | Generic | Specific to auth type |
| **Developer Experience** | Must install `az` CLI | Use existing IDE authentication |
| **CI/CD** | Requires Azure CLI in pipeline | Supports service principals natively |
| **Azure Workloads** | Manual token management | Automatic Managed Identity |

---

## 🔧 Testing

**Build Status:** ✅ Successful

**Backward Compatibility:** ✅ Yes
- Users with Azure CLI can still use it (AzureCliCredential is one of the methods)
- No breaking changes to command-line interface
- Existing appsettings.json and connection string options still work

---

## 📚 Related Documentation

- **Azure.Identity Package:** https://www.nuget.org/packages/Azure.Identity
- **DefaultAzureCredential Docs:** https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential
- **Testing_Guide.md:** Complete testing documentation with all auth methods
- **Quick_Test_Reference.md:** Quick reference card for common scenarios

---

## 🚀 Migration Complete

The DialogActivityExporter now uses modern Azure.Identity SDK for authentication, providing a better developer experience and supporting more authentication scenarios without requiring Azure CLI installation.
