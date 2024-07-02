Write-Output "build: Build started"

Push-Location $PSScriptRoot

if(Test-Path .\artifacts) {
	Write-Output "build: Cleaning ./artifacts"
	Remove-Item ./artifacts -Force -Recurse
}

& dotnet restore --no-cache

$branch = @{ $true = $env:APPVEYOR_REPO_BRANCH; $false = $(git symbolic-ref --short -q HEAD) }[$env:APPVEYOR_REPO_BRANCH -ne $NULL];
$revision = @{ $true = "{0:00000}" -f [convert]::ToInt32("0" + $env:APPVEYOR_BUILD_NUMBER, 10); $false = "local" }[$env:APPVEYOR_BUILD_NUMBER -ne $NULL];
$suffix = @{ $true = ""; $false = "$($branch.Substring(0, [math]::Min(10,$branch.Length)))-$revision"}[$branch -eq "main" -and $revision -ne "local"]
$commitHash = $(git rev-parse --short HEAD)
$buildSuffix = @{ $true = "$($suffix)-$($commitHash)"; $false = "$($branch)-$($commitHash)" }[$suffix -ne ""]

echo "build: Package version suffix is $suffix"
echo "build: Build version suffix is $buildSuffix" 

foreach ($src in Get-ChildItem src/*) {
    Push-Location $src

	Write-Output "build: Packaging project in $src"

    & dotnet build -c Release --version-suffix=$buildSuffix -p:ContinuousIntegrationBuild=true
    if($LASTEXITCODE -ne 0) { throw "Build failed" }
    
    if ($suffix) {
        & dotnet pack -c Release -o ..\..\artifacts --version-suffix=$suffix --no-build
    } else {
        & dotnet pack -c Release -o ..\..\artifacts --no-build
    }
    if($LASTEXITCODE -ne 0) { throw "Packaging failed" }

    Pop-Location
}

foreach ($test in Get-ChildItem test/*.Tests) {
    Push-Location $test

	Write-Output "build: Testing project in $test"

    & dotnet test -c Release
    if($LASTEXITCODE -ne 0) { throw "Testing failed" }

    Pop-Location
}

Pop-Location