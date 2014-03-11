mkdir "%APPDATA%\JetBrains\dotCover\v2.2\Plugins" 2> NUL
mkdir "%APPDATA%\JetBrains\dotCover\v2.2\Plugins\mspec" 2> NUL
copy /y Machine.Specifications.dll "%APPDATA%\JetBrains\dotCover\v2.2\Plugins\mspec"
copy /y Machine.Specifications.pdb "%APPDATA%\JetBrains\dotCover\v2.2\Plugins\mspec" > NUL
copy /y Machine.Specifications.dotCoverRunner.2.2.dll "%APPDATA%\JetBrains\dotCover\v2.2\Plugins\mspec"
copy /y Machine.Specifications.dotCoverRunner.2.2.pdb "%APPDATA%\JetBrains\dotCover\v2.2\Plugins\mspec" > NUL
pause
