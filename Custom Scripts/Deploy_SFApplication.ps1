###################################################################################################
##  REF: https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-automate-powershell
###################################################################################################

param (
    [Parameter(Mandatory=$true, HelpMessage="Enter the path to the folder containing the package to be deployed.")]
    [ValidateScript({Test-Path -Path $_ -PathType Container})]
    [string] $path = "C:\Repos\Demos\Voting_v3\Voting\pkg\Debug",     #Set this to the path for your solution output directory
    [string] $imageStorePath = "Voting",
    [string] $appTypeName = "VotingType",
    [string] $appName = "fabric:/$imageStorePath",
    [string] $appVersion = "1.0.0",
    [string] $ServerCommonName = "localhost" ,    # for local hosted cluster deployments or “CLUSTER_NAME.REGION.cloudapp.azure.com” for azure
    [string] $thumb="YOUR_CERTIFICATE_THUMBPRINT"   # for secured cluster hosted in azure
)

Write-Host "Deploying application '$imageStorePath' to cluster at '$clusterAddress'..."

try {

    Import-Module "$ENV:ProgramFiles\Microsoft SDKs\Service Fabric\Tools\PSModule\ServiceFabricSDK\ServiceFabricSDK.psm1"

    # Connect to the Service Fabric cluster  (need certificate for a secured cluster in Azure)
    if($ServerCommonName -eq "localhost")
    {
	    Connect-ServiceFabricCluster -ConnectionEndpoint $clusterAddress
    }
    else
    {
	    Connect-ServiceFabricCluster -ConnectionEndpoint $clusterAddress -X509Credential -ServerCertThumbprint $thumb -FindType FindByThumbprint -FindValue $thumb -StoreLocation CurrentUser -StoreName My
    }

    $imageStoreConnStr = Get-ImageStoreConnectionStringFromClusterManifest -ClusterManifest (Get-ServiceFabricClusterManifest)

    # Upload package to package store
    Copy-ServiceFabricApplicationPackage -ApplicationPackagePath $path -ApplicationPackagePathInImageStore $imageStorePath -TimeoutSec 1800

    # Register the application package
    Register-ServiceFabricApplicationType -ApplicationPathInImageStore $imageStorePath

    # Create the application 
    New-ServiceFabricApplication -ApplicationName $appName -ApplicationTypeName $appTypeName -ApplicationTypeVersion $appVersion

    # Get all application instances created in the cluster
    Get-ServiceFabricApplication -ApplicationName $appName

    # Get all service instances for each application
    Get-ServiceFabricApplication -ApplicationName $appName | Get-ServiceFabricService
}
catch
{
    Write-Error "An error occurred: $($_.Exception.Message)"
}