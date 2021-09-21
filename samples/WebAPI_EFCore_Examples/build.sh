pushd GreetingsAdapters || exit
rm -rf out
dotnet restore
dotnet build
dotnet publish -c Release -o out
popd || exit
pushd GreetingsWatcher || exit
rm -rf out
dotnet restore
dotnet build
dotnet publish -c Release -o out
popd || exit

