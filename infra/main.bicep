// ============================================================
// TravelMap - Azure Infrastructure
// Creates a new App Service on the EXISTING B1 plan from
// ActivitiesJournal, reuses existing KV, Storage, App Insights.
// All resources live in the same resource group.
// ============================================================

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Name for the TravelMap App Service (must be globally unique)')
param appName string

@description('Name of the existing App Service Plan (from ActivitiesJournal)')
param appServicePlanName string

@description('Name of the existing Key Vault')
param keyVaultName string

@description('Name of the existing Storage Account')
param storageAccountName string

@description('Name of the existing Application Insights')
param appInsightsName string

@description('Name of the existing Log Analytics Workspace (from ActivitiesJournal)')
param logAnalyticsWorkspaceName string

@description('Object ID of the deployer — gets Key Vault Secrets Officer role')
param deployerObjectId string = ''

// ------------------------------------------------------------
// Reference existing shared resources (same resource group)
// ------------------------------------------------------------
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' existing = {
  name: appServicePlanName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' existing = {
  name: keyVaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: logAnalyticsWorkspaceName
}

// ------------------------------------------------------------
// App Service — new site on existing B1 plan
// ------------------------------------------------------------
resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: appName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'Google__ClientId'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=Google--ClientId)'
        }
        {
          name: 'Google__ClientSecret'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=Google--ClientSecret)'
        }
        {
          name: 'Storage__BlobEndpoint'
          value: storageAccount.properties.primaryEndpoints.blob
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
      ]
    }
  }
}

// ------------------------------------------------------------
// Role assignments on shared resources for TravelMap's identity
// ------------------------------------------------------------

// Storage Blob Data Contributor
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, appService.id, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets User
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appService.id, kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets Officer for deployer (skipped if no deployerObjectId)
var kvSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource kvDeployerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerObjectId)) {
  name: guid(keyVault.id, deployerObjectId, kvSecretsOfficerRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsOfficerRoleId)
    principalId: deployerObjectId
    principalType: 'User'
  }
}

// ------------------------------------------------------------
// Diagnostic Settings — App Service → Log Analytics
// ------------------------------------------------------------
resource appServiceDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${appName}'
  scope: appService
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      { category: 'AppServiceHTTPLogs',    enabled: true }
      { category: 'AppServiceConsoleLogs', enabled: true }
      { category: 'AppServiceAppLogs',     enabled: true }
      { category: 'AppServiceAuditLogs',   enabled: true }
    ]
  }
}

// ------------------------------------------------------------
// Outputs
// ------------------------------------------------------------
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output appServiceName string = appService.name
