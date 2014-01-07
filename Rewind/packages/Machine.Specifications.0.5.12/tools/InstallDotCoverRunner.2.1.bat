mkdir "%LOCALAPPDATA%\JetBrains\dotCover\v2.1\Plugins" 2> NUL
mkdir "%LOCALAPPDATA%\JetBrains\dotCover\v2.1\Plugins\mspec" 2> NUL
copy /y Machine.Specifications.dll "%LOCALAPPDATA%\JetBrains\dotCover\v2.1\Plugins\mspec"
copy /y Machine.Specifications.pdb "%LOCALAPPDATA%\JetBrains\dotCover\v2.1\Plugins\mspec" > NUL
copy /y Machine.Specifications.dotCoverRunner.2.1.dll "%LOCALAPPDATA%\JetBrains\dotCover\v2.1\Plugins\mspec"
copy /y Machine.Specifications.dotCoverRunner.2.1.pdb "%LOCALAPPDATA%\JetBrains\dotCover\v2.1\Plugins\mspec" > NUL
pause
