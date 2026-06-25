#!/bin/bash
set -euo pipefail

# Usage:
#   chmod +x -R scripts (only first time, make scripts executable)
#   ./scripts/setup-hooks.sh (execute script)

# Function to enable a specific Git hook by copying its sample file
enable_hook() {
    local hooks_dir="$1"
    local hook_name="$2"
    local target_hook="$hooks_dir/$hook_name"
    local template_file="$hooks_dir/$hook_name.sample"

    if [[ -f "$target_hook" ]]; then
        echo "The $hook_name hook is already enabled. ($target_hook)"
    elif [[ -f "$template_file" ]]; then
        echo "Enabling $hook_name hook..."
        mv "$template_file" "$target_hook"
        echo "Template hook copied to $target_hook."
    else
        echo "Template for $hook_name hook not found."
    fi
}

get_root_path() {
    git rev-parse --show-toplevel 2>/dev/null || {
        echo "Error: Unable to determine the Git repository root. Ensure this script is executed within a Git repository."
        return 1
    }
}

get_hooks_path() {
    git rev-parse --git-path hooks 2>/dev/null || {
        echo "Error: Unable to determine the Git hooks directory. Ensure Git is initialized in the repository."
        return 1
    }
}

get_package_json_path() {
    local root_path
    root_path=$(get_root_path) || {
        echo "Error: Cannot determine the package.json path as the Git repository root could not be found."
        return 1
    }

    local package_json_path="$root_path/package.json"
    if [[ -f "$package_json_path" ]]; then
        echo "$package_json_path"
    else
        echo "Warning: package.json not found in the repository root ($root_path)."
        return 1
    fi
}

command_exists() {
    command -v "$1" &>/dev/null
}

main() {
    # Check if `pre-commit` is installed
    if ! command_exists pre-commit; then
        echo "Error: pre-commit is not installed. Please install it first."
        exit 1
    fi

    # Check for package.json and npm
    local package_json_path
    if ! package_json_path=$(get_package_json_path); then
        echo "Skipping npm dependency installation as package.json is missing."
    elif ! command_exists npm; then
        echo "Error: npm is required to install dependencies from package.json but is not installed."
        exit 1
    fi

    # Install pre-commit hooks
    local pre_commit_hooks=("commit-msg" "prepare-commit-msg" "pre-merge-commit" "pre-push")
    for hook_type in "${pre_commit_hooks[@]}"; do
        echo "Installing $hook_type pre-commit hook..."
        pre-commit install --hook-type "$hook_type"
    done

    # Enable additional Git hooks
    local hooks_dir
    if hooks_dir=$(get_hooks_path); then
        enable_hook "$hooks_dir" "pre-rebase"
    else
        echo "Git hooks directory not found. Skipping additional hook configuration."
    fi

    # Install npm dependencies if package.json exists
    if [[ -n "${package_json_path:-}" ]]; then
        echo "Installing npm dependencies..."
        npm install
    fi
}

# Execute the main function
main "$@"
