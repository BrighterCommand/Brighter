* Code organisation
- Content/fonts/Scripts - populated by nuget
- assets - populates in-memory website

* if the site looks rubbish
- likely cause is the above assets are added via nuget
- on upgrade via nuget js files change as the version is bumped and thus :
  # need to reset files to be embedded resources
  # may need to change version numbers in referenced asset files