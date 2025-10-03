Set-ExecutionPolicy Bypass -Scope Process -Force
.\RepoDoctor-AIM.ps1 -RunBuild -Verbose

param(
  [string]$RepoPath = ".",
  [switch]$RunBuild,
  [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

# ----------------------------
# Utilities
# ----------------------------
function New-BackupPath {
  param([string]$RepoRoot)
  $stamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
  $backupRoot = Join-Path $RepoRoot ".aimfix\$stamp"
  New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
  return $backupRoot
}

function Backup-FileIfChanged {
  param(
    [string]$FilePath,
    [string]$OriginalText,
    [string]$BackupRoot,
    [string]$RepoRoot
  )
  $rel = [IO.Path]::GetRelativePath((Resolve-Path $RepoRoot), (Resolve-Path $FilePath))
  $dest = Join-Path $BackupRoot $rel
  $destDir = Split-Path $dest -Parent
  if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Force -Path $destDir | Out-Null }
  Set-Content -Path $dest -Value $OriginalText -Encoding UTF8
}

function Sanitize-Text {
  param([string]$Text)
  # Remove zero-width chars and convert smart quotes to ASCII
  $t = $Text -replace "[\u200B-\u200D\uFEFF]", "
  $t = $t.Replace('"','"').Replace('"','"').Replace('"',"'").Replace('"',"'")
  # Normalize CRLF to LF (harmless for XML)
  $t = $t -replace "`r`n","`n"
  return $t
}

function Ensure-CsprojHeader {
  param([string]$Text)
  if ($Text -notmatch '^\s*<Project\s+Sdk="Microsoft\.NET\.Sdk">') {
    $t = $Text -replace '^\s*<Project\s+Sdk\s*=\s*["]?Microsoft\.NET\.Sdk["]?\s*>','<Project Sdk="Microsoft.NET.Sdk">',1
    if ($t -eq $Text) {
      $lines = $Text -split "`n"
      if ($lines.Length -gt 0 -and $lines[0] -match '^\s*<Project') {
        $lines[0] = '<Project Sdk="Microsoft.NET.Sdk">'
      } else {
        $lines = @('<Project Sdk="Microsoft.NET.Sdk">') + $lines
      }
      $t = $lines -join "`n"
    }
    return $t
  }
  return $Text
}

function Parse-CodeBehindInfo {
  <#
    Returns a hashtable with Namespace, Class, Base.
    Looks for: namespace XYZ; public sealed partial class Foo : Window
  #>
  param([string]$CsPath)

  if (-not (Test-Path $CsPath)) { return $null }
  $code = Get-Content $CsPath -Raw
  $code = $code -replace '[\u200B-\u200D\uFEFF]', ''

  $ns   = [regex]::Match($code, 'namespace\s+([A-Za-z0-9_.]+)\s*;')
  $cls  = [regex]::Match($code, 'class\s+([A-Za-z0-9_]+)\s*:\s*([A-Za-z0-9_.]+)')
  if (-not $cls.Success) { $cls = [regex]::Match($code, 'class\s+([A-Za-z0-9_]+)') }

  $result = @{}
  if ($ns.Success)  { $result.Namespace = $ns.Groups[1].Value }
  if ($cls.Success) {
    $result.Class = $cls.Groups[1].Value
    if ($cls.Groups.Count -ge 3) { $result.Base = $cls.Groups[2].Value }
  }
  return $result
}

function Infer-ExpectedRoot {
  param([string]$XamlPath, [hashtable]$CbInfo)
  $file = [IO.Path]::GetFileName($XamlPath)

  if ($file -ieq 'App.xaml') { return 'Application' }

  if ($CbInfo -and $CbInfo.ContainsKey('Base')) {
    switch -Regex ($CbInfo.Base) {
      'ContentDialog$' { return 'ContentDialog' }
      'Window$'        { return 'Window' }
      default          { } # fall through
    }
  }

  if ($file -like '*Window.xaml') { return 'Window' }
  if ($file -like '*Dialog.xaml') { return 'ContentDialog' }
  return 'UserControl'
}

function Build-FullClass {
  param([hashtable]$CbInfo, [string]$FallbackNs, [string]$FileNameNoExt)
  if ($CbInfo -and $CbInfo.Namespace -and $CbInfo.Class) {
    return "$($CbInfo.Namespace).$($CbInfo.Class)"
  }
  # Fallback: compute namespace from folder conventions
  if ($FallbackNs) {
    return "$FallbackNs.$FileNameNoExt"
  }
  return $FileNameNoExt
}

function Ensure-XamlHeader {
  <#
    Repairs root tag, x:Class, and core xmlns.
    Adds xmlns:muxc always (harmless) for WinUI controls usage.
  #>
  param([string]$FilePath, [string]$Text, [string]$ExpectedRoot, [string]$ExpectedClass)

  $t = Sanitize-Text $Text

  # find first start-tag
  $m = [regex]::Match($t, '^\s*<\s*([A-Za-z0-9_:.]+)\s+([^>]*?)>', 'Singleline')
  if ($m.Success) {
    $foundRoot = $m.Groups[1].Value
    $attrs     = $m.Groups[2].Value

    # normalize quotes on x:Class
    $attrs = $attrs -replace '\s*x:Class\s*=\s*"["]', ' x:Class="$1"'

    if ($attrs -notmatch 'x:Class\s*=\s*"[^"]+"') {
      $attrs = ' x:Class="' + $ExpectedClass + '" ' + $attrs
    } else {
      $attrs = [regex]::Replace($attrs, 'x:Class\s*=\s*"[^"]+"', 'x:Class="' + [regex]::Escape($ExpectedClass) + '"', 1)
    }

    if ($attrs -notmatch 'xmlns\s*=\s*"http://schemas\.microsoft\.com/winfx/2006/xaml/presentation"') {
      $attrs = ' xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" ' + $attrs
    }
    if ($attrs -notmatch 'xmlns:x\s*=\s*"http://schemas\.microsoft\.com/winfx/2006/xaml"') {
      $attrs = ' xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" ' + $attrs
    }
    if ($attrs -notmatch 'xmlns:muxc\s*=\s*"using:Microsoft\.UI\.Xaml\.Controls"') {
      $attrs = ' xmlns:muxc="using:Microsoft.UI.Xaml.Controls" ' + $attrs
    }

    # fix root name if needed
    if ($ExpectedRoot -and $foundRoot -ne $ExpectedRoot) {
      $t = $t -replace ('^\s*<\s*' + [regex]::Escape($foundRoot) + '\s+'), ('<' + $ExpectedRoot + ' ')
      $t = [regex]::Replace($t, '</\s*' + [regex]::Escape($foundRoot) + '\s*>\s*$', "</$ExpectedRoot>`n")
    }

    # rebuild first tag
    $newStart = '<' + ($ExpectedRoot ?? $foundRoot) + ' ' + ($attrs.Trim()) + '>'
    $t = $newStart + $t.Substring($m.Index + $m.Length)
    return $t
  }

  # If no start tag found, synthesize a minimal one
  return @"
<$ExpectedRoot x:Class="$ExpectedClass"
               xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               xmlns:muxc="using:Microsoft.UI.Xaml.Controls">
    <!-- TODO: add content -->
</$ExpectedRoot>
"@
}

# ----------------------------
# Run
# ----------------------------
$RepoRoot  = Resolve-Path $RepoPath
$BackupRoot = New-BackupPath -RepoRoot $RepoRoot
Write-Host "Repo: $RepoRoot" -ForegroundColor Cyan
Write-Host "Backup at: $BackupRoot" -ForegroundColor DarkCyan

$changed = @()

# 1) Fix .csproj headers
Get-ChildItem -Path $RepoRoot -Recurse -Include *.csproj | ForEach-Object {
  $fp = $_.FullName
  $orig = Get-Content $fp -Raw
  $txt  = Sanitize-Text $orig
  $txt  = Ensure-CsprojHeader $txt
  if ($txt -ne $orig) {
    Write-Host "[csproj] $fp" -ForegroundColor Yellow
    if (-not $WhatIf) {
      Backup-FileIfChanged -FilePath $fp -OriginalText $orig -BackupRoot $BackupRoot -RepoRoot $RepoRoot
      Set-Content -Path $fp -Value $txt -Encoding UTF8
    }
    $changed += $fp
  }
}

# 2) Fix all XAML files with code-behind aware repair
Get-ChildItem -Path $RepoRoot -Recurse -Include *.xaml | ForEach-Object {
  $xamlPath = $_.FullName

  # Skip generated artifacts if any
  if ($xamlPath -match '\\obj\\|\\bin\\') { return }

  $orig = Get-Content $xamlPath -Raw
  $san  = Sanitize-Text $orig

  # Find code-behind
  $csPath = "$xamlPath.cs"
  $cb = Parse-CodeBehindInfo -CsPath $csPath

  # Infer expected root and full class
  $expectedRoot = Infer-ExpectedRoot -XamlPath $xamlPath -CbInfo $cb

  # Fallback namespace for Tabs and Views
  $rel = [IO.Path]::GetRelativePath($RepoRoot, $xamlPath)
  $folderNs = switch -Regex ($rel) {
    'AIM\.WinUI\\Views\\Tabs\\' { 'AIM.WinUI.Views.Tabs' ; break }
    'AIM\.WinUI\\Views\\'       { 'AIM.WinUI.Views' ; break }
    default                     { 'AIM.WinUI' }
  }

  $fileNameNoExt = [IO.Path]::GetFileNameWithoutExtension($xamlPath)
  $expectedClass = Build-FullClass -CbInfo $cb -FallbackNs $folderNs -FileNameNoExt $fileNameNoExt

  # Build corrected XAML
  $fixed = Ensure-XamlHeader -FilePath $xamlPath -Text $san -ExpectedRoot $expectedRoot -ExpectedClass $expectedClass

  if ($fixed -ne $orig) {
    Write-Host "[xaml] $rel  (root=$expectedRoot, class=$expectedClass)" -ForegroundColor Green
    if (-not $WhatIf) {
      Backup-FileIfChanged -FilePath $xamlPath -OriginalText $orig -BackupRoot $BackupRoot -RepoRoot $RepoRoot
      Set-Content -Path $xamlPath -Value $fixed -Encoding UTF8
    }
    $changed += $xamlPath
  }
}

Write-Host "
Write-Host "Changed files: $($changed.Count)" -ForegroundColor Magenta
$changed | ForEach-Object { Write-Host " - $_" -ForegroundColor DarkGray }
Write-Host "

if ($RunBuild) {
  Push-Location $RepoRoot
  try {
    Write-Host "dotnet restore" -ForegroundColor Cyan
    dotnet restore
    Write-Host "dotnet build -c Debug" -ForegroundColor Cyan
    dotnet build -c Debug
  } finally {
    Pop-Location
  }
}

Write-Host "Done." -ForegroundColor Cyan
