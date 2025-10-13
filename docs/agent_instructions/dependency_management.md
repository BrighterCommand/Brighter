# Dependency Management

- Use Directory.Packages.props for central package management.
- All projects should reference the central Directory.Packages.props file
- Inside, you then define each of the respective package versions required of your projects using <PackageVersion /> elements that define the package ID and version.

``` xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.1" />
  </ItemGroup>
</Project>
```

- Within the project files, you then reference the packages without specifying a version, as the version is managed centrally.

``` xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>
</Project>
```

- Align all Microsoft.Extensions.* and System.* package versions.
- Avoid mixing preview and stable package versions.
- Enable CentralPackageTransitivePinningEnabled where possible.