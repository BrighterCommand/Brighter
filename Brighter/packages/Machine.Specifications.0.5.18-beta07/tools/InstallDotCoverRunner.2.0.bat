mkdir "%APPDATA%\JetBrains\dotCover\v2.0\Plugins" 2> NUL
mkdir "%APPDATA%\JetBrains\dotCover\v2.0\Plugins\mspec" 2> NUL
copy /y Machine.Specifications.dll "%APPDATA%\JetBrains\dotCover\v2.0\Plugins\mspec"
copy /y Machine.Specifications.pdb "%APPDATA%\JetBrains\dotCover\v2.0\Plugins\mspec" > NUL
copy /y Machine.Specifications.dotCoverRunner.2.0.dll "%APPDATA%\JetBrains\dotCover\v2.0\Plugins\mspec"
copy /y Machine.Specifications.dotCoverRunner.2.0.pdb "%APPDATA%\JetBrains\dotCover\v2.0\Plugins\mspec" > NUL
pause
