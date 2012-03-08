properties {
  $base_dir  = resolve-path .
  $lib_dir = "$base_dir\SharedLibs"
  $build_dir = "$base_dir\build"
  $buildartifacts_dir = "$build_dir\"
  $sln_file = "$base_dir\RavenFS.sln"
  $version = "1.0"
  $tools_dir = "$base_dir\Tools"
  $release_dir = "$base_dir\Release"
  $uploader = "..\Uploader\S3Uploader.exe"
  
  $ravenfs_web = @( "RavenFS.???", "AsyncCtpLibrary.???", "Esent.Interop.???", "Lucene.Net.???", "Newtonsoft.Json.???", "NLog.???" );
  $ravenfs_client = @( "RavenFS.Client.???", "AsyncCtpLibrary.???", "Newtonsoft.Json.???" );
  $ravenfs_silverlight = @( "RavenFS.Client.Silverlight.???", "AsyncCtpLibrary_Silverlight.???", "Newtonsoft.Json.Silverlight.???" );
      
  $test_prjs = @("RavenFS.Tests.dll" );
}
include .\psake_ext.ps1

task default -depends Release

task Verify40 {
	if( (ls "$env:windir\Microsoft.NET\Framework\v4.0*") -eq $null ) {
		throw "Building Raven requires .NET 4.0, which doesn't appear to be installed on this machine"
	}
}


task Clean {
  remove-item -force -recurse $buildartifacts_dir -ErrorAction SilentlyContinue
  remove-item -force -recurse $release_dir -ErrorAction SilentlyContinue
}

task Init -depends Verify40, Clean {
  $global:uploadCategory = "RavenFS"
    
  if($env:BUILD_NUMBER -ne $null) {
    $env:buildlabel  = $env:BUILD_NUMBER
	}
	
	if($env:buildlabel -eq $null) {
		$env:buildlabel = "13"
	}
	
	$projectFiles = ls -path $base_dir -include *.csproj -recurse | 
					Where { $_ -notmatch [regex]::Escape($lib_dir) } | 
					Where { $_ -notmatch [regex]::Escape($tools_dir) }
	
	$notclsCompliant = @("RavenFS.Client.Silverlight", "RavenFS.Studio")
	
	foreach($projectFile in $projectFiles) {
		
		$projectDir = [System.IO.Path]::GetDirectoryName($projectFile)
		$projectName = [System.IO.Path]::GetFileName($projectDir)
		$asmInfo = [System.IO.Path]::Combine($projectDir, [System.IO.Path]::Combine("Properties", "AssemblyInfo.cs"))
		
		$clsComliant = "true"
		
		if([System.Array]::IndexOf($notclsCompliant, $projectName) -ne -1) {
            $clsComliant = "false"
		}
		
		Generate-Assembly-Info `
			-file $asmInfo `
			-title "$projectName $version.0.0" `
			-description "A distributed, replicated, file server for .NET" `
			-company "Hibernating Rhinos" `
			-product "RavenFS $version.0.0" `
			-version "$version.0" `
			-fileversion "1.0.$env:buildlabel.0" `
			-copyright "Copyright © Hibernating Rhinos and Ayende Rahien 2004 - 2010" `
			-clsCompliant $clsComliant
	}
	
	new-item $release_dir -itemType directory -ErrorAction SilentlyContinue
	new-item $build_dir -itemType directory -ErrorAction SilentlyContinue
	
	copy $tools_dir\xUnit\*.* $build_dir
}


task Compile -depends Init {
	
 $v4_net_version = (ls "$env:windir\Microsoft.NET\Framework\v4.0*").Name
 exec { &"C:\Windows\Microsoft.NET\Framework\$v4_net_version\MSBuild.exe" "$sln_file" /p:OutDir="$buildartifacts_dir\" }
 cp (Get-DependencyPackageFiles 'NLog.2') $build_dir -force
 cp (Get-DependencyPackageFiles 'Newtonsoft.Json') $build_dir -force
}

task Test -depends Compile{
  $old = pwd
  cd $build_dir
  Write-Host $test_prjs
  foreach($test_prj in $test_prjs) {
    Write-Host "Testing $build_dir\$test_prj"
    exec { &"$build_dir\xunit.console.clr4.exe" "$build_dir\$test_prj" } 
  }
  cd $old
}

task ReleaseNoTests -depends DoRelease {

}


task Release -depends Test,DoRelease { 
}


task ZipOutput {
	
	if($env:buildlabel -eq 13)
	{
      return 
	}

	$old = pwd
	
	cd $build_dir\Output
	
	$file = "$release_dir\$global:uploadCategory-Build-$env:buildlabel.zip"
		
	exec { 
		& $tools_dir\zip.exe -9 -A -r `
			$file `
			EmbeddedClient\*.* `
			Client\*.* `
			Samples\*.* `
			Smuggler\*.* `
			Backup\*.* `
			Client-3.5\*.* `
			Web\*.* `
			Bundles\*.* `
			Web\bin\*.* `
			Server\*.* `
			*.*
	}
	
    cd $old

}

task CleanOutputDirectory { 
	remove-item $build_dir\Output -Recurse -Force  -ErrorAction SilentlyContinue
}

task PrepareForZip -depends CleanOutputDirectory {
    mkdir $build_dir\Output
    mkdir $build_dir\Output\Web
    mkdir $build_dir\Output\Web\bin
    mkdir $build_dir\Output\Silverlight
    mkdir $build_dir\Output\Client

  foreach($file in $ravenfs_web) {
    cp "$build_dir\$file" $build_dir\Output\Web\bin
  }
  cp "$base_dir\DefaultConfigs\Web.config" $build_dir\Output\Web

  foreach($client_dll in $ravenfs_client) {
    cp "$build_dir\$client_dll" $build_dir\Output\Client
  }

  foreach($client_dll in $ravenfs_silverlight) {
    cp "$build_dir\$client_dll" $build_dir\Output\Silverlight
  }
}

task DoRelease -depends Compile, `
    PrepareForZip, `
	ZipOutput {	
	Write-Host "Done building RavenFS"
}


task Upload -depends DoRelease {
	Write-Host "Starting upload"
	if (Test-Path $uploader) {
		$log = $env:push_msg 
		if($log -eq $null -or $log.Length -eq 0) {
		  $log = git log -n 1 --oneline		
		}
		
		$file = "$release_dir\$global:uploadCategory-Build-$env:buildlabel.zip"
		write-host "Executing: $uploader '$global:uploadCategory' $file '$log'"
		&$uploader "$uploadCategory" $file "$log"
			
		if ($lastExitCode -ne 0) {
			write-host "Failed to upload to S3: $lastExitCode"
			throw "Error: Failed to publish build"
		}
	}
	else {
		Write-Host "could not find upload script $uploadScript, skipping upload"
	}
	
	
}

