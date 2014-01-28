mkdir "%LOCALAPPDATA%\JetBrains\dotCover\v2.2\Plugins" 2> NUL
mkdir "%LOCALAPPDATA%\JetBrains\dotCover\v2.2\Plugins\mspec" 2> NUL
copy /y Machine.Specifications.dll "%LOCALAPPDATA%\JetBrains\dotCover\v2.2\Plugins\mspec"
copy /y Machine.Specifications.pdb "%LOCALAPPDATA%\JetBrains\dotCover\v2.2\Plugins\mspec" > NUL
copy /y Machine.Specifications.dotCoverRunner.2.2.dll "%LOCALAPPDATA%\JetBrains\dotCover\v2.2\Plugins\mspec"
copy /y Machine.Specifications.dotCoverRunner.2.2.pdb "%LOCALAPPDATA%\JetBrains\dotCover\v2.2\Plugins\mspec" > NUL
pause
