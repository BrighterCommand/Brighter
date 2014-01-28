del "%APPDATA%\JetBrains\ReSharper\v7.0\vs10.0\Plugins\Machine.Specifications.*" 2> NUL
del "%APPDATA%\JetBrains\ReSharper\v7.0\vs11.0\Plugins\Machine.Specifications.*" 2> NUL

mkdir "%APPDATA%\JetBrains\ReSharper\v7.0\Plugins" 2> NUL
mkdir "%APPDATA%\JetBrains\ReSharper\v7.0\Plugins\mspec" 2> NUL
copy /y Machine.Specifications.dll "%APPDATA%\JetBrains\ReSharper\v7.0\Plugins\mspec"
copy /y Machine.Specifications.pdb "%APPDATA%\JetBrains\ReSharper\v7.0\Plugins\mspec" > NUL
copy /y Machine.Specifications.ReSharperRunner.7.0.dll "%APPDATA%\JetBrains\ReSharper\v7.0\Plugins\mspec"
copy /y Machine.Specifications.ReSharperRunner.7.0.pdb "%APPDATA%\JetBrains\ReSharper\v7.0\Plugins\mspec" > NUL
pause
