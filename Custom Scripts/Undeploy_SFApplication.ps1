###################################################################################################
##  REF: https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-automate-powershell
###################################################################################################

param (
    [string] $imageStorePath = "Voting",
    [string] $appTypeName = "VotingType",
    [string] $appName = "fabric:/$imageStorePath",
    [string] $appVersion = "1.0.0",
    [string] $ServerCommonName = "localhost" ,    # for local hosted cluster deployments or “CLUSTER_NAME.REGION.cloudapp.azure.com” for azure
    [string] $thumb="YOUR_CERTIFICATE_THUMBPRINT"   # for secured cluster hosted in azure
)

try {

    Import-Module "$ENV:ProgramFiles\Microsoft SDKs\Service Fabric\Tools\PSModule\ServiceFabricSDK\ServiceFabricSDK.psm1"

    # Connect to the Service Fabric cluster  (need certificate for a secured cluster in Azure)
    if($ServerCommonName -eq "localhost")
    {
	    Connect-ServiceFabricCluster $clusterAddress
    }
    else
    {
	    Connect-ServiceFabricCluster -ConnectionEndpoint $clusterAddress -X509Credential -ServerCertThumbprint $thumb -FindType FindByThumbprint -FindValue $thumb -StoreLocation CurrentUser -StoreName My
    }
 
    Write-Host "Removing application '$imageStorePath' from cluster at '$clusterAddress'..."

    # Remove an application instance
    Remove-ServiceFabricApplication -ApplicationName $appName -Force  #force flag skips prompt for confirmation

    # Unregister the application type
    Unregister-ServiceFabricApplicationType -ApplicationTypeName $appTypeName -ApplicationTypeVersion $appVersion -Force

    # Remove the application package
    Remove-ServiceFabricApplicationPackage -ApplicationPackagePathInImageStore Voting

}
catch
{
    Write-Error "An error occurred: $($_.Exception.Message)"
}