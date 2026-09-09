#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Project
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath $Project).Path
# Evaluate the same local overrides as a command-line build. No restore or build
# is needed to discover the actual projects selected by the Mod.
$evaluation = & dotnet msbuild $projectPath -nologo `
    -getProperty:ModRepositoryRoot,UtilityProjectPath,AdapterProjectPath,UseAdapter `
    -p:BuildingInsideVisualStudio=false
if ($LASTEXITCODE -ne 0) {
    throw 'Could not evaluate the Mod project.'
}
$properties = ($evaluation -join "`n" | ConvertFrom-Json).Properties
if (-not $properties.ModRepositoryRoot -or -not $properties.UtilityProjectPath) {
    throw 'The project does not import the shared Mod build configuration.'
}

$repositoryRoot = [IO.Path]::GetFullPath($properties.ModRepositoryRoot)
$repositoryName = [IO.Path]::GetFileName($repositoryRoot.TrimEnd('/', '\'))
$solutionPath = Join-Path $repositoryRoot "$repositoryName.local.slnx"
$projects = @($projectPath, $properties.UtilityProjectPath)
if ($properties.UseAdapter -eq 'true') {
    $projects += $properties.AdapterProjectPath
}

$document = [xml]'<Solution />'
foreach ($path in $projects) {
    $resolved = (Resolve-Path -LiteralPath $path).Path
    $relative = [IO.Path]::GetRelativePath($repositoryRoot, $resolved).Replace('\', '/')
    $element = $document.CreateElement('Project')
    $element.SetAttribute('Path', $relative)
    [void]$document.DocumentElement.AppendChild($element)
}

$settings = [Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.IndentChars = '    '
$settings.OmitXmlDeclaration = $true
$settings.Encoding = [Text.UTF8Encoding]::new($false)
$settings.NewLineChars = "`n"
$writer = [Xml.XmlWriter]::Create($solutionPath, $settings)
try {
    $document.Save($writer)
} finally {
    $writer.Dispose()
}
Write-Output "Open in Visual Studio 2022 17.14 or newer: $solutionPath"
