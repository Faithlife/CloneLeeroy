using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CloneLeeroy
{
	public static class Program
	{
		public static async Task<int> Main(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;

			CloneLeeroyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloneLeeroy");
			Directory.CreateDirectory(CloneLeeroyPath);
			ConfigurationPath = Path.Combine(CloneLeeroyPath, "Configuration");

			// try to read a project name from a saved configuration file
			string? projectName = null;
			try
			{
				await using var stream = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), c_configurationFileName));
				var settings = await JsonSerializer.DeserializeAsync<SavedSettings>(stream);
				projectName = settings?.ProjectName;
			}
			catch (Exception)
			{
			}

			// make the 'project' argument have a default value if one was saved
			var projectArgument = projectName is null ? new Argument<string>("project") : new Argument<string>("project", () => projectName);
			projectArgument.Description = "Project name";
			if (Directory.Exists(ConfigurationPath))
				projectArgument.Suggestions.Add(new ProjectSuggestionSource(ConfigurationPath));

			var rootCommand = new RootCommand("Clones repositories required by a Leeroy config")
			{
				new Option<bool>("--save", description: "Save the configuration to the current directory"),
				new Option<bool>("--solution-info-csharp", description: "Create a SolutionInfo.cs file"),
				new Option<bool>("--solution-info-header", description: "Create a SolutionInfo.h file"),
				projectArgument,
			};

			rootCommand.Handler = CommandHandler.Create(async (bool save, bool solutionInfoCsharp, bool solutionInfoHeader, string project) =>
			{
				try
				{
					return await Run(save, solutionInfoCsharp, solutionInfoHeader, project);
				}
				catch (LeeroyException ex)
				{
					using (SetColor(ConsoleColor.Red))
						Console.Error.WriteLine(ex.Message);
					return ex.ExitCode ?? 99;
				}
			});

			return await rootCommand.InvokeAsync(args);
		}

		private static async Task<int> Run(bool save, bool solutionInfoCsharp, bool solutionInfoHeader, string project)
		{
			await UpdateConfiguration(ConfigurationPath);

			Console.WriteLine("Cloning '{0}'", project);

			// read Leeroy configuration
			var configurationFilePath = Path.Combine(ConfigurationPath, project + ".json");
			LeeroyConfiguration? leeroyConfiguration;
			try
			{
				using var stream = File.OpenRead(configurationFilePath);
				leeroyConfiguration = await JsonSerializer.DeserializeAsync<LeeroyConfiguration>(stream);
			}
			catch (FileNotFoundException ex)
			{
				throw new LeeroyException($"Configuration '{project}' not found", exitCode: 92, innerException: ex);
			}
			catch (Exception ex)
			{
				throw new LeeroyException($"Error reading configuration '{project}': {ex.Message}", innerException: ex);
			}

			var submodules = leeroyConfiguration?.Submodules;
			if (submodules is null)
				throw new LeeroyException($"No submodules defined in '{project}'", exitCode: 93);

			if (solutionInfoCsharp)
				await File.WriteAllTextAsync("SolutionInfo.cs", "using System.Reflection;\n\n[assembly: AssemblyVersion(\"9.99.0.0\")]\n[assembly: AssemblyCompany(\"Faithlife\")]\n[assembly: AssemblyCopyright(\"Copyright 2021 Faithlife\")]\n[assembly: AssemblyDescription(\"Local Build\")]\n");
			if (solutionInfoHeader)
				await File.WriteAllTextAsync("SolutionInfo.h", "#define ASSEMBLY_VERSION_MAJOR 9\n#define ASSEMBLY_VERSION_MINOR 99\n#define ASSEMBLY_VERSION_BUILD 0\n#define ASSEMBLY_VERSION_MAJOR_MINOR_BUILD 1337\n#define ASSEMBLY_VERSION_REVISION 0\n#define ASSEMBLY_VERSION_STRING \"9.99.0.0\"\n\n#define ASSEMBLY_COMPANY \"Faithlife\"\n#define ASSEMBLY_COPYRIGHT \"Copyright 2021 Faithlife\"\n");

			// start processing each submodule in parallel
			var submoduleTasks = new List<(string Name, Task Task)>();
			foreach (var (name, branch) in submodules.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
				submoduleTasks.Add((name, UpdateSubmodule(Directory.GetCurrentDirectory(), name, branch)));

			var failures = new List<(string Name, LeeroyException Exception)>();

			// write the status of each submodule (as they complete)
			foreach (var (name, task) in submoduleTasks)
			{
				Console.Write("{0}...", name);
				var success = true;
				try
				{
					await task;
				}
				catch (LeeroyException ex)
				{
					failures.Add((name, ex));
					success = false;
				}
				using (SetColor(success ? ConsoleColor.Green : ConsoleColor.Red))
					Console.WriteLine(success ? "✔️" : "❌");
			}

			foreach (var failure in failures)
			{
				Console.Error.WriteLine();
				using (SetColor(ConsoleColor.Red))
					Console.Error.WriteLine($"{failure.Name}: {failure.Exception.Message}");
				Console.Error.WriteLine(failure.Exception.ErrorOutput);
			}

			if (save)
			{
				await using var stream = File.Create(Path.Combine(Directory.GetCurrentDirectory(), c_configurationFileName));
				await JsonSerializer.SerializeAsync(stream, new SavedSettings { ProjectName = project });
			}

			return failures.Count;
		}

		// Clones/updates the Build/Configuration repo in %LOCALAPPDATA%\CloneLeeroy\Configuration.
		private static async Task UpdateConfiguration(string configurationPath)
		{
			if (Directory.Exists(configurationPath))
			{
				if (await RunGit(configurationPath, "pull") is not (0, _, _))
				{
					using (SetColor(ConsoleColor.Yellow))
						await Console.Error.WriteLineAsync("WARNING: Couldn't pull latest changes to Configuration repository");
				}
			}
			else
			{
				VerifySuccess(await RunGit(Path.GetDirectoryName(configurationPath)!, "clone", "git@git.faithlife.dev:Build/Configuration.git"),
					"Couldn't clone Configuration repository", 91);
			}
		}

		private static async Task UpdateSubmodule(string folder, string name, string branch)
		{
			var nameParts = name.Split('/', 2);
			var (user, repo) = (nameParts[0], nameParts[1]);
			var remoteUrl = $"git@git.faithlife.dev:{user}/{repo}.git";

			var submodulePath = Path.Combine(folder, repo);
			if (Directory.Exists(submodulePath))
			{
				// check and reset remote URL for origin
				var currentUrl = VerifySuccess(await RunGit(submodulePath, "config", "--get", "remote.origin.url"), "Couldn't read origin URL");
				if (currentUrl != remoteUrl)
				{
					VerifySuccess(await RunGit(submodulePath, "remote", "rm", "origin"), "Couldn't remove remote origin");
					VerifySuccess(await RunGit(submodulePath, "remote", "add", "origin", remoteUrl), "Couldn't add remote origin");
				}

				VerifySuccess(await RunGit(submodulePath, "fetch", "origin"), "Couldn't fetch origin");

				// check current branch and switch if necessary
				var currentBranch = VerifySuccess(await RunGit(submodulePath, "symbolic-ref", "--short", "-q", "HEAD"), "Couldn't get current branch");
				if (currentBranch != branch)
					VerifySuccess(await RunGit(submodulePath, "checkout", "-B", branch, "--track", $"origin/{branch}"), $"Couldn't checkout ${branch}");

				VerifySuccess(await RunGit(submodulePath, "pull", "--rebase", "origin", branch), "Couldn't pull with rebase");
			}
			else
			{
				VerifySuccess(await RunGit(folder, "clone", "--recursive", "--branch", branch, remoteUrl), $"Couldn't clone {remoteUrl}");
			}
		}

		private static string VerifySuccess((int ExitCode, string Stdout, string Stderr) gitResults, string message, int? exitCode = default)
		{
			if (gitResults.ExitCode != 0)
				throw new LeeroyException(message, errorOutput: gitResults.Stderr.Trim(), exitCode: exitCode);
			return gitResults.Stdout.Trim();
		}

		private static async Task<(int ExitCode, string Stdout, string Stderr)> RunGit(string workingDirectory, params string[] arguments)
		{
			using var process = new System.Diagnostics.Process
			{
				StartInfo =
				{
					FileName = "git",
					WorkingDirectory = workingDirectory,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					StandardErrorEncoding = Encoding.UTF8,
					StandardOutputEncoding = Encoding.UTF8,
				},
				EnableRaisingEvents = true,
			};
			foreach (var argument in arguments)
				process.StartInfo.ArgumentList.Add(argument);

			var output = new StringBuilder();
			var error = new StringBuilder();
			process.OutputDataReceived += (sender, args) => output.Append(args.Data + Environment.NewLine);
			process.ErrorDataReceived += (sender, args) => error.Append(args.Data + Environment.NewLine);

			if (!process.Start())
				throw new InvalidOperationException("Couldn't start git");
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			await process.WaitForExitAsync();

			return (process.ExitCode, output.ToString(), error.ToString());
		}

		private static ScopedConsoleColor SetColor(ConsoleColor color)
		{
			var oldColor = Console.ForegroundColor;
			Console.ForegroundColor = color;
			return new(oldColor);
		}

		private static string CloneLeeroyPath { get; set; } = "";
		private static string ConfigurationPath { get; set; } = "";

		private const string c_configurationFileName = ".clonejs";
	}
}
