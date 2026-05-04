@description('Location for all resources')
param location string = resourceGroup().location

@description('Container App name')
param containerAppName string = 'hermesnotifier-api'

@description('Container App Environment name')
param environmentName string = 'hermesnotifier-env'

@description('Container image')
param containerImage string = 'ghcr.io/tony0935929660/hermesnotifier.api:latest'

@description('LINE Channel ID')
param lineChannelId string

@secure()
@description('LINE Channel Secret')
param lineChannelSecret string

@secure()
@description('LINE Bot Access Token')
param lineBotToken string

@secure()
@description('Database Connection String')
param connectionString string

@description('LINE Callback URL')
param lineCallbackUrl string

// Container Apps Environment
resource environment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: environmentName
  location: location
  properties: {
	appLogsConfiguration: {
	  destination: 'log-analytics'
	}
  }
}

// Container App
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  properties: {
	managedEnvironmentId: environment.id
	configuration: {
	  ingress: {
		external: true
		targetPort: 8080
		transport: 'http'
		allowInsecure: false
	  }
	  secrets: [
		{
		  name: 'line-channel-secret'
		  value: lineChannelSecret
		}
		{
		  name: 'line-bot-token'
		  value: lineBotToken
		}
		{
		  name: 'connection-string'
		  value: connectionString
		}
	  ]
	}
	template: {
	  containers: [
		{
		  name: 'hermesnotifier-api'
		  image: containerImage
		  resources: {
			cpu: json('0.5')
			memory: '1Gi'
		  }
		  env: [
			{
			  name: 'ASPNETCORE_ENVIRONMENT'
			  value: 'Production'
			}
			{
			  name: 'ASPNETCORE_URLS'
			  value: 'http://+:8080'
			}
			{
			  name: 'Line__ChannelId'
			  value: lineChannelId
			}
			{
			  name: 'Line__ChannelSecret'
			  secretRef: 'line-channel-secret'
			}
			{
			  name: 'Line__CallbackUrl'
			  value: lineCallbackUrl
			}
			{
			  name: 'LINE_BOT_CHANNEL_ACCESS_TOKEN'
			  secretRef: 'line-bot-token'
			}
			{
			  name: 'ConnectionStrings__DefaultConnection'
			  secretRef: 'connection-string'
			}
		  ]
		}
	  ]
	  scale: {
		minReplicas: 1
		maxReplicas: 3
		rules: [
		  {
			name: 'http-scaling'
			http: {
			  metadata: {
				concurrentRequests: '10'
			  }
			}
		  }
		]
	  }
	}
  }
}

output containerAppFQDN string = containerApp.properties.configuration.ingress.fqdn
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
