pushd GreetingsWeb || exit
rm -rf out
dotnet restore
dotnet build
dotnet publish -c Release -o out
docker build .
popd || exit
pushd SalutationAnalytics || exit
rm -rf out
dotnet restore
dotnet build
dotnet publish -c Release -o out
docker build .
popd || exit

