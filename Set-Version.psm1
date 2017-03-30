function Set-Version {
param(
	[string] $csprojPattern,
	[string] $version
)
	$versionTag = "VersionPrefix";

	Get-ChildItem $csprojPattern | ForEach-Object {
		$csproj = Get-Content -Raw -Path $_.FullName
		$originalVersion = [regex]::match($csproj, "<$versionTag>(.*)</$versionTag>").Groups[1].Value
		$csproj.Replace("<$versionTag>$originalVersion</$versionTag>", "<$versionTag>$version</$versionTag>") | Set-Content $_.FullName
	}

	return $buildVersion
}

Export-ModuleMember -Function Set-Version