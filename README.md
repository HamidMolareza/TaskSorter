<h1 align="center">
  <a href="">
    <img src="./docs/images/logo.png" alt="Logo" width="100" height="100">
  </a>
</h1>

<div align="center">
  TaskSorter
  <br />
  <a href="#getting-started"><strong>Getting Started »</strong></a>
  <br />
  <br />
  <a href="https://github.com/HamidMolareza/TaskSorter/issues/new?assignees=&labels=bug&template=BUG_REPORT.md&title=bug%3A+">Report a Bug</a>
  ·
  <a href="https://github.com/HamidMolareza/TaskSorter/issues/new?assignees=&labels=enhancement&template=FEATURE_REQUEST.md&title=feat%3A+">Request a Feature</a>
  .
  <a href="https://github.com/HamidMolareza/TaskSorter/issues/new?assignees=&labels=question&template=SUPPORT_QUESTION.md&title=support%3A+">Ask a Question</a>
</div>

<div align="center">
<br />



![Docker Pulls](https://img.shields.io/docker/pulls/hamidmolareza/task-sorter)
![Docker Image Size (latest semver)](https://badgen.net/docker/size/hamidmolareza/task-sorter?icon=docker&label=image%20size)
![Docker Image Version](https://img.shields.io/docker/v/hamidmolareza/task-sorter?sort=semver)

![GitHub](https://img.shields.io/github/license/HamidMolareza/TaskSorter)
[![Pull Requests welcome](https://img.shields.io/badge/PRs-welcome-ff69b4.svg?style=flat-square)](https://github.com/HamidMolareza/TaskSorter/issues?q=is%3Aissue+is%3Aopen+label%3A%22help+wanted%22)
[![code with love by HamidMolareza](https://img.shields.io/badge/%3C%2F%3E%20with%20%E2%99%A5%20by-HamidMolareza-ff1414.svg?style=flat-square)](https://github.com/HamidMolareza)

</div>

## About

**TaskSorter** is a command-line application crafted to bring structure and clarity to GitHub task management, tailored
for developers and project managers who rely heavily on GitHub issues and pull requests to track progress and
priorities.

### Problem It Solves

In collaborative development projects, managing priorities across multiple repositories can be challenging. Often,
issues and pull requests are scattered, with varying degrees of importance that depend on repository significance and
label assignments. TaskSorter simplifies this by assigning a clear, customizable priority score to each task, enabling
you to focus on what truly matters.

### Purpose and Goals

The purpose of TaskSorter is to streamline the process of prioritizing GitHub tasks based on pre-set repository and
label priorities. By using a scoring mechanism, it ensures that tasks align with the project’s most pressing needs,
saving time and improving productivity. With a straightforward CLI interface, TaskSorter aims to be an accessible,
lightweight tool for improving task management practices.

### Why It Matters

Effective task prioritization is crucial for productivity and project success. TaskSorter allows users to tackle their
highest-priority tasks first, ensuring the most impactful work is addressed without unnecessary guesswork. This is
especially valuable in multi-repo environments or in cases where urgent tasks require immediate attention, cutting down
on time spent sorting tasks manually.

### Built With

- C#, dotnet 8
- docker

## Getting Started

### Prerequisites

- **Docker**: Make sure Docker is installed on your system. You can download it from
  the [official Docker website](https://www.docker.com/get-started) if needed.

### Installation

To get started with TaskSorter, pull the Docker image from the repository:

```bash
docker pull hamidmolareza/task-sorter:latest
```

### Usage

To run TaskSorter, you’ll need to provide it with two files specifying repository and label priorities. These files
should be mapped to Docker volumes so that TaskSorter can access them.

1. **Prepare Your Files**:
    - Create a `repo-priority.txt` file that lists your repositories in priority order (highest priority at the top).
    - Create a `label-priority.txt` file that lists your labels in priority order (highest priority at the top).

2. **Run TaskSorter with Docker**:
   Use the following command to run TaskSorter, mapping your priority files to the container:

   ```bash
   docker run --rm -v /path/to/repo-priority.txt:repo-priority.txt -v /path/to/label-priority.txt:label-priority.txt hamidmolareza/task-sorter -r repo-priority.txt -l label-priority.txt
   ```

   Replace `/path/to/repo-priority.txt` and `/path/to/label-priority.txt` with the actual paths to your priority files.

> For file names, you can use any name you want.

### Example

```bash
docker run --rm \
  -v $(pwd)/repo-priority.txt:repo-priority.txt \
  -v $(pwd)/label-priority.txt:label-priority.txt \
  hamidmolareza/task-sorter -r repo-priority.txt -l label-priority.txt
```

This command will run TaskSorter using your specified priority files, displaying sorted tasks based on calculated
scores.

Now you’re all set to efficiently manage and prioritize your GitHub tasks with TaskSorter!

## Example Files

### Repository Priority File (`repo-priority.txt`)

The higher the line, the more priority (and higher score) the repository receives:

```
owner/repo1
owner/repo2
```

### Label Priority File (`label-priority.txt`)

List labels by priority, with the highest priority on top:

```
priority-critical
priority-high
priority-medium
document
```

## How It Works

1. **Load Priorities**: TaskSorter reads the `repo-priority.txt` and `label-priority.txt` files to determine scoring.
2. **Fetch GitHub Issues and PRs**: Using GitHub API, it retrieves open issues and PRs from the repositories.
3. **Score Calculation**: For each task, it assigns scores based on the repository and label priorities.
4. **Sorting and Output**: Tasks are sorted by calculated score, with higher scores indicating higher priority.

## CHANGELOG

Please see the [CHANGELOG.md](./CHANGELOG.MD) file.

## Features

- **Repository and Label Priority Sorting**: Assign priorities to repositories and labels to rank tasks effectively.
- **Custom Scoring System**: Each task is given a score based on repository and label priorities, making it easy to sort
  tasks.
- **GitHub Issues and PRs**: Retrieves issues and PRs from specified repositories.

## Support

Reach out to the maintainer at one of the following places:

- [GitHub issues](https://github.com/HamidMolareza/TaskSorter/issues/new?assignees=&labels=question&template=SUPPORT_QUESTION.md&title=support%3A+)
- Contact options listed on [this GitHub profile](https://github.com/HamidMolareza)

## FAQ

### 1. **What is TaskSorter?**

TaskSorter is a command-line application that helps users prioritize GitHub issues and pull requests based on repository
and label priorities. It’s designed for developers and project managers who need a clear, organized view of their most
critical tasks.

### 2. **How does TaskSorter calculate priority?**

TaskSorter assigns priority scores based on the order of repositories and labels provided in two
files: `repo-priority.txt` and `label-priority.txt`. Tasks are given higher scores if they belong to higher-priority
repositories or have higher-priority labels, enabling sorting by overall importance.

### 3. **Can TaskSorter be used with private repositories?**

Yes, TaskSorter can access private repositories as long as you provide a GitHub token with appropriate permissions.
Ensure your token has at least read access for issues and pull requests in private repositories.

### 4. **How should I format the repository and label priority files?**

The repository and label files should each list items in descending order of priority (most important at the top).
Here’s an example:

**Repository Priority File (`repo-priority.txt`):**

   ```
   owner/repo1
   owner/repo2
   ```

**Label Priority File (`label-priority.txt`):**

   ```
   priority-critical
   priority-high
   priority-medium
   ```

### 5. **Is TaskSorter cross-platform?**

Yes, with Docker, you can run the program on any operating system. Additionally, this program is written in C#, which is
compatible with all operating systems.

### 6. **How do I install TaskSorter?**

You can install TaskSorter by cloning the repository and building it with .NET. For detailed instructions, refer to
the [Installation](#installation) section.

### 7. **Can I customize the scoring system?**

The scoring system is based on the order of priorities specified in the provided files. While direct customization of
scoring rules isn't available, adjusting the order of items in `repo-priority.txt` and `label-priority.txt` will affect
task prioritization.

### 8. **Does TaskSorter support automated scheduling to re-sort tasks?**

TaskSorter does not currently support automated scheduling. However, you can run it manually whenever you want to get an
updated view of task priorities. To automate this process, consider setting up a cron job (Linux/macOS) or Task
Scheduler (Windows) to periodically run TaskSorter.

### 9. **How can I contribute to TaskSorter?**

Contributions are welcome! If you have ideas or would like to add new features, please check out
the [CONTRIBUTING.md](CONTRIBUTING.md) file in the repository to get started.

### 10. **Who is TaskSorter for?**

TaskSorter is ideal for developers, project managers, and anyone who manages a large number of GitHub tasks across
multiple repositories and wants a quick way to prioritize them.

Feel free to reach out via the issues section in the repository if you have any other questions or need further
assistance.

## Contributing

First off, thanks for taking the time to contribute! Contributions are what make the free/open-source community such an
amazing place to learn, inspire, and create. Any contributions you make will benefit everybody else and are **greatly
appreciated**.

Please read [our contribution guidelines](docs/CONTRIBUTING.md), and thank you for being involved!

## Authors & contributors

The original setup of this repository is by [HamidMolareza](https://github.com/HamidMolareza).

For a full list of all authors and contributors,
see [the contributors page](https://github.com/HamidMolareza/TaskSorter/contributors).

## Security

PROJECT_NAME follows good practices of security, but 100% security cannot be assured. PROJECT_NAME is provided **"as
is"** without any **warranty**.

_For more information and to report security issues, please refer to our [security documentation](docs/SECURITY.md)._

## License

This project is licensed under the **GPLv3**.

See [LICENSE](LICENSE) for more information.

## Related

Here are some related projects

[Awesome README](https://github.com/matiassingers/awesome-readme)


