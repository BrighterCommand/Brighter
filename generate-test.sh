#!/bin/bash

set -e  # Exit on error
set -u  # Exit on undefined variable

# Store the original directory
original_dir=$(pwd)

test_path="${original_dir}/tests"
tool_path="${original_dir}/tools/Paramore.Brighter.Test.Generator/Paramore.Brighter.Test.Generator.csproj"

# Verify tool exists
if [ ! -f "$tool_path" ]; then
    echo "Error: Test generator tool not found at: $tool_path" >&2
    exit 1
fi

# Verify tests directory exists
if [ ! -d "$test_path" ]; then
    echo "Error: Tests directory not found at: $test_path" >&2
    exit 1
fi

echo "Using test generator tool at: $tool_path"
echo "Starting test generation..."

error_count=0

for test_folder in "$test_path"/*; do
    if [ -d "$test_folder" ]; then
        echo "Generating test for $test_folder"
        
        # Change directory safely
        if ! cd "$test_folder"; then
            echo "Error: Failed to change directory to $test_folder" >&2
            error_count=$((error_count + 1))
            continue
        fi
        
        # Run the test generator
        if ! dotnet run --project "$tool_path"; then
            echo "Error: Test generation failed for $test_folder" >&2
            error_count=$((error_count + 1))
        fi
        
        # Always return to original directory
        cd "$original_dir" || {
            echo "Error: Failed to return to original directory" >&2
            exit 1
        }
    fi
done

echo "Test generation completed."

if [ $error_count -gt 0 ]; then
    echo "Warning: $error_count test folder(s) failed" >&2
    exit 1
fi

exit 0

