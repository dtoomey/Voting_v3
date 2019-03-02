###################################################################################################
##  REF: https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-automate-powershell
###################################################################################################

Import-Module "$ENV:ProgramFiles\Microsoft SDKs\Service Fabric\Tools\PSModule\ServiceFabricSDK\ServiceFabricSDK.psm1"

###################################################################################################
$imageStorePath = 'Voting'
$appTypeName = 'VotingType'
$appName = "fabric:/$imageStorePath"
$appVersion = '1.0.0'
#$ServerCommonName = "CLUSTER_NAME.REGION.cloudapp.azure.com"   #for azure hosted cluster deployments
$ServerCommonName = "localhost"								   #for local hosted cluster deployments
$clusterAddress = "${ServerCommonName}:19000"
$thumb="YOUR_CERTIFICATE_THUMBPRINT"   #for secured cluster hosted in azure
###################################################################################################

try {

    # Connect to the Service Fabric cluster  (need certificate for a secured cluster in Azure)
    if($ServerCommonName -eq "localhost")
    {
	    Connect-ServiceFabricCluster $clusterAddress
    }
    else
    {
	    Connect-ServiceFabricCluster -ConnectionEndpoint $clusterAddress -X509Credential -ServerCertThumbprint $thumb -FindType FindByThumbprint -FindValue $thumb -StoreLocation CurrentUser -StoreName My
    }
 
    Write-Output Removing application '${imageStorePath}' from cluster at '${clusterAddress}'...

    # Remove an application instance
    Remove-ServiceFabricApplication $appName -Force  #force flag skips prompt for confirmation

    # Unregister the application type
    Unregister-ServiceFabricApplicationType $appTypeName $appVersion

    # Remove the application package
    Remove-ServiceFabricApplicationPackage -ApplicationPackagePathInImageStore Voting

}
catch
{
    Write-Output "An error occurred: " $Error[0]
}