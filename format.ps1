# PowerShell script to format the solution using dotnet format

param(
    [switch]$VerifyOnly,
    [switch]$v,
    [ValidateSet("q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic")]
    [string]$Verbosity = "normal",
    [switch]$IncludeGenerated,
    [switch]$ChangedOnly,
    [switch]$Help,
    [switch]$h
)

$ErrorActionPreference = "Stop"

# Handle aliases
if ($v) { $VerifyOnly = $true }
if ($h) { $Help = $true }

# Show help if requested
if ($Help) {
    Write-Host "Code Formatter" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage: ./format.ps1 [options]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -VerifyOnly, -v        Run in verification mode (no changes made)"
    Write-Host "  -Verbosity <level>     Set verbosity: quiet, minimal, normal, detailed, diagnostic"
    Write-Host "  -IncludeGenerated      Include generated code files"
    Write-Host "  -ChangedOnly           Format only files changed in git"
    Write-Host "  -Help, -h              Show this help message"
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  ./format.ps1                    # Format all files"
    Write-Host "  ./format.ps1 -v                 # Verify formatting without changes"
    Write-Host "  ./format.ps1 -ChangedOnly       # Format only changed files"
    Write-Host "  ./format.ps1 -Verbosity detailed # Format with detailed output"
    exit 0
}

# Check if dotnet CLI is available
try {
    $dotnetVersion = & dotnet --version
    Write-Host "Using .NET SDK version: $dotnetVersion" -ForegroundColor Cyan
}
catch {
    Write-Host "Error: dotnet CLI is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

# Check if solution file exists
$solutionFile = "SliceR.sln"
if (-not (Test-Path $solutionFile)) {
    Write-Host "Error: Solution file '$solutionFile' not found in current directory" -ForegroundColor Red
    Write-Host "Please run this script from the repository root directory" -ForegroundColor Yellow
    exit 1
}

# Build the dotnet format command
$formatArgs = @("format", $solutionFile)

# Add verbosity
$formatArgs += "--verbosity"
$formatArgs += $Verbosity

# Add verify-only flag if specified
if ($VerifyOnly) {
    $formatArgs += "--verify-no-changes"
    Write-Host "Running in verification mode (no files will be modified)" -ForegroundColor Yellow
}

# Add include-generated flag if specified
if ($IncludeGenerated) {
    $formatArgs += "--include-generated"
    Write-Host "Including generated code files" -ForegroundColor Yellow
}

# Handle changed-only mode
if ($ChangedOnly) {
    # Get list of changed files from git
    try {
        $changedFiles = & git diff --name-only HEAD
        $stagedFiles = & git diff --cached --name-only
        $allChangedFiles = ($changedFiles + $stagedFiles) | Select-Object -Unique
        
        if ($allChangedFiles.Count -eq 0) {
            Write-Host "No changed files found in git" -ForegroundColor Yellow
            exit 0
        }
        
        # Filter for C# files
        $csFiles = $allChangedFiles | Where-Object { $_ -like "*.cs" }
        
        if ($csFiles.Count -eq 0) {
            Write-Host "No changed C# files found" -ForegroundColor Yellow
            exit 0
        }
        
        Write-Host "Formatting $($csFiles.Count) changed file(s)" -ForegroundColor Cyan
        
        # Add include filter for changed files
        foreach ($file in $csFiles) {
            $formatArgs += "--include"
            $formatArgs += $file
        }
    }
    catch {
        Write-Host "Error: Unable to get changed files from git" -ForegroundColor Red
        Write-Host "Make sure you're in a git repository" -ForegroundColor Yellow
        exit 1
    }
}

# Display what we're about to do
Write-Host ""
if ($VerifyOnly) {
    Write-Host "Verifying code formatting for $solutionFile..." -ForegroundColor Green
}
else {
    Write-Host "Formatting code in $solutionFile..." -ForegroundColor Green
}
Write-Host ""

# Run dotnet format
try {
    & dotnet $formatArgs
    $exitCode = $LASTEXITCODE
    
    Write-Host ""
    
    if ($exitCode -eq 0) {
        if ($VerifyOnly) {
            Write-Host "✓ All files are properly formatted" -ForegroundColor Green
        }
        else {
            Write-Host "✓ Code formatting completed successfully" -ForegroundColor Green
        }
    }
    else {
        if ($VerifyOnly) {
            Write-Host "✗ Some files need formatting" -ForegroundColor Red
            Write-Host "Run './format.ps1' to fix formatting issues" -ForegroundColor Yellow
        }
        else {
            Write-Host "✗ Code formatting failed with exit code $exitCode" -ForegroundColor Red
        }
        exit $exitCode
    }
}
catch {
    Write-Host "✗ An error occurred while running dotnet format:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

# Show additional info for successful formatting
if (-not $VerifyOnly -and $exitCode -eq 0) {
    Write-Host ""
    Write-Host "Tip: Use './format.ps1 -v' to verify formatting without making changes" -ForegroundColor Cyan
    Write-Host "Tip: Use './format.ps1 -ChangedOnly' to format only git-changed files" -ForegroundColor Cyan
}