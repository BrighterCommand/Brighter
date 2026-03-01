# Get the current working directory
$currentDir = Get-Location

# Define paths using Join-Path for cross-platform compatibility (handling \ vs /)
$testsDir = Join-Path $currentDir "tests"
$toolPath = Join-Path $currentDir "tools\Paramore.Brighter.Test.Generator\Paramore.Brighter.Test.Generator.csproj"

Write-Host "Using test generator tool at: $toolPath"
Write-Host "Starting test generation..."

# Check if the tests directory exists
if (Test-Path $testsDir) {
    # Get-ChildItem is like 'ls'. -Directory ensures we only get folders.
    $testFolders = Get-ChildItem -Path $testsDir -Directory

    foreach ($folder in $testFolders) {
        Write-Host "Generating test for $($folder.Name)"
        
        # Enter the directory (pushes previous path to a stack)
        Push-Location -Path $folder.FullName
        
        # Run the dotnet command
        dotnet run --project $toolPath
        
        # Return to the previous directory
        Pop-Location
    }
} else {
    Write-Host "Directory '$testsDir' not found." -ForegroundColor Red
}

Write-Host "Test generation completed."
