
Current we are using  MinVer https://github.com/adamralph/minver for versioning which is based on git tags.

If you tag the version is "as is" for example `9.1.3` the next version is base off the last tag + alpha + the height of the commit `9.1.4-alpha.0.1`

The Git Hub action is also triggered by a tag and if the build is successful the artifacts will be pushed to nuget.

When you create a release in GitHub is also tags the code so we generally use that to trigger a release.


- Click on "Releases" https://github.com/BrighterCommand/Brighter/releases
- Edit the current draft release notes and copy the release notes to use in the new release
- Delete the draft release
- Click "Draft a new release"
- Click "Choose a tag"
- Create a new tag `9.1.3`
- Make sure the "Target" is master
- Add "Release Title" `9.1.3`
- Paste release notes from the Draft
- Don't click "This is a pre-release"
- Click "Create a discussion for this release"
- Click "Publish release"
- Check the github action you should see the "release" step go green.
- Check nuget https://www.nuget.org/profiles/BrighterCommand to see if the packages are there can take a while.
- If all has worked, delete the current draft release, a new one will be created on the next pull request


If you are thinking why don't we just convert the pre-release draft to a proper release? Yes that would be nice, but due to a bug in github github actions do not trigger when you do that  ¯\_(ツ)_/¯. 
