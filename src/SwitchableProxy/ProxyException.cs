using System;


namespace Orlum.SwitchableProxy
{
	/// <summary>
	/// A class containing the details of an exception which occurred when processing a tor operation.
	/// </summary>
	public class ProxyException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ProxyException"/> class.
		/// </summary>
		public ProxyException() : base()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProxyException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public ProxyException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProxyException"/> class.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public ProxyException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}