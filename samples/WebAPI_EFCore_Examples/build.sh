dotnet restore
dotnet build
pushd GreetingsAPI || exit
rm -rf out
dotnet publish -c Release -o out
popd || exit
