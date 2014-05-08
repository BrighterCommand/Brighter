del "%APPDATA%\JetBrains\ReSharper\v8.2\vs10.0\Plugins\Machine.Specifications.*" 2> NUL
del "%APPDATA%\JetBrains\ReSharper\v8.2\vs11.0\Plugins\Machine.Specifications.*" 2> NUL
del "%APPDATA%\JetBrains\ReSharper\v8.2\vs12.0\Plugins\Machine.Specifications.*" 2> NUL

mkdir "%APPDATA%\JetBrains\ReSharper\v8.2\Plugins" 2> NUL
mkdir "%APPDATA%\JetBrains\ReSharper\v8.2\Plugins\mspec" 2> NUL
copy /y Machine.Specifications.dll "%APPDATA%\JetBrains\ReSharper\v8.2\Plugins\mspec"
copy /y Machine.Specifications.pdb "%APPDATA%\JetBrains\ReSharper\v8.2\Plugins\mspec" > NUL
copy /y Machine.Specifications.ReSharperRunner.8.2.dll "%APPDATA%\JetBrains\ReSharper\v8.2\Plugins\mspec"
copy /y Machine.Specifications.ReSharperRunner.8.2.pdb "%APPDATA%\JetBrains\ReSharper\v8.2\Plugins\mspec" > NUL
pause
