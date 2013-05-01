param($installPath, $toolsPath, $package, $project)

foreach ($fontFile in $project.ProjectItems.Item("Content").ProjectItems.Item("font").ProjectItems)
{
	$fontFile.Properties.Item("BuildAction").Value = 2;
}