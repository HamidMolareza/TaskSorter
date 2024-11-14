# Contributing

When contributing to this repository, please first discuss the change you wish to make via **issue**, email, or any
other
method with the owners of this repository before making a change. Please note we have
a [code of conduct](CODE_OF_CONDUCT.md), please follow it in all your interactions with the project.

Below is a guide on setting up your development environment and submitting a pull request.

### Development Environment Setup

1. **Clone the Repository**: Begin by cloning the TaskSorter repository.

   ```bash
   git clone https://github.com/HamidMolareza/TaskSorter.git
   cd tasksorter
   ```

2. **Ensure .NET SDK 8.0 is Installed**: TaskSorter requires .NET SDK 8.0.

3. **Install Git Hooks (Optional)**: If you’d like to use the provided Git hooks for better commit quality, run the
   setup script:

   ```bash
   ./scripts/setup-hooks.sh
   ```

   This script will set up pre-configured Git hooks to assist with linting and commit message formats.

## Issues and feature requests

You've found a bug in the source code, a mistake in the documentation or maybe you'd like a new feature? You can help us
by [submitting an issue on GitHub](https://github.com/GITHUB_USERNAME/REPO_SLUG/issues). Before you create an issue,
make sure to search the issue archive -- your issue may have already been addressed!

Please try to create bug reports that are:

- _Reproducible._ Include steps to reproduce the problem.
- _Specific._ Include as much detail as possible: which version, what environment, etc.
- _Unique._ Do not duplicate existing opened issues.
- _Scoped to a Single Bug._ One bug per report.

**Even better: Submit a pull request with a fix or new feature!**

### How to Submit a Pull Request

1. **Follow Conventional Commits**: This project uses [Conventional Commits](https://www.conventionalcommits.org/) for
   clear, consistent commit messages. Some example commit messages:

    - `feat: add support for new sorting option`
    - `fix: resolve issue with priority score calculation`
    - `docs: update README with usage instructions`

2. **Create a New Branch**: Work on a new branch for each feature or bug fix.

   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Make Your Changes**: Develop your feature or fix while ensuring code quality and project standards are met.

4. **Push Your Changes**: Once your changes are complete and tested, push the branch to the repository.

   ```bash
   git push origin feature/your-feature-name
   ```

5. **Open a Pull Request**: Go to the repository on GitHub, locate your newly pushed branch, and create a pull request.
   Please include a clear description of the changes you’ve made and reference any related issues.

6. **Review and Merge**: Once your pull request is reviewed and approved, it will be merged into the main codebase.

Thank you for helping make TaskSorter better!