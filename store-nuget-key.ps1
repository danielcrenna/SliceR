# Script to securely store NuGet API key using PowerShell's Credential Manager
param(
    [Parameter(Mandatory=$false)]
    [string]$ApiKey
)

Write-Host "NuGet API Key Storage Tool" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $secureKey = Read-Host "Enter your NuGet API Key" -AsSecureString
} else {
    $secureKey = ConvertTo-SecureString $ApiKey -AsPlainText -Force
}

# Create a PSCredential object
$credential = New-Object System.Management.Automation.PSCredential("NuGet", $secureKey)

# Store using Windows Credential Manager via CredentialManager module
try {
    # First, try to install/import CredentialManager module if available
    if (!(Get-Module -ListAvailable -Name CredentialManager)) {
        Write-Host "Installing CredentialManager module..." -ForegroundColor Yellow
        Install-Module -Name CredentialManager -Force -Scope CurrentUser -ErrorAction Stop
    }
    Import-Module CredentialManager -ErrorAction Stop
    
    # Store the credential
    New-StoredCredential -Target "NuGetSliceR" -Credentials $credential -Type Generic -Persist LocalMachine | Out-Null
    Write-Host "✓ API key stored successfully in Windows Credential Manager!" -ForegroundColor Green
    Write-Host "  The publish-nuget.ps1 script will now use this stored key automatically." -ForegroundColor Gray
    
} catch {
    Write-Warning "CredentialManager module not available. Falling back to file storage."
    
    # Fallback: Store in user profile
    $keyDir = Join-Path $env:USERPROFILE ".nuget"
    if (!(Test-Path $keyDir)) {
        New-Item -ItemType Directory -Path $keyDir -Force | Out-Null
    }
    
    $keyFile = Join-Path $keyDir "SliceR.apikey"
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    $plainKey = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
    
    $plainKey | Out-File -FilePath $keyFile -NoNewline -Encoding ASCII
    Write-Host "✓ API key stored successfully in user profile!" -ForegroundColor Green
    Write-Host "  Location: $keyFile" -ForegroundColor Gray
}

Write-Host ""
Write-Host "You can now run: .\publish-nuget.ps1" -ForegroundColor Cyan