#
# Creates a bunch of test files in the current directory
#
param ([int]$count)

Set-StrictMode -Latest

if ($count -eq 0) { $count = 1000 }

[Environment]::CurrentDirectory=(Get-Location -PSProvider FileSystem).ProviderPath

For ($i=0; $i -lt $count; $i++)
{
    $fileName = [System.IO.Path]::GetRandomFileName()
    $fileName
    [System.IO.File]::WriteAllText($fileName, $fileName);
}