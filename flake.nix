{
  description = "A very basic flake";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
  };

  outputs =
    {
      nixpkgs,
      flake-utils,
      ...
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        # pkgs = nixpkgs.legacyPackages.${system};
        pkgs = import nixpkgs { inherit system; };

        # Define the .NET SDK version you want to use
        dotnetSdk = pkgs.dotnetCorePackages.sdk_10_0-bin;
      in
      {
        devShells.default = pkgs.mkShell {
          packages = [
            dotnetSdk

            pkgs.netcoredbg # Debugger for .NET Core
            # pkgs.roslyn-ls # LSP for VS Code / Emacs / Vim
          ];
        };

        # Environment variables
        # 1. Essential: Tell dotnet tools where to find the SDK
        DOTNET_ROOT = "${dotnetSdk}";
      }
    );
}
