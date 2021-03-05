using System;

namespace CloneLeeroy
{
	public sealed class LeeroyException : Exception
	{
		public LeeroyException(string message, int? exitCode = default, Exception? innerException = null)
			: base(message, innerException)
		{
			ExitCode = exitCode;
		}

		public int? ExitCode { get; }
	}
}
