# Building #
## Dependency on IdentityModelv2 Hawk ##
We depend on Thinktecture's Identity Model for support for Hawk authentication. There is no .NET Core version of Identity Model but there is
an IdentityModelv2. However, the Hawk model has not been ported to that yet and it is in beta. For this reason I have forked IdentityModelv2 
https://github.com/iancooper/IdentityModelv2 and retro-fitted the Hawk support. Although this might make it back to IdentityModelv2 via a PR
it also may not. For now though there is a nuget package wrapper for the forked version of this project in the Lib folder. You will thus need to add
the Lib folder to your list of NuGet package sources to get this to build.

 