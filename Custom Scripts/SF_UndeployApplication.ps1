###################################################################################################
##  REF httpsdocs.microsoft.comen-usazureservice-fabricservice-fabric-automate-powershell
###################################################################################################

Import-Module $ENVProgramFilesMicrosoft SDKsService FabricToolsPSModuleServiceFabricSDKServiceFabricSDK.psm1

$imageStorePath = 'Voting'
$appTypeName = 'VotingType'
$appName = fabric$imageStorePath
$appVersion = '1.0.0'
$ServerCommonName = "CLUSTER_NAME.REGION.cloudapp.azure.com"   #for azure hosted cluster deployments
#$ServerCommonName = localhost                               #for local hosted cluster deployments
$clusterAddress = ${ServerCommonName}19000
$thumb="YOUR_CERTIFICATE_THUMBPRINT"   #for secured cluster hosted in azure

# Connect to the Service Fabric cluster  (need certificate for a secured cluster in Azure)
#Connect-ServiceFabricCluster $clusterAddress  #for local cluster only
Connect-ServiceFabricCluster -ConnectionEndpoint $clusterAddress -X509Credential -ServerCertThumbprint $thumb -FindType FindByThumbprint -FindValue $thumb -StoreLocation CurrentUser -StoreName My
 
Write-Output Removing application '${imageStorePath}' from cluster at '${clusterAddress}'...

# Remove an application instance
Remove-ServiceFabricApplication $appName -Force  #force flag skips prompt for confirmation

# Unregister the application type
Unregister-ServiceFabricApplicationType $appTypeName $appVersion

# Remove the application package
Remove-ServiceFabricApplicationPackage -ApplicationPackagePathInImageStore Voting