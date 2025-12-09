#!/bin/bash

test_path=$(pwd)/tests/*
tool_path=$(pwd)/tools/Paramore.Brighter.Test.Generator/Paramore.Brighter.Test.Generator.csproj

echo "Using test generator tool at: $tool_path"
echo "Starting test generation..."

for test_folder in $test_path; do
        if [ -d "$test_folder" ]; then
                cd $test_folder
                echo "Generating test for $test_folder"
                dotnet run --project $tool_path
                cd ..
        fi
done

echo "Test generation completed."
