using System;

namespace CloneLeeroy
{
	internal readonly struct ScopedConsoleColor : IDisposable
	{
		public ScopedConsoleColor(ConsoleColor oldColor) => m_oldColor = oldColor;

		public void Dispose() => Console.ForegroundColor = m_oldColor;

		private readonly ConsoleColor m_oldColor;
	}
}
