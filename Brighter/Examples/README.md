# Use of NuGet not solution references #
Note that the Examples **MUST** use the NuGet packages not direct assembly references. This is **intentional** as we want the examples to always reflect shipped code. It does create a slight double step when altering master because you have to build, create and release NuGet packages, then change and check examples. This will mean that the examples are momentarily out of synchronization with the packages whilst this is done.

For now we consider this better than trying to look at the examples and being confused because they do not match the current released NuGet packages.

We could argue that consumers should look at release branch examples instead, and thus we don't need this double-hop. However, for now I prefer the simplicity of users finding examples on the default project in GitHub over the pain of the double-hop when releasing as releases are more infrequent than project views.



