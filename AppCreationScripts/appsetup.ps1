Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force
$secpasswd = ConvertTo-SecureString "Abeyd@123" -AsPlainText -Force
$mycreds = New-Object System.Management.Automation.PSCredential ("alex@maipandeppe.ccsctp.net", $secpasswd)
$tenantId = "d649ffe5-bd04-4d8a-900b-0ceedafacd5b"

.\Cleanup.ps1 -Credential $mycreds -TenantId $tenantId -AzureEnvironmentName "AzurePPE"
.\Configure.ps1 -Credential $mycreds -TenantId $tenantId -AzureEnvironmentName "AzurePPE"