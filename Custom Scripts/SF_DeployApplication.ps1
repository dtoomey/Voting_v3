###################################################################################################
##  REF: https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-automate-powershell
###################################################################################################

Import-Module "$ENV:ProgramFiles\Microsoft SDKs\Service Fabric\Tools\PSModule\ServiceFabricSDK\ServiceFabricSDK.psm1"

###################################################################################################
$path = "C:\Repos\Demos\Voting_v3\Voting\pkg\Debug" #Set this to the path for your solution output directory
$imageStorePath = "Voting"
$appTypeName = "VotingType"
$appName = "fabric:/$imageStorePath"
$appVersion = "1.0.0"
#$ServerCommonName = "CLUSTER_NAME.REGION.cloudapp.azure.com"   #for azure hosted cluster deployments
$ServerCommonName = "localhost"                               #for local hosted cluster deployments
$clusterAddress = "${ServerCommonName}:19000"
$thumb="YOUR_CERTIFICATE_THUMBPRINT"   #for secured cluster hosted in azure
###################################################################################################

Write-Output "Deploying application '${imageStorePath}' to cluster at '${clusterAddress}'..."

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

    $imageStoreConnStr = Get-ImageStoreConnectionStringFromClusterManifest(Get-ServiceFabricClusterManifest)

    # Upload package to package store
    Copy-ServiceFabricApplicationPackage -ApplicationPackagePath $path -ApplicationPackagePathInImageStore $imageStorePath -TimeoutSec 1800

    # Register the application package
    Register-ServiceFabricApplicationType -ApplicationPathInImageStore $imageStorePath

    # Get all application types registered in the cluster
    #Get-ServiceFabricApplicationType

    # Create the application 
    New-ServiceFabricApplication $appName $appTypeName $appVersion

    # Get all application instances created in the cluster
    Get-ServiceFabricApplication -ApplicationName $appName

    # Get all service instances for each application
    Get-ServiceFabricApplication -ApplicationName $appName | Get-ServiceFabricService
}
catch
{
    Write-Output "An error occurred: " $Error[0]
}