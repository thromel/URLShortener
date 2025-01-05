param name string = 'api'
param location string = resourceGroup().location

var uniqueId = uniqueString(resourceGroup().id)

module apiService 'modules/compute/appservice.bicep' = {
  name: 'apiDeployment'
  params: {
    appservicePlanName: '${name}-plan-${uniqueId}'
    webAppName: '${name}-web-${uniqueId}'
    location: location
  }
}
