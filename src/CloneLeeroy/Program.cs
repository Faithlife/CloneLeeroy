using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CloneLeeroy
{
	public static class Program
	{
		public static async Task<int> Main(string[] args)
		{
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

			rootCommand.Handler = CommandHandler.Create<bool, string>(Run);

			// Parse the incoming args and invoke the handler
			return await rootCommand.InvokeAsync(args);
		}

		private static async Task Run(bool save, string project)
		{
			Console.WriteLine(save);
			Console.WriteLine(project);
		}
	}
}
