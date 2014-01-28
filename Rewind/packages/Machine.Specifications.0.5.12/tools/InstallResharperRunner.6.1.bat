del "%APPDATA%\JetBrains\ReSharper\v6.1\vs9.0\Plugins\Machine.Specifications.*" 2> NUL
del "%APPDATA%\JetBrains\ReSharper\v6.1\vs10.0\Plugins\Machine.Specifications.*" 2> NUL

mkdir "%APPDATA%\JetBrains\ReSharper\v6.1\Plugins" 2> NUL
mkdir "%APPDATA%\JetBrains\ReSharper\v6.1\Plugins\mspec" 2> NUL
copy /y Machine.Specifications.dll "%APPDATA%\JetBrains\ReSharper\v6.1\Plugins\mspec"
copy /y Machine.Specifications.pdb "%APPDATA%\JetBrains\ReSharper\v6.1\Plugins\mspec" > NUL
copy /y Machine.Specifications.ReSharperRunner.6.1.dll "%APPDATA%\JetBrains\ReSharper\v6.1\Plugins\mspec"
copy /y Machine.Specifications.ReSharperRunner.6.1.pdb "%APPDATA%\JetBrains\ReSharper\v6.1\Plugins\mspec" > NUL
pause
