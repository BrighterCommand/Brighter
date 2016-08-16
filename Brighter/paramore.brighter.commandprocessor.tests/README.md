# dotnetcore support #
In order to get this working we need to use FakeItEasy. That is currently supported out of a beta available via an appveyor feed.
As we already have a lib directory I have downloaded the beta to there, so that we can use the same lib folder addition to NuGet over adding yet another NuGet source.
The assumption is that a non-beta version will appear instead of an update to the appveyor feed as the next version we build.