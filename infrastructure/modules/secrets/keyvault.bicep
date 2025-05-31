param location string
param keyVaultName string

resource keyVault 'Microsoft.KeyVault/vaults@2024-04-01-preview' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      name: 'standard'
      family: 'A'
    }
    enableRbacAuthorization: true
    tenantId: subscription().tenantId
  }
}

output name string = keyVault.name
output id string = keyVault.id
