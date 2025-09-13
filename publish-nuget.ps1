#Requires -Version 5.0
<#
.SYNOPSIS
    Builds, packages, and publishes the SliceR NuGet package.

.DESCRIPTION
    This script automates the process of building, packaging, and publishing the SliceR NuGet package.
    It supports multiple secure methods for providing the NuGet API key.

.PARAMETER ApiKey
    The NuGet API key. If not provided, the script will try other methods to obtain it.

.PARAMETER SkipTests
    Skip running tests before packaging.

.PARAMETER SkipConfirmation
    Skip the confirmation prompt before publishing.

.PARAMETER Source
    The NuGet source to publish to. Defaults to https://api.nuget.org/v3/index.json

.EXAMPLE
    .\publish-nuget.ps1
    Builds, tests, packages, and publishes using stored API key.

.EXAMPLE
    .\publish-nuget.ps1 -ApiKey "your-api-key-here"
    Publishes using the provided API key.

.EXAMPLE
    .\publish-nuget.ps1 -SkipTests -SkipConfirmation
    Publishes without running tests and without confirmation.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$ApiKey,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipTests,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipConfirmation,
    
    [Parameter(Mandatory=$false)]
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"

# Color output functions
function Write-ColorOutput {
    param([string]$Message, [ConsoleColor]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Write-Success { Write-ColorOutput $args[0] Green }
function Write-Info { Write-ColorOutput $args[0] Cyan }
function Write-Warning { Write-ColorOutput $args[0] Yellow }
function Write-Error { Write-ColorOutput $args[0] Red }

# Function to get NuGet API key from various sources
function Get-NuGetApiKey {
    param([string]$ProvidedKey)
    
    # 1. Use provided key if available
    if (![string]::IsNullOrWhiteSpace($ProvidedKey)) {
        Write-Info "Using provided API key."
        return $ProvidedKey
    }
    
    # 2. Check environment variable
    $envKey = $env:NUGET_API_KEY
    if (![string]::IsNullOrWhiteSpace($envKey)) {
        Write-Info "Using API key from NUGET_API_KEY environment variable."
        return $envKey
    }
    
    # 3. Check user profile file
    $userKeyFile = Join-Path $env:USERPROFILE ".nuget\SliceR.apikey"
    if (Test-Path $userKeyFile) {
        Write-Info "Using API key from user profile: $userKeyFile"
        $key = Get-Content $userKeyFile -Raw
        return $key.Trim()
    }
    
    # 4. Check Windows Credential Manager (Windows only)
    if ($IsWindows -or $PSVersionTable.Platform -eq 'Win32NT' -or !$PSVersionTable.Platform) {
        try {
            # Try using CredentialManager module first (most reliable)
            if (Get-Module -ListAvailable -Name CredentialManager) {
                Import-Module CredentialManager -ErrorAction SilentlyContinue
                $cred = Get-StoredCredential -Target "NuGetSliceR" -ErrorAction SilentlyContinue
                if ($cred -and $cred.Password) {
                    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($cred.Password)
                    $password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
                    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
                    if (![string]::IsNullOrWhiteSpace($password)) {
                        Write-Info "Using API key from Windows Credential Manager."
                        return $password
                    }
                }
            }
        } catch {
            # Credential Manager not available or failed - continue silently
        }
    }
    
    # 5. Prompt for key
    Write-Warning "No stored API key found. Please enter your NuGet API key."
    Write-Info "Tip: You can store your key securely by:"
    Write-Info "  1. Run: .\store-nuget-key.ps1 -ApiKey YOUR_KEY"
    Write-Info "  2. Set NUGET_API_KEY environment variable"
    Write-Info "  3. Save to $userKeyFile"
    Write-Host ""
    
    $secureKey = Read-Host "Enter NuGet API Key" -AsSecureString
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    $key = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
    
    # Offer to save the key
    $save = Read-Host "Would you like to save this key for future use? (y/n)"
    if ($save -eq 'y' -or $save -eq 'Y') {
        $saveLocation = Read-Host "Where to save? (1=User Profile, 2=Environment Variable, 3=Skip)"
        switch ($saveLocation) {
            "1" {
                $keyDir = Join-Path $env:USERPROFILE ".nuget"
                if (!(Test-Path $keyDir)) {
                    New-Item -ItemType Directory -Path $keyDir -Force | Out-Null
                }
                $key | Out-File -FilePath $userKeyFile -NoNewline
                Write-Success "API key saved to $userKeyFile"
            }
            "2" {
                [System.Environment]::SetEnvironmentVariable("NUGET_API_KEY", $key, "User")
                Write-Success "API key saved to user environment variable NUGET_API_KEY"
                Write-Info "Note: You may need to restart your PowerShell session for this to take effect."
            }
        }
    }
    
    return $key
}

# Main script
try {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "     SliceR NuGet Publishing Script     " -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Check if we're in the right directory
    if (!(Test-Path "SliceR.sln")) {
        throw "SliceR.sln not found. Please run this script from the repository root."
    }
    
    # Step 1: Clean previous builds
    Write-Info "Step 1: Cleaning previous builds..."
    dotnet clean --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "Clean failed" }
    Write-Success "✓ Clean completed"
    Write-Host ""
    
    # Step 2: Build the solution
    Write-Info "Step 2: Building solution in Release mode..."

    # Set ContinuousIntegrationBuild for deterministic builds in CI environments
    $buildArgs = @("--configuration", "Release", "--verbosity", "minimal")
    if ($env:CI -eq "true" -or $env:TF_BUILD -eq "true" -or $env:GITHUB_ACTIONS -eq "true") {
        Write-Info "CI environment detected - enabling ContinuousIntegrationBuild"
        $buildArgs += "-p:ContinuousIntegrationBuild=true"
    }

    dotnet build @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Success "✓ Build completed"
    Write-Host ""
    
    # Step 3: Run tests (unless skipped)
    if (!$SkipTests) {
        Write-Info "Step 3: Running tests..."
        dotnet test --configuration Release --no-build --verbosity minimal
        if ($LASTEXITCODE -ne 0) { 
            Write-Error "Tests failed! Publishing aborted."
            throw "Tests failed"
        }
        Write-Success "✓ All tests passed"
    } else {
        Write-Warning "Step 3: Skipping tests (-SkipTests specified)"
    }
    Write-Host ""
    
    # Step 4: Create NuGet package
    Write-Info "Step 4: Creating NuGet package..."
    $projectPath = "src\SliceR\SliceR.csproj"

    $packArgs = @($projectPath, "--configuration", "Release", "--no-build", "--verbosity", "minimal")
    if ($env:CI -eq "true" -or $env:TF_BUILD -eq "true" -or $env:GITHUB_ACTIONS -eq "true") {
        $packArgs += "-p:ContinuousIntegrationBuild=true"
    }

    dotnet pack @packArgs
    if ($LASTEXITCODE -ne 0) { throw "Pack failed" }
    Write-Success "✓ Package created"
    Write-Host ""
    
    # Step 5: Find and display package info
    Write-Info "Step 5: Package information:"
    $packageDir = "src\SliceR\bin\Release"
    $packageFile = Get-ChildItem -Path $packageDir -Filter "*.nupkg" | 
                   Where-Object { $_.Name -notlike "*.symbols.nupkg" } | 
                   Sort-Object LastWriteTime -Descending | 
                   Select-Object -First 1
    
    if (!$packageFile) {
        throw "No package file found in $packageDir"
    }
    
    # Extract version from package name
    $packageName = $packageFile.BaseName
    if ($packageName -match "SliceR\.(.+)") {
        $version = $matches[1]
    } else {
        $version = "Unknown"
    }
    
    Write-Host "  Package: $($packageFile.Name)" -ForegroundColor White
    Write-Host "  Version: $version" -ForegroundColor Green
    Write-Host "  Size: $([math]::Round($packageFile.Length / 1KB, 2)) KB" -ForegroundColor White
    Write-Host "  Path: $($packageFile.FullName)" -ForegroundColor Gray
    Write-Host ""
    
    # Step 6: Confirm publication
    if (!$SkipConfirmation) {
        Write-Warning "About to publish to NuGet.org"
        Write-Host "Source: $Source" -ForegroundColor Gray
        $confirm = Read-Host "Do you want to publish version $version? (y/n)"
        if ($confirm -ne 'y' -and $confirm -ne 'Y') {
            Write-Warning "Publication cancelled by user."
            exit 0
        }
    }
    
    # Step 7: Get API key
    Write-Info "Step 6: Obtaining API key..."
    $apiKeyToUse = Get-NuGetApiKey -ProvidedKey $ApiKey
    if ([string]::IsNullOrWhiteSpace($apiKeyToUse)) {
        throw "No API key provided or found"
    }
    Write-Success "✓ API key obtained"
    Write-Host ""
    
    # Step 8: Publish to NuGet
    Write-Info "Step 7: Publishing to NuGet.org..."
    dotnet nuget push $packageFile.FullName --api-key $apiKeyToUse --source $Source --skip-duplicate
    if ($LASTEXITCODE -ne 0) { 
        throw "Push failed. Please check your API key and network connection."
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Success "✓ Package published successfully!"
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Info "View your package at: https://www.nuget.org/packages/SliceR/$version"
    
} catch {
    Write-Host ""
    Write-Error "ERROR: $_"
    exit 1
}