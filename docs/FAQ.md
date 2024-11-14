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