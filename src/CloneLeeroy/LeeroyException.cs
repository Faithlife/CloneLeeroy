using System;

namespace CloneLeeroy
{
	public sealed class LeeroyException : Exception
	{
		public LeeroyException(string message, string? errorOutput = null, int? exitCode = default, Exception? innerException = null)
			: base(message, innerException)
		{
			ErrorOutput = errorOutput;
			ExitCode = exitCode;
		}

		public string? ErrorOutput { get; }
		public int? ExitCode { get; }
	}
}
