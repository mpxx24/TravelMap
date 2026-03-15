using './main.bicep'

// TravelMap App Service name — globally unique
param appName = 'mytravelmap'

// Azure region
param location = 'westeurope'

// Shared resources from ActivitiesJournal (same resource group)
param appServicePlanName = 'plan-myactivitiesjournal'

// These values need to be filled in from the existing deployment.
// Run: az deployment group show -g rg-activities-journal -n main --query properties.outputs -o json
// to get the actual names (they include uniqueString hashes).
param keyVaultName = '' // e.g. 'kvxxxxxxxxxxxxxxxxx'
param storageAccountName = '' // e.g. 'stxxxxxxxxxxxxxxxxx'
param appInsightsName = 'appi-myactivitiesjournal'
