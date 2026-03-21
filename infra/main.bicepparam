using './main.bicep'

// All parameter values are passed via the deploy script (deploy-travelmap.sh)
// so that no real resource names or environment-specific values are committed to git.
//
// Required params:
//   appName             — App Service name (becomes <name>.azurewebsites.net)
//   appServicePlanName  — Existing App Service Plan (from ActivitiesJournal)
//   keyVaultName        — Existing Key Vault (from ActivitiesJournal)
//   storageAccountName  — Existing production Storage Account (from ActivitiesJournal)
//   appInsightsName     — Existing Application Insights (from ActivitiesJournal)
//
// Optional params (have defaults in main.bicep):
//   location            — Azure region (default: resource group location)
//   deployerObjectId    — passed by script at deploy time
