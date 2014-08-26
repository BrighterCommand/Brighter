mkdir "%APPDATA%\JetBrains\dotCover\v2.5\Plugins" 2> NUL
mkdir "%APPDATA%\JetBrains\dotCover\v2.5\Plugins\mspec" 2> NUL
copy /y Machine.Specifications.dll "%APPDATA%\JetBrains\dotCover\v2.5\Plugins\mspec"
copy /y Machine.Specifications.pdb "%APPDATA%\JetBrains\dotCover\v2.5\Plugins\mspec" > NUL
copy /y Machine.Specifications.dotCoverRunner.2.5.dll "%APPDATA%\JetBrains\dotCover\v2.5\Plugins\mspec"
copy /y Machine.Specifications.dotCoverRunner.2.5.pdb "%APPDATA%\JetBrains\dotCover\v2.5\Plugins\mspec" > NUL
pause
