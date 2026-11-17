$ErrorActionPreference='Stop'
$atlasRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$atlasBuild=Join-Path $atlasRoot 'Builds\AtlasPages'
$atlasOutput=Join-Path $atlasRoot 'ReleaseArtifacts'
if(!(Test-Path -LiteralPath (Join-Path $atlasBuild 'index.html') -PathType Leaf)){throw 'Primero compila Atlas Web en Unity.'}
New-Item -ItemType Directory -Force -Path $atlasOutput | Out-Null
Set-Content -LiteralPath (Join-Path $atlasBuild '.nojekyll') -Value '' -NoNewline
$atlasManifest=& node (Join-Path $PSScriptRoot 'verify-build.cjs') $atlasBuild
if($LASTEXITCODE -ne 0){throw 'La verificación del build falló.'}
$atlasManifest | Set-Content -LiteralPath (Join-Path $atlasOutput 'build-manifest.json') -Encoding utf8
$atlasZip=Join-Path $atlasOutput 'Atlas-Psicologia-Web.zip'
$atlasItems=@(Get-ChildItem -LiteralPath $atlasBuild -Force | Select-Object -ExpandProperty FullName)
Compress-Archive -LiteralPath $atlasItems -DestinationPath $atlasZip -CompressionLevel NoCompression -Force
$atlasHash=(Get-FileHash -LiteralPath $atlasZip -Algorithm SHA256).Hash.ToLowerInvariant()
($atlasHash+'  Atlas-Psicologia-Web.zip') | Set-Content -LiteralPath (Join-Path $atlasOutput 'SHA256SUMS.txt') -Encoding ascii
Write-Output ('Build preparado: '+$atlasZip)
Write-Output ('SHA256: '+$atlasHash)
