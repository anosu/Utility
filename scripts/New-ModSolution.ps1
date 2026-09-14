# Compatibility entry point. The implementation lives in ModEngineering.
param([Parameter(Mandatory)][string]$Project)
$ErrorActionPreference = 'Stop'
$repo = & dotnet msbuild $Project -nologo -getProperty:ModRepositoryRoot
if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate the project.' }
& python (Join-Path $repo.Trim() 'shared/ModEngineering/scripts/mod.py') solution --repo $repo.Trim() --local
if ($LASTEXITCODE -ne 0) { throw 'Could not generate the local solution.' }
