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

			// try to read a project name from a saved configuration file
			string? projectName = null;
			try
			{
				await using var stream = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), ".clonejs"));
				var settings = await JsonSerializer.DeserializeAsync<SavedSettings>(stream);
				projectName = settings?.ProjectName;
			}
			catch (Exception)
			{
			}

			// make the 'project' argument have a default value if one was saved
			var projectArgument = projectName is null ? new Argument<string>("project") : new Argument<string>("project", () => projectName);
			projectArgument.Description = "Project name";

			var rootCommand = new RootCommand("Clones repositories required by a Leeroy config")
			{
				new Option<bool>(
					"--save",
					description: "Save the configuration to the current directory"),
				projectArgument,
			};

			rootCommand.Handler = CommandHandler.Create<bool, string>(async (bool save, string project) =>
			{
				try
				{
					return await Run(project);
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

		private static async Task<int> Run(string project)
		{
			var cloneLeeroyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloneLeeroy");
			Directory.CreateDirectory(cloneLeeroyPath);
			var configurationPath = Path.Combine(cloneLeeroyPath, "Configuration");
			await UpdateConfiguration(configurationPath);

			Console.WriteLine("Cloning '{0}'", project);

			// read Leeroy configuration
			var configurationFilePath = Path.Combine(configurationPath, project + ".json");
			LeeroyConfiguration? leeroyConfiguration;
			try
			{
				using var stream = File.OpenRead(configurationFilePath);
				leeroyConfiguration = await JsonSerializer.DeserializeAsync<LeeroyConfiguration>(stream);
			}
			catch (FileNotFoundException)
			{
				throw new LeeroyException($"Configuration '{project}' not found", 2);
			}
			catch (Exception ex)
			{
				throw new LeeroyException($"Error reading configuration '{project}': {ex.Message}", innerException: ex);
			}

			var submodules = leeroyConfiguration?.Submodules;
			if (submodules is null)
			{
				throw new LeeroyException($"No submodules defined in '{project}'", 3);
			}

			// start processing each submodule in parallel
			var submoduleTasks = new List<(string Name, Task<bool> Task)>();
			foreach (var (name, branch) in submodules.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
				submoduleTasks.Add((name, UpdateSubmodule(Directory.GetCurrentDirectory(), name, branch)));

			// write the status of each submodule (as they complete)
			foreach (var (name, task) in submoduleTasks)
			{
				Console.Write("{0}...", name);
				var success = await task;
				using (SetColor(success ? ConsoleColor.Green : ConsoleColor.Red))
					Console.WriteLine(success ? "✔️" : "❌");
			}

			return 0;
		}

		// Clones/updates the Build/Configuration repo in %LOCALAPPDATA%\CloneLeeroy\Configuration.
		private static async Task UpdateConfiguration(string configurationPath)
		{
			if (Directory.Exists(configurationPath))
			{
				var (code, output, error) = await RunGit(configurationPath, "pull");
				if (code != 0)
				{
					using (SetColor(ConsoleColor.Yellow))
						await Console.Error.WriteLineAsync("WARNING: Couldn't pull latest changes to Configuration repository");
				}
			}
			else
			{
				var (code, output, error) = await RunGit(Path.GetDirectoryName(configurationPath)!, "clone", "git@git.faithlife.dev:Build/Configuration.git");
				if (code != 0)
					throw new LeeroyException("Couldn't clone Configuration repository\n" + error, 1);
			}
		}

		private static async Task<bool> UpdateSubmodule(string folder, string name, string branch)
		{
			var nameParts = name.Split('/', 2);
			var (user, repo) = (nameParts[0], nameParts[1]);
			var url = $"git@git.faithlife.dev:{user}/{repo}.git";

			var submodulePath = Path.Combine(folder, repo);
			if (Directory.Exists(submodulePath))
			{
				var (code, _, _) = await RunGit(submodulePath, "pull", "--rebase", "origin", branch);
				return code == 0;
			}
			else
			{
				var (code, _, _) = await RunGit(folder, "clone", "--recursive", "--branch", branch, url);
				return code == 0;
			}
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
			process.OutputDataReceived += (sender, args) => output.Append(args.Data);
			process.ErrorDataReceived += (sender, args) => error.Append(args.Data);

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
	}
}
